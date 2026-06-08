using System;
using System.Collections.Generic;
using System.Numerics;
using AetherNet;
using nkast.Aether.Physics2D.Dynamics;

namespace ClashUp.Shared.Simulation
{
    /// <summary>
    /// Deterministic 2D physics world for one match, shared by server and client.
    /// Coordinate mapping: game (X, Z) ↔ Aether (x, y).  Gravity is zero (top-down view).
    /// Players are dynamic circle bodies; velocity is set from input each tick so collision
    /// resolution (collide-and-slide) happens naturally via Box2D.
    /// </summary>
    public sealed class MatchPhysicsWorld : IDisposable
    {
        /// <summary>Fallback radius when none is provided.</summary>
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
                MaxBodies = 64,
            });
        }

        public IEnumerable<string> PlayerIds => _playerIds.Keys;

        public void EnsurePlayer(string playerId, int colorSlot, float moveSpeed = MovementModel.MoveSpeed)
        {
            if (_playerIds.ContainsKey(playerId)) return;

            int id = _nextId++;
            var def = new BodyDef
            {
                BodyType = BodyType.Dynamic,
                Position = new Vector2(colorSlot * MovementModel.SpawnSpacing, 0f),
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

        /// <summary>
        /// Applies pending velocities and advances physics by <paramref name="deltaSeconds"/>.
        /// Pending velocities are cleared after each step — callers must call ApplyInput again next tick.
        /// </summary>
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

        /// <summary>Returns (x, z, yaw) for the given player. Yaw is derived from velocity direction.</summary>
        public (float x, float z, float yaw) GetPlayerState(string playerId)
        {
            if (!_playerIds.TryGetValue(playerId, out int id)) return default;
            var ts = _world.GetBodyState(id);
            float yaw = 0f;
            if (ts.LinearVelocity.LengthSquared() > 1e-6f)
                yaw = MathF.Atan2(ts.LinearVelocity.X, ts.LinearVelocity.Y) * (180f / MathF.PI);
            return (ts.Position.X, ts.Position.Y, yaw);
        }

        /// <summary>
        /// Teleports the player to (x, z) and zeros velocity. Used for server-authoritative reconciliation.
        /// </summary>
        public void SnapPlayerPosition(string playerId, float x, float z)
        {
            if (!_playerIds.TryGetValue(playerId, out int id)) return;
            _world.SetPosition(id, new Vector2(x, z));
            _world.ResetDynamics(id);
        }

        public void Dispose() { }
    }
}
