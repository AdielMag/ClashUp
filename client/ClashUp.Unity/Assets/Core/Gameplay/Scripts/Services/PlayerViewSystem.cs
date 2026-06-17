using System;
using System.Collections.Generic;
using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;
using TMPro;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class PlayerViewSystem : ITickable, IDisposable
    {
        private readonly IClientSimulation _sim;
        private readonly RemotePlayerInterpolator _interpolator;
        private readonly ClientPredictionWorld _prediction;
        private readonly GameObject _playerPrefab;
        private readonly CharacterPrefabMap _characterMap;
        private readonly MatchCharactersHolder _characters;
        private readonly Dictionary<string, GameObject> _views = new();
        private readonly Dictionary<string, WorldSpaceHealthBar> _healthBars = new();
        private readonly Dictionary<string, CharacterId> _characterIds = new();
        private readonly Dictionary<string, string> _displayNames = new();
        private Vector3 _lastRenderedPos;

        public Transform LocalPlayerTransform { get; private set; }
        public CharacterId LocalPlayerCharacterId { get; private set; }

        public PlayerViewSystem(
            IClientSimulation sim,
            RemotePlayerInterpolator interpolator,
            ClientPredictionWorld prediction,
            GameObject playerPrefab,
            CharacterPrefabMap characterMap,
            MatchCharactersHolder characters)
        {
            _sim = sim;
            _interpolator = interpolator;
            _prediction = prediction;
            _playerPrefab = playerPrefab;
            _characterMap = characterMap;
            _characters = characters;
        }

        public void RegisterPlayer(PlayerSummary player)
        {
            _characterIds[player.Id.Value] = player.CharacterId;
            _displayNames[player.Id.Value] = player.DisplayName;
        }

        public void UnregisterPlayer(PlayerId id)
        {
            if (_views.Remove(id.Value, out var go))
                UnityEngine.Object.Destroy(go);
            _healthBars.Remove(id.Value);
            _interpolator.Remove(id.Value);
            if (LocalPlayerTransform != null && id.Equals(_sim.LocalId))
                LocalPlayerTransform = null;
        }

        public bool TryGetPosition(string playerId, out Vector3 pos)
        {
            if (_views.TryGetValue(playerId, out var go) && go != null)
            {
                pos = go.transform.position;
                return true;
            }
            pos = default;
            return false;
        }

        public void Tick()
        {
            _interpolator.Advance(Time.deltaTime * 1000.0);
            _prediction.DecayCorrection(Time.deltaTime);

            var localId = _sim.LocalId.Value;
            if (localId != null && _sim.Players.TryGetValue(localId, out var local))
            {
                var go = GetOrSpawn(localId, isLocal: true);
                bool isDead = local.RespawnInTicks > 0;
                go.SetActive(!isDead);

                if (!isDead)
                {
                    float alpha = _prediction.RenderAlpha;
                    var pos = new Vector3(
                        Mathf.Lerp(local.PrevX, local.X, alpha) + _prediction.CorrectionX,
                        1f,
                        Mathf.Lerp(local.PrevZ, local.Z, alpha) + _prediction.CorrectionZ);
                    var rot = Quaternion.Euler(0f, Mathf.LerpAngle(local.PrevYaw, local.Yaw, alpha), 0f);
                    go.transform.SetPositionAndRotation(pos, rot);
                    _lastRenderedPos = pos;

                    if (_healthBars.TryGetValue(localId, out var localHb))
                        localHb.SetHealth(local.Health, local.MaxHealth);
                }
            }

            var remoteIds = _interpolator.PlayerIds;
            for (int i = 0; i < remoteIds.Count; i++)
            {
                var id = remoteIds[i];
                if (!_interpolator.TryGet(id, out var pos, out var yaw, out var health, out var respawnInTicks)) continue;
                var go = GetOrSpawn(id, isLocal: false);
                bool isDead = respawnInTicks > 0;
                go.SetActive(!isDead);

                if (!isDead)
                {
                    go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

                    if (_healthBars.TryGetValue(id, out var remoteHb))
                    {
                        var maxHealth = GetMaxHealth(id);
                        remoteHb.SetHealth(health, maxHealth);
                    }
                }
            }
        }

        private GameObject GetOrSpawn(string playerId, bool isLocal)
        {
            if (_views.TryGetValue(playerId, out var go) && go != null)
                return go;

            go = SpawnPlayer(playerId);
            _views[playerId] = go;
            if (isLocal)
            {
                LocalPlayerTransform = go.transform;
                LocalPlayerCharacterId = _characterIds.TryGetValue(playerId, out var cid)
                    ? cid
                    : _characters.Catalog.DefaultId;
            }
            return go;
        }

        private GameObject SpawnPlayer(string playerId)
        {
            var go = UnityEngine.Object.Instantiate(_playerPrefab);
            go.name = $"Player_{playerId[..Math.Min(6, playerId.Length)]}";
            go.transform.position = new Vector3(0f, 1f, 0f);

            var characterId = _characterIds.TryGetValue(playerId, out var cid)
                ? cid
                : _characters.Catalog.DefaultId;
            var charPrefab = _characterMap.Get(characterId);
            var charGo = UnityEngine.Object.Instantiate(charPrefab, go.transform);
            charGo.transform.localPosition = Vector3.zero;
            charGo.transform.localRotation = Quaternion.identity;

            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                var displayName = _displayNames.TryGetValue(playerId, out var n)
                    ? n
                    : playerId[..Math.Min(6, playerId.Length)];
                label.text = displayName;
            }

            var healthBar = go.GetComponentInChildren<WorldSpaceHealthBar>();
            if (healthBar != null)
                _healthBars[playerId] = healthBar;

            return go;
        }

        private float GetMaxHealth(string playerId)
        {
            var characterId = _characterIds.TryGetValue(playerId, out var cid)
                ? cid
                : _characters.Catalog.DefaultId;
            return _characters.Catalog.Get(characterId).BaseStats.MaxHealth;
        }

        public void Dispose()
        {
            foreach (var go in _views.Values)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            _views.Clear();
        }
    }
}
