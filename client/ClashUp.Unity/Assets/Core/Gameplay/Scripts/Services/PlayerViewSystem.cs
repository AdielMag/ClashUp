using System;
using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class PlayerViewSystem : ITickable, IDisposable
    {
        private readonly IClientSimulation _sim;
        private readonly GameObject _playerPrefab;
        private readonly PlayerMaterialMap _materialMap;
        private readonly Dictionary<string, GameObject> _capsules = new();
        private readonly Dictionary<string, int> _colorSlots = new();

        public Transform LocalPlayerTransform { get; private set; }

        public PlayerViewSystem(IClientSimulation sim, GameObject playerPrefab, PlayerMaterialMap materialMap)
        {
            _sim = sim;
            _playerPrefab = playerPrefab;
            _materialMap = materialMap;
        }

        public void RegisterPlayer(PlayerSummary player)
        {
            _colorSlots[player.Id.Value] = player.ColorSlot;

            if (_capsules.TryGetValue(player.Id.Value, out var go) && go != null)
                go.GetComponent<Renderer>().material = _materialMap.Get(player.ColorSlot);
        }

        public void UnregisterPlayer(PlayerId id)
        {
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
                    go = SpawnPlayer(id);
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

        private GameObject SpawnPlayer(string playerId)
        {
            var go = UnityEngine.Object.Instantiate(_playerPrefab);
            go.name = $"Player_{playerId[..Math.Min(6, playerId.Length)]}";
            go.transform.position = new Vector3(0f, 1f, 0f);

            var slot = _colorSlots.TryGetValue(playerId, out var s) ? s : 0;
            go.GetComponent<Renderer>().material = _materialMap.Get(slot);

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
