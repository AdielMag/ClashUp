using System;
using System.Collections.Generic;
using AetherNet;
using AetherNet.Collision;
using ClashUp.Shared.Maps;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using Vector2 = System.Numerics.Vector2;
using AVec2 = nkast.Aether.Physics2D.Common.Vector2;

namespace ClashUp.Shared.Simulation
{
    public sealed class MatchPhysicsWorld : IDisposable
    {
        public const float DefaultPlayerRadius = 0.4f;

        private readonly PhysicsWorldManager _world;
        private readonly float _playerRadius;
        private readonly Dictionary<string, int> _playerIds = new();
        private readonly Dictionary<string, float> _playerMoveSpeeds = new();
        private readonly Dictionary<string, Vector2> _pendingVel = new();
        private int _nextId;

        public float PlayerRadius => _playerRadius;

        public MatchPhysicsWorld(float playerRadius = DefaultPlayerRadius)
        {
            _playerRadius = playerRadius;
            _world = new PhysicsWorldManager(new WorldConfig
            {
                Gravity = Vector2.Zero,
                AllowSleeping = false,
                MaxBodies = 256,
            });
        }

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

        public void Dispose() { }
    }
}
