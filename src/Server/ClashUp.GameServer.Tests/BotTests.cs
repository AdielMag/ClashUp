using ClashUp.Server.GameServer.Simulation;
using ClashUp.Server.Services.Matchmaking;
using ClashUp.Shared.Characters;
using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests;

public class MatchmakingQueueBotFillTests
{
    [Fact]
    public void CountQueued_CountsOnlyMatchingMode()
    {
        var q = new MatchmakingQueue();
        q.Enqueue("a", "m1");
        q.Enqueue("b", "m1");
        q.Enqueue("c", "m2");

        q.CountQueued("m1").Should().Be(2);
        q.CountQueued("m2").Should().Be(1);
        q.CountQueued("none").Should().Be(0);
    }

    [Fact]
    public void OldestEnqueuedAt_ReturnsEarliest_OrNull()
    {
        var q = new MatchmakingQueue();
        q.OldestEnqueuedAt("m1").Should().BeNull();

        var first = q.Enqueue("a", "m1");
        q.Enqueue("b", "m1");

        q.OldestEnqueuedAt("m1").Should().Be(first.EnqueuedAt);
    }

    [Fact]
    public void DrainUpTo_ReturnsPartialBatch_AndPreservesOtherModes()
    {
        var q = new MatchmakingQueue();
        q.Enqueue("a", "m1");
        q.Enqueue("x", "m2");
        q.Enqueue("b", "m1");

        var batch = q.DrainUpTo(5, "m1");

        batch.Select(t => t.PlayerId).Should().BeEquivalentTo(new[] { "a", "b" });
        q.CountQueued("m1").Should().Be(0);
        // The other mode's ticket survives the partial drain.
        q.GetQueuedModeIds().Should().BeEquivalentTo(new[] { "m2" });
    }

    [Fact]
    public void DrainUpTo_CapsAtMax()
    {
        var q = new MatchmakingQueue();
        q.Enqueue("a", "m1");
        q.Enqueue("b", "m1");
        q.Enqueue("c", "m1");

        q.DrainUpTo(2, "m1").Should().HaveCount(2);
        q.CountQueued("m1").Should().Be(1);
    }
}

public class BotDirectorTests
{
    // Stub simulation that feeds the BotDirector a fixed perception view.
    private sealed class FakeSim : IServerSimulation
    {
        private readonly BotView _view;
        private readonly bool _has;
        public FakeSim(BotView view, bool has = true) { _view = view; _has = has; }

        public int CurrentTick => 0;
        public uint RandomSeed => 12345u;
        public void LoadMap(MapData mapData) { }
        public void Configure(string objectiveType) { }
        public bool IsEliminated(string playerId) => false;
        public void EnsurePlayer(PlayerId player, int colorSlot, int teamId, CharacterId characterId) { }
        public void ApplyInput(PlayerId player, InputCommand command) { }
        public void Step(double deltaSeconds) { }
        public ReadOnlyMemory<byte> EncodeDelta(int baselineTick) => ReadOnlyMemory<byte>.Empty;
        public IReadOnlyList<MatchEvent> DrainAbilityEvents() => Array.Empty<MatchEvent>();
        public IReadOnlyDictionary<string, int> GetTeamScores() => new Dictionary<string, int>();
        public bool TryGetBotView(string botId, out BotView view) { view = _view; return _has; }
        public void Dispose() { }
    }

    // Enemy (when present) is placed at +X so movement sign is easy to assert.
    private static BotView View(bool hasEnemy, float dist, bool alive = true) => new()
    {
        SelfX = 0f,
        SelfZ = 0f,
        SelfYaw = 0f,
        SelfAlive = alive,
        HasEnemy = hasEnemy,
        EnemyX = dist,
        EnemyZ = 0f,
        EnemyDistance = dist,
    };

    private static readonly uint ActiveAbilityMask = (1u << 1) | InputCommand.AutoAimFlag;

    [Fact]
    public void DeadBot_ProducesIdleInput()
    {
        var d = new BotDirector();
        d.RegisterBot("b", 1u);
        var sim = new FakeSim(View(hasEnemy: true, dist: 1f, alive: false));

        var input = d.Think("b", sim, 5);

        input.MoveX.Should().Be((short)0);
        input.MoveY.Should().Be((short)0);
        input.ButtonMask.Should().Be(0u);
        input.Tick.Should().Be(5);
    }

    [Fact]
    public void UnregisteredBot_ProducesIdleInput()
    {
        var d = new BotDirector();
        var sim = new FakeSim(View(hasEnemy: true, dist: 1f));

        d.Think("ghost", sim, 9).ButtonMask.Should().Be(0u);
    }

    [Fact]
    public void NoEnemy_Wanders_WithMovement_AndDoesNotAttack()
    {
        var d = new BotDirector();
        d.RegisterBot("b", 99u);
        var sim = new FakeSim(View(hasEnemy: false, dist: float.MaxValue));

        var input = d.Think("b", sim, 1);

        input.ButtonMask.Should().Be(0u);
        (input.MoveX != 0 || input.MoveY != 0).Should().BeTrue("a wandering bot picks a heading");
    }

    [Fact]
    public void EnemyDetected_Chases_TowardEnemy_WithoutAttacking()
    {
        var d = new BotDirector();
        d.RegisterBot("b", 3u);
        // Within detect range (12) but beyond attack range (4).
        var sim = new FakeSim(View(hasEnemy: true, dist: 8f));

        var chase = d.Think("b", sim, 1); // Wander -> Chase

        chase.ButtonMask.Should().Be(0u);
        MovementModel.DecodeAxis(chase.MoveX).Should().BeGreaterThan(0f);
    }

    [Fact]
    public void EnemyInRange_Attacks_WithActiveAbility_TowardEnemy()
    {
        var d = new BotDirector();
        d.RegisterBot("b", 7u);
        var sim = new FakeSim(View(hasEnemy: true, dist: 3f)); // within attack range (4)

        d.Think("b", sim, 1);              // Wander -> Chase
        var attack = d.Think("b", sim, 2); // Chase -> Attack

        attack.ButtonMask.Should().Be(ActiveAbilityMask);
        MovementModel.DecodeAxis(attack.MoveX).Should().BeGreaterThan(0f);
    }

    [Fact]
    public void AfterAttacking_Flees_AwayFromEnemy()
    {
        var d = new BotDirector();
        d.RegisterBot("b", 11u);
        var sim = new FakeSim(View(hasEnemy: true, dist: 3f));

        // 1 tick to Chase, 1 to Attack, then ~30 ticks of attacking before the flee transition.
        for (int i = 0; i < 40; i++) d.Think("b", sim, i);
        var fleeing = d.Think("b", sim, 100);

        MovementModel.DecodeAxis(fleeing.MoveX).Should().BeLessThan(0f, "the bot runs away from the +X enemy");
    }

    [Fact]
    public void NoPlayer_WithBox_SeeksBox_WithoutSpendingActiveAbility()
    {
        var d = new BotDirector();
        d.RegisterBot("b", 5u);
        var view = new BotView
        {
            SelfAlive = true,
            HasEnemy = false,
            EnemyDistance = float.MaxValue,
            HasBox = true,
            BoxX = 5f,
            BoxDistance = 5f,
        };
        var sim = new FakeSim(view);

        var input = d.Think("b", sim, 1); // Wander -> SeekBox

        input.ButtonMask.Should().Be(0u, "the active ability is saved for players");
        MovementModel.DecodeAxis(input.MoveX).Should().BeGreaterThan(0f, "the bot heads to the +X box");
    }

    [Fact]
    public void CloserPlayer_IsPreferredOverBox()
    {
        var d = new BotDirector();
        d.RegisterBot("b", 6u);
        // Player at +X dist 3, box at -X dist 5 → engage the (closer) player.
        var view = new BotView
        {
            SelfAlive = true,
            HasEnemy = true, EnemyX = 3f, EnemyDistance = 3f,
            HasBox = true, BoxX = -5f, BoxDistance = 5f,
        };
        var sim = new FakeSim(view);

        var chase = d.Think("b", sim, 1);

        MovementModel.DecodeAxis(chase.MoveX).Should().BeGreaterThan(0f, "moves toward the +X player, not the -X box");
    }

    [Fact]
    public void MuchCloserBox_IsPreferredOverFartherPlayer()
    {
        var d = new BotDirector();
        d.RegisterBot("b", 8u);
        // Box at +X dist 2, player at -X dist 10 (within detect). 10 > 2 * PlayerPreference → go for the box.
        var view = new BotView
        {
            SelfAlive = true,
            HasEnemy = true, EnemyX = -10f, EnemyDistance = 10f,
            HasBox = true, BoxX = 2f, BoxDistance = 2f,
        };
        var sim = new FakeSim(view);

        var input = d.Think("b", sim, 1);

        MovementModel.DecodeAxis(input.MoveX).Should().BeGreaterThan(0f, "moves toward the much-closer +X box");
        input.ButtonMask.Should().Be(0u);
    }
}
