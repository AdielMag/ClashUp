using System;
using System.Collections.Generic;
using AetherNet;
using AetherNet.Collision;
using AetherNet.Queries;
using ClashUp.Shared.Maps;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using Vector2 = System.Numerics.Vector2;
using AVec2 = nkast.Aether.Physics2D.Common.Vector2;

namespace ClashUp.Shared.Simulation
{
    public sealed class MatchPhysicsWorld : IDisposable
    {
        // MUST match the Player prefab's AetherCircleCollider radius (Assets/Core/Gameplay/
        // Art/Prefabs/Player.prefab → _radius). The client reads the radius from that collider;
        // the server can't, so it uses this default. If they differ, client and server resolve
        // wall collisions at different distances → a constant position disagreement → reconciliation
        // shimmer against walls. Keep these two values in sync.
        public const float DefaultPlayerRadius = 0.5f;

        // Orbs live on their own physics layer (bit 5) so they collide with walls (Environment=2)
        // and with each other, but NOT with players — pickup is a proximity query, not a physical
        // shove. PhysicsLayers only predefines 0..4, so we build the orb filter by hand.
        private const int OrbLayer = 5;
        private const ushort OrbCategoryBits = 1 << OrbLayer;
        private const ushort OrbMaskBits = (1 << 2) | (1 << OrbLayer); // Environment + other orbs
        /// <summary>layerMask for <see cref="OverlapCircle"/> to find only loose point orbs.</summary>
        public const int OrbQueryMask = 1 << OrbLayer;

        private readonly PhysicsWorldManager _world;
        private readonly float _playerRadius;
        private readonly Dictionary<string, int> _playerIds = new();
        private readonly Dictionary<int, string> _entityToPlayer = new();
        private readonly Dictionary<string, float> _playerMoveSpeeds = new();
        private readonly Dictionary<string, Vector2> _pendingVel = new();
        private readonly HashSet<int> _boxEntities = new();
        private readonly HashSet<int> _orbEntities = new();
        private readonly Stack<int> _freeIds = new(); // recycled box/orb entity ids
        private readonly PhysicsQueryBuffer _queryBuffer = new();
        private int _nextId;

        public float PlayerRadius => _playerRadius;

        public MatchPhysicsWorld(float playerRadius = DefaultPlayerRadius)
        {
            _playerRadius = playerRadius;
            _world = new PhysicsWorldManager(new WorldConfig
            {
                Gravity = Vector2.Zero,
                AllowSleeping = false,
                // Headroom for players + map geometry + breakable boxes + many loose orbs.
                MaxBodies = 512,
            });
        }

        private int AllocId() => _freeIds.Count > 0 ? _freeIds.Pop() : _nextId++;

        public IEnumerable<string> PlayerIds => _playerIds.Keys;

        public void LoadMapGeometry(MapData map)
        {
            if (map?.Entities == null) return;

            int maxEntityId = _nextId;
            var scratchVertices = new Vertices(64);

            foreach (var entity in map.Entities)
            {
                var bodyType = entity.BodyType switch
                {
                    1 => BodyType.Kinematic,
                    2 => BodyType.Dynamic,
                    _ => BodyType.Static,
                };

                var def = new BodyDef
                {
                    BodyType = bodyType,
                    Position = new Vector2(entity.PositionX, entity.PositionY),
                    Angle = entity.Angle,
                    LinearDamping = entity.LinearDamping,
                    AngularDamping = entity.AngularDamping,
                    GravityScale = entity.GravityScale,
                    FixedRotation = entity.FixedRotation,
                    Constraints = (RigidbodyConstraints)entity.Constraints,
                };

                var body = _world.CreateBody(def, entity.EntityId);

                foreach (var fix in entity.Fixtures)
                {
                    AVec2 offset = new AVec2(fix.OffsetX, fix.OffsetY);
                    Fixture fixture;

                    switch (fix.Shape)
                    {
                        case BakedFixtureShape.Box:
                            fixture = body.CreateRectangle(fix.Width, fix.Height, fix.Density, offset);
                            break;
                        case BakedFixtureShape.Circle:
                            fixture = body.CreateCircle(fix.Radius, fix.Density, offset);
                            break;
                        case BakedFixtureShape.Polygon:
                            scratchVertices.Clear();
                            int count = Math.Min(fix.VerticesX.Length, fix.VerticesY.Length);
                            for (int i = 0; i < count; i++)
                                scratchVertices.Add(new AVec2(fix.VerticesX[i], fix.VerticesY[i]));
                            fixture = body.CreatePolygon(scratchVertices, fix.Density);
                            break;
                        default:
                            continue;
                    }

                    fixture.Friction = fix.Friction;
                    fixture.Restitution = fix.Restitution;
                    fixture.IsSensor = fix.IsSensor;

                    var filter = CollisionFilter.FromLayer(fix.Layer);
                    fixture.CollisionCategories = (Category)filter.CategoryBits;
                    fixture.CollidesWith = (Category)filter.MaskBits;
                    fixture.CollisionGroup = filter.GroupIndex;
                }

                if (entity.EntityId >= maxEntityId)
                    maxEntityId = entity.EntityId + 1;
            }

            _nextId = maxEntityId;
        }

        public void EnsurePlayer(string playerId, float spawnX, float spawnZ, float moveSpeed = MovementModel.MoveSpeed)
        {
            if (_playerIds.ContainsKey(playerId)) return;

            int id = _nextId++;
            var def = new BodyDef
            {
                BodyType = BodyType.Dynamic,
                Position = new Vector2(spawnX, spawnZ),
                FixedRotation = true,
                LinearDamping = 0f,
            };
            var body = _world.CreateBody(def, id);
            body.CreateCircle(_playerRadius, 1f);
            _playerIds[playerId] = id;
            _entityToPlayer[id] = playerId;
            _playerMoveSpeeds[playerId] = moveSpeed;
        }

        public void ApplyInput(string playerId, float moveX, float moveZ)
        {
            _pendingVel[playerId] = new Vector2(moveX, moveZ);
        }

        public void Step(double deltaSeconds)
        {
            foreach (var kvp in _playerIds)
            {
                Vector2 vel = Vector2.Zero;
                if (_pendingVel.TryGetValue(kvp.Key, out var raw))
                {
                    float mag = raw.Length();
                    float speed = _playerMoveSpeeds.TryGetValue(kvp.Key, out var s) ? s : MovementModel.MoveSpeed;
                    vel = (mag > 1f ? raw / mag : raw) * speed;
                }
                _world.SetLinearVelocity(kvp.Value, vel);
            }
            _pendingVel.Clear();
            _world.Advance((float)deltaSeconds);
        }

        public (float x, float z, float yaw) GetPlayerState(string playerId)
        {
            if (!_playerIds.TryGetValue(playerId, out int id)) return default;
            var ts = _world.GetBodyState(id);
            float yaw = 0f;
            if (ts.LinearVelocity.LengthSquared() > 1e-6f)
                yaw = MathF.Atan2(ts.LinearVelocity.X, ts.LinearVelocity.Y) * (180f / MathF.PI);
            return (ts.Position.X, ts.Position.Y, yaw);
        }

        public void SnapPlayerPosition(string playerId, float x, float z)
        {
            if (!_playerIds.TryGetValue(playerId, out int id)) return;
            _world.SetPosition(id, new Vector2(x, z));
            _world.ResetDynamics(id);
        }

        public int OverlapCircle(float centerX, float centerZ, float radius, int[] resultEntityIds, int layerMask = -1)
        {
            _world.OverlapCircle(new Vector2(centerX, centerZ), radius, _queryBuffer, layerMask);
            int count = Math.Min(_queryBuffer.OverlapCount, resultEntityIds.Length);
            for (int i = 0; i < count; i++)
                resultEntityIds[i] = _queryBuffer.OverlapResults[i].EntityId;
            return count;
        }

        public string? GetPlayerByEntityId(int entityId) =>
            _entityToPlayer.TryGetValue(entityId, out var p) ? p : null;

        public int GetEntityIdForPlayer(string playerId) =>
            _playerIds.TryGetValue(playerId, out var id) ? id : -1;

        // ── Server-only objects: breakable boxes + loose point orbs ──────────────
        // Only the server populates these; the client's world only holds the local player.

        /// <summary>Create a static, player-blocking breakable box on the Environment layer.</summary>
        public int SpawnBox(float x, float z, float halfExtent)
        {
            int id = AllocId();
            var def = new BodyDef
            {
                BodyType = BodyType.Static,
                Position = new Vector2(x, z),
                FixedRotation = true,
            };
            var body = _world.CreateBody(def, id);
            var fixture = body.CreateRectangle(halfExtent * 2f, halfExtent * 2f, 1f, new AVec2(0f, 0f));
            var filter = CollisionFilter.FromLayer(2); // Environment — blocks players, hit by abilities
            fixture.CollisionCategories = (Category)filter.CategoryBits;
            fixture.CollidesWith = (Category)filter.MaskBits;
            fixture.CollisionGroup = filter.GroupIndex;
            _boxEntities.Add(id);
            return id;
        }

        /// <summary>
        /// Create a dynamic point orb that scatters (initial velocity), settles (damping), and collides
        /// with walls + other orbs but not players. Pickup is via <see cref="OverlapCircle"/> with
        /// <see cref="OrbQueryMask"/>.
        /// </summary>
        public int SpawnOrb(float x, float z, float velX, float velZ, float radius)
        {
            int id = AllocId();
            var def = new BodyDef
            {
                BodyType = BodyType.Dynamic,
                Position = new Vector2(x, z),
                LinearDamping = 4.5f, // brings the scatter to rest in ~1s
                FixedRotation = true,
            };
            var body = _world.CreateBody(def, id);
            var fixture = body.CreateCircle(radius, 0.4f, new AVec2(0f, 0f));
            fixture.Restitution = 0.25f;
            fixture.Friction = 0.4f;
            fixture.CollisionCategories = (Category)OrbCategoryBits;
            fixture.CollidesWith = (Category)OrbMaskBits;
            fixture.CollisionGroup = 0;
            _orbEntities.Add(id);
            _world.SetLinearVelocity(id, new Vector2(velX, velZ));
            return id;
        }

        public bool IsBoxEntity(int entityId) => _boxEntities.Contains(entityId);
        public bool IsOrbEntity(int entityId) => _orbEntities.Contains(entityId);

        public (float x, float z) GetEntityPosition(int entityId)
        {
            var ts = _world.GetBodyState(entityId);
            return (ts.Position.X, ts.Position.Y);
        }

        /// <summary>Destroy a box/orb body and recycle its entity id for reuse.</summary>
        public void DestroyEntity(int entityId)
        {
            if (!_boxEntities.Remove(entityId) && !_orbEntities.Remove(entityId))
                return;
            _world.DestroyBody(entityId);
            _freeIds.Push(entityId);
        }

        public void Dispose() { }
    }
}
