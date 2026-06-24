using System;
using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;

namespace ClashUp.Server.GameServer.Simulation;

/// <summary>
/// Per-match AI driver. For each registered bot it produces one synthetic <see cref="InputCommand"/>
/// per tick from a small wander → chase → attack → flee state machine, fed by the simulation's
/// <see cref="BotView"/> perception. Scoped service — one instance per MatchContext scope.
///
/// Auto-attacks already auto-fire server-side (AbilityExecutor), so the bot only steers movement and
/// presses the active ability while engaged; the executor no-ops the press while it's on cooldown.
/// </summary>
public sealed class BotDirector
{
    private enum Phase { Wander, Chase, Attack, Flee, SeekBox }

    private sealed class BotState
    {
        public Phase Phase = Phase.Wander;
        public DeterministicRng Rng = new(1u);
        public float WanderX;
        public float WanderZ;
        public int WanderTicksLeft;
        public int AttackTicksLeft;
        public int FleeTicksLeft;
        public int Seq;
    }

    // Tunable AI constants (distances in world units, durations in ticks @ 30 Hz).
    private const float DetectRange = 12f;      // start chasing within this distance
    private const float AttackRange = 4f;       // close enough to engage / attack
    private const int WanderRethinkTicks = 30;  // re-roll the wander heading ~1s
    private const int AttackHoldTicks = 30;     // ~1s of attacking before fleeing
    private const int FleeTicks = 45;           // ~1.5s of running away

    // Utility weighting (smarter than pure nearest-target): a player is preferred over a box even when
    // somewhat farther — players are the real threat/objective. >1 biases toward fighting players.
    private const float PlayerPreference = 1.25f;

    // The active/manual ability sits in slot 1 (slot 0 is the auto-attack). See AbilityExecutor.InitPlayer.
    // AutoAimFlag lets the executor resolve aim/target onto the nearest enemy. No-op if the character
    // has no active ability.
    private const uint ActiveAbilityMask = (1u << 1) | InputCommand.AutoAimFlag;

    private const float TwoPi = 6.2831853f;

    private readonly Dictionary<string, BotState> _bots = new(StringComparer.Ordinal);
    private uint _seedCounter;

    public bool HasBots => _bots.Count > 0;

    /// <summary>Register a bot so <see cref="Think"/> will drive it. <paramref name="matchSeed"/> makes
    /// the per-bot RNG reproducible for a given match.</summary>
    public void RegisterBot(string botId, uint matchSeed)
    {
        uint seed = matchSeed ^ (0x9E3779B9u * (++_seedCounter));
        _bots[botId] = new BotState
        {
            Rng = new DeterministicRng(seed == 0 ? 1u : seed),
        };
    }

    /// <summary>Produce this bot's input for the current tick.</summary>
    public InputCommand Think(string botId, IServerSimulation sim, int tick)
    {
        if (!_bots.TryGetValue(botId, out var bot)
            || !sim.TryGetBotView(botId, out var view)
            || !view.SelfAlive)
            return Idle(tick);

        bool enemyDetected = view.HasEnemy && view.EnemyDistance <= DetectRange;
        bool enemyInRange = view.HasEnemy && view.EnemyDistance <= AttackRange;

        // Target arbitration (utility): engage a player when one is detected AND it's the better target
        // than the nearest box (players preferred via PlayerPreference). Otherwise farm the nearest box.
        bool preferPlayer = enemyDetected
            && (!view.HasBox || view.EnemyDistance <= view.BoxDistance * PlayerPreference);

        // Unit vector toward the enemy (zero when there is none).
        float toEnemyX = 0f, toEnemyZ = 0f;
        if (view.HasEnemy)
        {
            float dx = view.EnemyX - view.SelfX;
            float dz = view.EnemyZ - view.SelfZ;
            float len = MathF.Sqrt(dx * dx + dz * dz);
            if (len > 1e-4f) { toEnemyX = dx / len; toEnemyZ = dz / len; }
        }

        // Unit vector toward the nearest box.
        float toBoxX = 0f, toBoxZ = 0f;
        if (view.HasBox)
        {
            float dx = view.BoxX - view.SelfX;
            float dz = view.BoxZ - view.SelfZ;
            float len = MathF.Sqrt(dx * dx + dz * dz);
            if (len > 1e-4f) { toBoxX = dx / len; toBoxZ = dz / len; }
        }

        // 1) State transitions. Flee always runs to completion (commit to the retreat after attacking).
        switch (bot.Phase)
        {
            case Phase.Wander:
                if (preferPlayer) bot.Phase = Phase.Chase;
                else if (view.HasBox) bot.Phase = Phase.SeekBox;
                break;
            case Phase.SeekBox:
                if (preferPlayer) bot.Phase = Phase.Chase;       // a player became the better target
                else if (!view.HasBox) bot.Phase = Phase.Wander; // all boxes gone
                break;
            case Phase.Chase:
                if (enemyInRange) { bot.Phase = Phase.Attack; bot.AttackTicksLeft = AttackHoldTicks; }
                else if (!enemyDetected) bot.Phase = view.HasBox ? Phase.SeekBox : Phase.Wander;
                break;
            case Phase.Attack:
                bot.AttackTicksLeft--;
                if (!view.HasEnemy) bot.Phase = view.HasBox ? Phase.SeekBox : Phase.Wander;
                else if (bot.AttackTicksLeft <= 0) { bot.Phase = Phase.Flee; bot.FleeTicksLeft = FleeTicks; }
                break;
            case Phase.Flee:
                bot.FleeTicksLeft--;
                if (bot.FleeTicksLeft <= 0)
                    bot.Phase = preferPlayer ? Phase.Chase : (view.HasBox ? Phase.SeekBox : Phase.Wander);
                break;
        }

        // 2) Movement + buttons for the resulting phase.
        float moveX = 0f, moveZ = 0f;
        uint buttonMask = 0u;
        switch (bot.Phase)
        {
            case Phase.Wander:
                if (bot.WanderTicksLeft <= 0)
                {
                    float ang = bot.Rng.NextRange(0f, TwoPi);
                    bot.WanderX = MathF.Sin(ang);
                    bot.WanderZ = MathF.Cos(ang);
                    bot.WanderTicksLeft = WanderRethinkTicks;
                }
                bot.WanderTicksLeft--;
                moveX = bot.WanderX;
                moveZ = bot.WanderZ;
                break;
            case Phase.SeekBox:
                // Walk into the box; the auto-attack auto-fires on it (active ability is saved for players).
                moveX = toBoxX;
                moveZ = toBoxZ;
                break;
            case Phase.Chase:
                moveX = toEnemyX;
                moveZ = toEnemyZ;
                break;
            case Phase.Attack:
                // Stay close and attack. Auto-attack auto-fires; press the active ability too.
                moveX = toEnemyX * 0.3f;
                moveZ = toEnemyZ * 0.3f;
                buttonMask = ActiveAbilityMask;
                break;
            case Phase.Flee:
                moveX = -toEnemyX; // run directly away (zero if the enemy vanished)
                moveZ = -toEnemyZ;
                break;
        }

        bot.Seq++;
        return new InputCommand
        {
            Tick = tick,
            SequenceId = bot.Seq,
            MoveX = MovementModel.EncodeAxis(moveX),
            MoveY = MovementModel.EncodeAxis(moveZ),
            ButtonMask = buttonMask,
        };
    }

    private static InputCommand Idle(int tick) => new() { Tick = tick };
}
