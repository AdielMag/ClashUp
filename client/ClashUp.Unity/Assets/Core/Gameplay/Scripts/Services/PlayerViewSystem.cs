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
        private readonly MatchModeHolder _mode;
        private readonly Dictionary<string, GameObject> _views = new();
        private readonly Dictionary<string, WorldSpaceHealthBar> _healthBars = new();
        private readonly Dictionary<string, CharacterId> _characterIds = new();
        private readonly Dictionary<string, string> _displayNames = new();
        private readonly Dictionary<string, bool> _isBot = new();
        private readonly Dictionary<string, int> _points = new();
        private readonly Dictionary<string, TMP_Text> _pointsLabels = new();

        // Nameplate tint for AI bots, so they read as distinct from human players at a glance.
        private static readonly Color BotNameColor = new(1f, 0.55f, 0.2f); // orange
        // Per-player point counter color (matches the elimination HUD's gold).
        private static readonly Color PointsColor = new(1f, 0.85f, 0.1f);
        private readonly Dictionary<string, float> _serverMaxHealth = new();
        private readonly HashSet<string> _eliminated = new();
        private Vector3 _lastRenderedPos;

        public Transform LocalPlayerTransform { get; private set; }
        public CharacterId LocalPlayerCharacterId { get; private set; }

        public PlayerViewSystem(
            IClientSimulation sim,
            RemotePlayerInterpolator interpolator,
            ClientPredictionWorld prediction,
            GameObject playerPrefab,
            CharacterPrefabMap characterMap,
            MatchCharactersHolder characters,
            MatchModeHolder mode)
        {
            _sim = sim;
            _interpolator = interpolator;
            _prediction = prediction;
            _playerPrefab = playerPrefab;
            _characterMap = characterMap;
            _characters = characters;
            _mode = mode;
            _prediction.SnapshotDecoded += OnSnapshotDecoded;
        }

        // Server-authoritative max health (grows with points) + elimination state, read from snapshots.
        private void OnSnapshotDecoded(int tick, WorldStatePacket packet)
        {
            foreach (var dto in packet.Players)
            {
                if (dto.MaxHealth > 0f) _serverMaxHealth[dto.Id.Value] = dto.MaxHealth;
                if (dto.IsEliminated) _eliminated.Add(dto.Id.Value);
                else _eliminated.Remove(dto.Id.Value);
                _points[dto.Id.Value] = dto.Points;
            }
        }

        private float ServerMax(string id, float fallback) =>
            _serverMaxHealth.TryGetValue(id, out var m) && m > 0f ? m : fallback;

        public void RegisterPlayer(PlayerSummary player)
        {
            _characterIds[player.Id.Value] = player.CharacterId;
            _displayNames[player.Id.Value] = player.DisplayName;
            _isBot[player.Id.Value] = player.IsBot;
        }

        public void UnregisterPlayer(PlayerId id)
        {
            if (_views.Remove(id.Value, out var go))
                UnityEngine.Object.Destroy(go);
            _healthBars.Remove(id.Value);
            _pointsLabels.Remove(id.Value); // GameObject destroyed with the player view above
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
                bool isDead = local.RespawnInTicks > 0 || _eliminated.Contains(localId);
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
                        localHb.SetHealth(local.Health, ServerMax(localId, local.MaxHealth));

                    UpdatePoints(localId);
                }
            }

            var remoteIds = _interpolator.PlayerIds;
            for (int i = 0; i < remoteIds.Count; i++)
            {
                var id = remoteIds[i];
                if (!_interpolator.TryGet(id, out var pos, out var yaw, out var health, out var respawnInTicks)) continue;
                var go = GetOrSpawn(id, isLocal: false);
                bool isDead = respawnInTicks > 0 || _eliminated.Contains(id);
                go.SetActive(!isDead);

                if (!isDead)
                {
                    go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

                    if (_healthBars.TryGetValue(id, out var remoteHb))
                    {
                        var maxHealth = ServerMax(id, GetMaxHealth(id));
                        remoteHb.SetHealth(health, maxHealth);
                    }

                    UpdatePoints(id);
                }
            }
        }

        private void UpdatePoints(string playerId)
        {
            if (_pointsLabels.TryGetValue(playerId, out var label) && label != null)
                label.text = (_points.TryGetValue(playerId, out var p) ? p : 0).ToString();
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
                // Bots get a distinct nameplate color so they're obvious at a glance.
                if (_isBot.TryGetValue(playerId, out var bot) && bot)
                    label.color = BotNameColor;

                // Point counter above the nameplate — only meaningful in objective (point) modes.
                if (_mode.IsElimination)
                    _pointsLabels[playerId] = CreatePointsLabel(label, playerId);
            }

            var healthBar = go.GetComponentInChildren<WorldSpaceHealthBar>();
            if (healthBar != null)
                _healthBars[playerId] = healthBar;

            return go;
        }

        // Builds a gold point counter just above the nameplate, reusing the nameplate's font/canvas.
        private TMP_Text CreatePointsLabel(TextMeshProUGUI nameLabel, string playerId)
        {
            var go = new GameObject("PointsLabel");
            go.transform.SetParent(nameLabel.transform.parent, false); // the player's world-space canvas
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 40f); // above the name (health bar sits at -40)
            rect.sizeDelta = new Vector2(240f, 45f);

            var pts = go.AddComponent<TextMeshProUGUI>();
            pts.font = nameLabel.font;
            pts.fontSharedMaterial = nameLabel.fontSharedMaterial;
            pts.fontSize = nameLabel.fontSize;
            pts.fontStyle = FontStyles.Bold;
            pts.alignment = TextAlignmentOptions.Center;
            pts.color = PointsColor;
            pts.text = (_points.TryGetValue(playerId, out var p) ? p : 0).ToString();
            return pts;
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
            _prediction.SnapshotDecoded -= OnSnapshotDecoded;
            foreach (var go in _views.Values)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            _views.Clear();
        }
    }
}
