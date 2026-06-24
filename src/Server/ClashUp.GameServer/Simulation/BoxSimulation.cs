using System.Text.Json;
using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;

namespace ClashUp.Server.GameServer.Simulation;

/// <summary>
/// Server-authoritative breakable boxes (objective modes). Boxes are static AetherNet bodies authored
/// into the map (<see cref="BoxSpawnDef"/>). Abilities/projectiles damage them; on break they drop
/// point orbs and schedule a respawn at the same spot. Streamed to clients via <see cref="BoxStateDto"/>.
/// </summary>
public sealed class BoxSimulation
{
    public const float BoxHalfExtent = 0.6f;
    private const float BoxMaxHealth = 30f;
    private const int BoxPointValue = 5;   // points worth of orbs dropped on break
    private const int OrbsPerBox = 3;
    private const int RespawnDelayTicks = 300; // 10s at 30 Hz

    private sealed class Box
    {
        public int EntityId;       // also the wire id while alive
        public float X, Z;
        public float Health;
        public int RespawnTimer;   // <0 = alive, >=0 = counting down to respawn
    }

    private readonly PointOrbSimulation _orbs;
    private readonly List<Box> _boxes = new();
    private readonly List<MatchEvent> _pendingEvents = new();

    public BoxSimulation(PointOrbSimulation orbs)
    {
        _orbs = orbs;
    }

    /// <summary>Spawn the initial set of boxes from the map's authored spawn points.</summary>
    public void Initialize(MatchPhysicsWorld world, BoxSpawnDef[] spawns)
    {
        if (spawns == null) return;
        foreach (var s in spawns)
            SpawnBox(world, s.X, s.Z);
    }

    private void SpawnBox(MatchPhysicsWorld world, float x, float z)
    {
        int id = world.SpawnBox(x, z, BoxHalfExtent);
        _boxes.Add(new Box { EntityId = id, X = x, Z = z, Health = BoxMaxHealth, RespawnTimer = -1 });
    }

    public void ApplyDamage(MatchPhysicsWorld world, int entityId, float amount, string breakerId, int currentTick)
    {
        var box = FindAlive(entityId);
        if (box == null) return;

        box.Health -= MathF.Abs(amount);
        if (box.Health > 0f) return;

        // Break: free the body, drop orbs, schedule a respawn at the same spot.
        world.DestroyEntity(box.EntityId);
        _orbs.SpawnBurst(world, box.X, box.Z, BoxPointValue, OrbsPerBox);
        _pendingEvents.Add(new MatchEvent
        {
            Tick = currentTick,
            Kind = "box_broken",
            Payload = JsonSerializer.Serialize(new { id = entityId, x = box.X, z = box.Z, breaker = breakerId }),
        });

        box.Health = 0f;
        box.RespawnTimer = RespawnDelayTicks;
    }

    public void Tick(MatchPhysicsWorld world)
    {
        foreach (var box in _boxes)
        {
            if (box.RespawnTimer < 0) continue;
            if (--box.RespawnTimer > 0) continue;

            box.EntityId = world.SpawnBox(box.X, box.Z, BoxHalfExtent);
            box.Health = BoxMaxHealth;
            box.RespawnTimer = -1;
        }
    }

    public BoxStateDto[] Snapshot()
    {
        int alive = 0;
        foreach (var b in _boxes)
            if (b.Health > 0f) alive++;
        if (alive == 0) return Array.Empty<BoxStateDto>();

        var dtos = new BoxStateDto[alive];
        int i = 0;
        foreach (var b in _boxes)
        {
            if (b.Health <= 0f) continue;
            dtos[i++] = new BoxStateDto
            {
                Id = b.EntityId, X = b.X, Z = b.Z, Health = b.Health, MaxHealth = BoxMaxHealth,
            };
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

    private Box? FindAlive(int entityId)
    {
        foreach (var b in _boxes)
            if (b.EntityId == entityId && b.Health > 0f) return b;
        return null;
    }

    /// <summary>Nearest currently-alive box to a point (for bot box-seeking). False when none are alive.</summary>
    public bool TryGetNearestBox(float fromX, float fromZ, out float x, out float z, out float distSq)
    {
        x = 0f;
        z = 0f;
        distSq = float.MaxValue;
        bool found = false;
        foreach (var b in _boxes)
        {
            if (b.Health <= 0f) continue;
            float dx = b.X - fromX;
            float dz = b.Z - fromZ;
            float d = dx * dx + dz * dz;
            if (d < distSq)
            {
                distSq = d;
                x = b.X;
                z = b.Z;
                found = true;
            }
        }
        return found;
    }
}
