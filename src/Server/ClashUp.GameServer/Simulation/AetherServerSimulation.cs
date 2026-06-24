using System.Collections.Generic;
using ClashUp.Server.GameServer.Abilities;
using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.Abilities;
using ClashUp.Shared.Characters;
using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using MessagePack;

namespace ClashUp.Server.GameServer.Simulation;

public sealed class AetherServerSimulation : IServerSimulation
{
    private readonly MatchCharactersHolder _characters;
    private readonly MatchPhysicsWorld _world = new();
    private readonly HealthTable _health = new();
    private readonly AbilityExecutor _abilities = new();
    private readonly ProjectileSimulation _projectiles = new();
    private readonly Dictionary<string, int> _lastSeq = new();
    private readonly Dictionary<int, int> _teamSlotCounters = new();
    private readonly HashSet<string> _knownPlayers = new();
    private readonly Dictionary<string, float> _maxHealth = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (float X, float Z)> _spawnPositions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _respawnTimers = new(StringComparer.Ordinal);

    // Objective-mode state (boxes / points / elimination). Inert in survival mode.
    private readonly Dictionary<string, int> _playerTeams = new(StringComparer.Ordinal);
    private readonly HashSet<string> _eliminated = new(StringComparer.Ordinal);
    private readonly List<MatchEvent> _pendingEvents = new();
    private readonly PlayerProgression _progression;
    private readonly PointOrbSimulation _orbs;
    private readonly BoxSimulation _boxes;
    private string _objectiveType = "survival";
    private bool _noRespawn;

    // 5 seconds at 30 Hz
    private const int RespawnDelayTicks = 150;
    private MapData? _mapData;

    public int CurrentTick { get; private set; }
    public uint RandomSeed { get; }

    public AetherServerSimulation(ServerAbilityStore abilityStore, MatchCharactersHolder characters)
    {
        _characters = characters;
        RandomSeed = (uint)System.Random.Shared.Next(1, int.MaxValue);
        _abilities.RegisterAbilities(abilityStore.All);
        _progression = new PlayerProgression(_health);
        _orbs = new PointOrbSimulation(RandomSeed);
        _boxes = new BoxSimulation(_orbs);
    }

    public void LoadMap(MapData mapData)
    {
        _mapData = mapData;
        _world.LoadMapGeometry(mapData);
    }

    public void Configure(string objectiveType)
    {
        _objectiveType = string.IsNullOrEmpty(objectiveType) ? "survival" : objectiveType;
        _noRespawn = _objectiveType == "elimination";

        // Box-based modes seed the arena's breakable boxes from the map (requires LoadMap first).
        if (_noRespawn && _mapData != null)
            _boxes.Initialize(_world, _mapData.BoxSpawns);
    }

    public bool IsEliminated(string playerId) => _eliminated.Contains(playerId);

    public void EnsurePlayer(PlayerId player, int colorSlot, int teamId, CharacterId characterId)
    {
        if (!_knownPlayers.Add(player.Value)) return;

        var character = _characters.Catalog.Get(characterId);
        var stats = character.BaseStats;

        if (!_teamSlotCounters.TryGetValue(teamId, out int slotIndex))
            slotIndex = 0;

        var (spawnX, spawnZ) = SpawnResolver.GetSpawnPosition(_mapData, teamId, slotIndex);
        _world.EnsurePlayer(player.Value, spawnX, spawnZ, stats.MoveSpeed);
        _health.Initialize(player.Value, stats.MaxHealth);
        _maxHealth[player.Value] = stats.MaxHealth;
        _spawnPositions[player.Value] = (spawnX, spawnZ);
        _playerTeams[player.Value] = teamId;
        _progression.RegisterPlayer(player.Value, stats.MaxHealth);
        _health.SetInvulnerable(player.Value, HealthTable.DefaultSpawnInvulnTicks);
        _abilities.InitPlayer(player.Value, character.AutoAttackId, character.ActiveAbilityId);

        _teamSlotCounters[teamId] = slotIndex + 1;
    }

    public void ApplyInput(PlayerId player, InputCommand command)
    {
        _lastSeq[player.Value] = command.SequenceId;
        if (!_health.IsAlive(player.Value)) return;

        _world.ApplyInput(player.Value,
            MovementModel.DecodeAxis(command.MoveX),
            MovementModel.DecodeAxis(command.MoveY));

        if (command.ButtonMask != 0)
        {
            float aimYaw = MovementModel.DecodeAxis(command.AimYawQ) * 180f;
            float aimMagnitude = MovementModel.DecodeAxis(command.AimDistanceQ);
            _abilities.ProcessInput(player.Value, command.ButtonMask, aimYaw, aimMagnitude, _world, _health, CurrentTick);
        }
    }

    public void Step(double deltaSeconds)
    {
        _world.Step(deltaSeconds);
        _health.Tick();
        // Boxes/progression are only relevant (and non-null) in objective modes.
        _abilities.Tick(_world, _health, _projectiles, _noRespawn ? _boxes : null,
            _noRespawn ? _progression : null, deltaSeconds, CurrentTick);
        _projectiles.Tick(_world, _health, _noRespawn ? _boxes : null,
            _noRespawn ? _progression : null, CurrentTick);

        if (_noRespawn)
        {
            _boxes.Tick(_world);
            _orbs.Tick(_world, _health, _progression, CurrentTick);
        }

        CurrentTick++;

        if (_noRespawn)
            HandleEliminations();
        else
            HandleRespawns();
    }

    // No-respawn modes: a player who hits 0 HP is out for good and drops all their points as orbs.
    private void HandleEliminations()
    {
        foreach (var id in _world.PlayerIds)
        {
            if (_health.IsAlive(id)) continue;
            if (!_eliminated.Add(id)) continue; // already eliminated

            var (x, z, _) = _world.GetPlayerState(id);
            int dropped = _progression.Take(id);
            if (dropped > 0)
                _orbs.DropFromPlayer(_world, x, z, dropped);

            int team = _playerTeams.TryGetValue(id, out var t) ? t : 0;
            _pendingEvents.Add(new MatchEvent
            {
                Tick = CurrentTick,
                Kind = "player_eliminated",
                Payload = System.Text.Json.JsonSerializer.Serialize(new { playerId = id, team, droppedPoints = dropped }),
            });
            Console.WriteLine($"[ELIM] {id[..6]} eliminated (team {team}, dropped {dropped} pts)");
        }
    }

    // Survival modes: 5-second respawn timer (unchanged legacy behavior).
    private void HandleRespawns()
    {
        foreach (var id in _world.PlayerIds)
        {
            if (_health.IsAlive(id)) continue;

            if (_respawnTimers.TryGetValue(id, out var ticks))
            {
                ticks--;
                if (ticks <= 0)
                {
                    _respawnTimers.Remove(id);
                    if (_maxHealth.TryGetValue(id, out var max))
                    {
                        _health.Initialize(id, max);
                        _health.SetInvulnerable(id, HealthTable.DefaultSpawnInvulnTicks);
                        if (_spawnPositions.TryGetValue(id, out var spawn))
                            _world.SnapPlayerPosition(id, spawn.X, spawn.Z);
                        Console.WriteLine($"[RESPAWN] {id[..6]} → spawn ({_spawnPositions.GetValueOrDefault(id)})");
                    }
                }
                else
                {
                    _respawnTimers[id] = ticks;
                }
            }
            else
            {
                _respawnTimers[id] = RespawnDelayTicks;
                Console.WriteLine($"[DIED] {id[..6]} — respawning in {RespawnDelayTicks} ticks");
            }
        }
    }

    public IReadOnlyList<MatchEvent> DrainAbilityEvents()
    {
        var merged = new List<MatchEvent>();
        merged.AddRange(_abilities.DrainEvents());
        merged.AddRange(_projectiles.DrainEvents());
        if (_noRespawn)
        {
            merged.AddRange(_boxes.DrainEvents());
            merged.AddRange(_orbs.DrainEvents());
        }
        if (_pendingEvents.Count > 0)
        {
            merged.AddRange(_pendingEvents);
            _pendingEvents.Clear();
        }
        return merged;
    }

    public ReadOnlyMemory<byte> EncodeDelta(int baselineTick)
    {
        var dtos = new List<PlayerStateDto>();
        foreach (var id in _world.PlayerIds)
        {
            var (x, z, yaw) = _world.GetPlayerState(id);
            dtos.Add(new PlayerStateDto
            {
                Id = new PlayerId(id),
                X = x,
                Z = z,
                Yaw = yaw,
                Health = _health.GetHealth(id),
                LastProcessedInputSeq = _lastSeq.TryGetValue(id, out var seq) ? seq : 0,
                IsInvulnerable = _health.IsInvulnerable(id),
                RespawnInTicks = _respawnTimers.TryGetValue(id, out var rt) ? rt : 0,
                Points = _progression.GetPoints(id),
                IsEliminated = _eliminated.Contains(id),
                MaxHealth = _health.GetMaxHealth(id),
            });
        }

        var packet = _noRespawn
            ? new WorldStatePacket { Players = dtos.ToArray(), Boxes = _boxes.Snapshot(), Orbs = _orbs.Snapshot(_world) }
            : new WorldStatePacket { Players = dtos.ToArray() };
        return MessagePackSerializer.Serialize(packet);
    }

    public bool TryGetBotView(string botId, out BotView view)
    {
        view = default;
        if (!_playerTeams.TryGetValue(botId, out int myTeam)) return false;

        var (sx, sz, syaw) = _world.GetPlayerState(botId);

        float bestDistSq = float.MaxValue;
        bool hasEnemy = false;
        float ex = 0f, ez = 0f;
        foreach (var id in _world.PlayerIds)
        {
            if (id == botId) continue;
            if (!_health.IsAlive(id)) continue;
            if (_playerTeams.TryGetValue(id, out int t) && t == myTeam) continue;

            var (tx, tz, _) = _world.GetPlayerState(id);
            float dx = tx - sx;
            float dz = tz - sz;
            float distSq = dx * dx + dz * dz;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                ex = tx;
                ez = tz;
                hasEnemy = true;
            }
        }

        bool hasBox = _boxes.TryGetNearestBox(sx, sz, out float bx, out float bz, out float boxDistSq);

        view = new BotView
        {
            SelfX = sx,
            SelfZ = sz,
            SelfYaw = syaw,
            SelfAlive = _health.IsAlive(botId),
            HasEnemy = hasEnemy,
            EnemyX = ex,
            EnemyZ = ez,
            EnemyDistance = hasEnemy ? System.MathF.Sqrt(bestDistSq) : float.MaxValue,
            HasBox = hasBox,
            BoxX = bx,
            BoxZ = bz,
            BoxDistance = hasBox ? System.MathF.Sqrt(boxDistSq) : float.MaxValue,
        };
        return true;
    }

    /// <summary>Per-team total points (for the end-of-match result / standings).</summary>
    public IReadOnlyDictionary<string, int> GetTeamScores()
    {
        var scores = new Dictionary<string, int>();
        foreach (var kvp in _playerTeams)
        {
            string key = kvp.Value.ToString();
            scores.TryGetValue(key, out var cur);
            scores[key] = cur + _progression.GetPoints(kvp.Key);
        }
        return scores;
    }

    public void Dispose() => _world.Dispose();
}
