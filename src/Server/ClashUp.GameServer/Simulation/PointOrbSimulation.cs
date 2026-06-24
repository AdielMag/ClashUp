using System.Text.Json;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;

namespace ClashUp.Server.GameServer.Simulation;

/// <summary>
/// Server-authoritative loose point orbs. Orbs are real AetherNet dynamic bodies (so they scatter,
/// collide with walls + each other, and never overlap). Players pick them up by proximity. Orbs are
/// streamed to clients inside the world snapshot (<see cref="OrbStateDto"/>); the client only renders.
/// </summary>
public sealed class PointOrbSimulation
{
    public const float OrbRadius = 0.25f;
    private const float PickupRadius = 1.0f;
    private const int ArmTicks = 12;             // ~0.4s before an orb can be collected
    private const int MaxDropOrbs = 12;          // caps body usage on a big death drop
    private const float ScatterMinSpeed = 2.5f;
    private const float ScatterMaxSpeed = 6.0f;

    private sealed class Orb
    {
        public int EntityId;
        public int Value;
        public int Age;
    }

    private readonly List<Orb> _orbs = new();
    private readonly List<MatchEvent> _pendingEvents = new();
    private readonly DeterministicRng _rng;
    private readonly int[] _overlapScratch = new int[64];

    public PointOrbSimulation(uint seed)
    {
        _rng = new DeterministicRng(seed == 0 ? 0x9E3779B9u : seed);
    }

    /// <summary>Drop a fixed number of orbs splitting <paramref name="totalValue"/> between them.</summary>
    public void SpawnBurst(MatchPhysicsWorld world, float x, float z, int totalValue, int count)
    {
        if (totalValue <= 0) return;
        count = Math.Clamp(count, 1, MaxDropOrbs);
        int per = (totalValue + count - 1) / count; // ceil
        int remaining = totalValue;

        for (int i = 0; i < count && remaining > 0; i++)
        {
            int value = Math.Min(per, remaining);
            remaining -= value;

            float angle = _rng.NextRange(0f, MathF.PI * 2f);
            float speed = _rng.NextRange(ScatterMinSpeed, ScatterMaxSpeed);
            float vx = MathF.Sin(angle) * speed;
            float vz = MathF.Cos(angle) * speed;

            int id = world.SpawnOrb(x, z, vx, vz, OrbRadius);
            _orbs.Add(new Orb { EntityId = id, Value = value, Age = 0 });
        }
    }

    /// <summary>Death drop — one orb per point up to the cap, scattered from the death position.</summary>
    public void DropFromPlayer(MatchPhysicsWorld world, float x, float z, int totalPoints)
    {
        if (totalPoints <= 0) return;
        SpawnBurst(world, x, z, totalPoints, Math.Min(totalPoints, MaxDropOrbs));
    }

    public void Tick(MatchPhysicsWorld world, HealthTable health, PlayerProgression progression, int currentTick)
    {
        for (int i = 0; i < _orbs.Count; i++)
            _orbs[i].Age++;

        foreach (var playerId in world.PlayerIds)
        {
            if (!health.IsAlive(playerId)) continue;

            var (px, pz, _) = world.GetPlayerState(playerId);
            int n = world.OverlapCircle(px, pz, PickupRadius, _overlapScratch, MatchPhysicsWorld.OrbQueryMask);
            for (int k = 0; k < n; k++)
            {
                int entityId = _overlapScratch[k];
                int idx = IndexOf(entityId);
                if (idx < 0) continue;
                var orb = _orbs[idx];
                if (orb.Age < ArmTicks) continue;

                progression.Add(playerId, orb.Value);
                _pendingEvents.Add(new MatchEvent
                {
                    Tick = currentTick,
                    Kind = "points_collected",
                    Payload = JsonSerializer.Serialize(new
                    {
                        playerId,
                        points = orb.Value,
                        total = progression.GetPoints(playerId),
                    }),
                });

                world.DestroyEntity(entityId);
                _orbs.RemoveAt(idx);
            }
        }
    }

    public OrbStateDto[] Snapshot(MatchPhysicsWorld world)
    {
        if (_orbs.Count == 0) return Array.Empty<OrbStateDto>();
        var dtos = new OrbStateDto[_orbs.Count];
        for (int i = 0; i < _orbs.Count; i++)
        {
            var orb = _orbs[i];
            var (x, z) = world.GetEntityPosition(orb.EntityId);
            dtos[i] = new OrbStateDto { Id = orb.EntityId, X = x, Z = z, Value = orb.Value };
        }
        return dtos;
    }

    public IReadOnlyList<MatchEvent> DrainEvents()
    {
        if (_pendingEvents.Count == 0) return Array.Empty<MatchEvent>();
        var events = new List<MatchEvent>(_pendingEvents);
        _pendingEvents.Clear();
        return events;
    }

    private int IndexOf(int entityId)
    {
        for (int i = 0; i < _orbs.Count; i++)
            if (_orbs[i].EntityId == entityId) return i;
        return -1;
    }
}
