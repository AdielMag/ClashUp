using System;
using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class PlayerViewSystem : ITickable, IDisposable
    {
        private static readonly Color[] Palette =
        {
            new(0.2f, 0.6f, 1.0f),
            new(1.0f, 0.3f, 0.3f),
            new(0.3f, 1.0f, 0.3f),
            new(1.0f, 0.85f, 0.2f),
            new(0.8f, 0.3f, 1.0f),
            new(1.0f, 0.55f, 0.1f),
            new(0.1f, 0.9f, 0.9f),
            new(0.9f, 0.4f, 0.7f),
        };

        private readonly MovementClientSimulation _sim;
        private readonly Dictionary<string, GameObject> _capsules = new();
        private readonly Dictionary<string, int> _colorSlots = new();

        public Transform LocalPlayerTransform { get; private set; }

        public PlayerViewSystem(MovementClientSimulation sim)
        {
            _sim = sim;
        }

        public void RegisterPlayer(PlayerSummary player)
        {
            _colorSlots[player.Id.Value] = player.ColorSlot;
        }

        public void UnregisterPlayer(PlayerId id)
        {
            _colorSlots.Remove(id.Value);
            if (_capsules.Remove(id.Value, out var go))
                UnityEngine.Object.Destroy(go);
            if (LocalPlayerTransform != null && id.Equals(_sim.LocalId))
                LocalPlayerTransform = null;
        }

        public void Tick()
        {
            foreach (var kvp in _sim.Players)
            {
                var id = kvp.Key;
                var state = kvp.Value;

                if (!_capsules.TryGetValue(id, out var go))
                {
                    go = SpawnCapsule(id);
                    _capsules[id] = go;

                    if (state.Id.Equals(_sim.LocalId))
                        LocalPlayerTransform = go.transform;
                }

                var t = go.transform;
                var targetPos = new Vector3(state.X, 1f, state.Z);
                var targetRot = Quaternion.Euler(0f, state.Yaw, 0f);

                if (state.Id.Equals(_sim.LocalId))
                {
                    t.position = targetPos;
                    t.rotation = targetRot;
                }
                else
                {
                    t.position = Vector3.Lerp(t.position, targetPos, 15f * Time.deltaTime);
                    t.rotation = Quaternion.Slerp(t.rotation, targetRot, 15f * Time.deltaTime);
                }
            }
        }

        private GameObject SpawnCapsule(string playerId)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Player_{playerId[..Math.Min(6, playerId.Length)]}";
            go.transform.position = new Vector3(0f, 1f, 0f);

            var slot = _colorSlots.TryGetValue(playerId, out var s) ? s : 0;
            var color = Palette[slot % Palette.Length];
            go.GetComponent<Renderer>().material.color = color;

            return go;
        }

        public void Dispose()
        {
            foreach (var go in _capsules.Values)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            _capsules.Clear();
        }
    }
}
