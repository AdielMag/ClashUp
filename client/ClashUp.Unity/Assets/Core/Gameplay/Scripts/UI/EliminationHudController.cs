using System;
using System.Collections.Generic;
using ClashUp.Client.Networking;
using ClashUp.Shared.MessagePackObjects;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Mode-specific HUD for the elimination objective: local point counter, players-remaining, and a
    /// short elimination feed. Built lazily on the first snapshot once the mode is known (so it's a
    /// no-op in survival). Driven entirely by server snapshots + match events — no client logic.
    /// </summary>
    public sealed class EliminationHudController : IStartable, ITickable, IDisposable
    {
        private const int FeedLines = 4;
        private const float FeedLineSeconds = 5f;

        private readonly AetherClientSimulation _sim;
        private readonly ClientPredictionWorld _world;
        private readonly MatchHubReceiver _receiver;
        private readonly MatchModeHolder _mode;

        private GameObject _root;
        private TMP_Text _pointsLabel;
        private TMP_Text _remainingLabel;
        private TMP_Text _feedLabel;
        private readonly Queue<(string text, float expiry)> _feed = new();
        private bool _built;

        public EliminationHudController(
            AetherClientSimulation sim,
            ClientPredictionWorld world,
            MatchHubReceiver receiver,
            MatchModeHolder mode)
        {
            _sim = sim;
            _world = world;
            _receiver = receiver;
            _mode = mode;
        }

        public void Start()
        {
            _world.SnapshotDecoded += OnSnapshot;
            _receiver.MatchEventOccurred += OnMatchEvent;
        }

        public void Dispose()
        {
            _world.SnapshotDecoded -= OnSnapshot;
            _receiver.MatchEventOccurred -= OnMatchEvent;
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }

        private void OnSnapshot(int tick, WorldStatePacket packet)
        {
            if (!_mode.IsElimination) return;
            if (!_built) Build();

            var localId = _sim.LocalId;
            int remaining = 0;
            foreach (var p in packet.Players)
            {
                if (!p.IsEliminated) remaining++;
                if (p.Id.Equals(localId))
                    _pointsLabel.text = $"POINTS  {p.Points}";
            }
            _remainingLabel.text = $"Players left: {remaining}";
        }

        private void OnMatchEvent(MatchEvent evt)
        {
            if (!_mode.IsElimination || evt.Kind != "player_eliminated" || string.IsNullOrEmpty(evt.Payload)) return;
            var obj = JObject.Parse(evt.Payload);
            string id = obj["playerId"]?.Value<string>() ?? "?";
            string shortId = id.Length > 6 ? id.Substring(0, 6) : id;
            PushFeed($"{shortId} was eliminated");
        }

        private void PushFeed(string line)
        {
            _feed.Enqueue((line, Time.time + FeedLineSeconds));
            while (_feed.Count > FeedLines) _feed.Dequeue();
        }

        public void Tick()
        {
            if (!_built || _feedLabel == null) return;

            // Drop expired lines, then render the remaining ones.
            bool changed = false;
            while (_feed.Count > 0 && _feed.Peek().expiry <= Time.time) { _feed.Dequeue(); changed = true; }

            if (changed || _feed.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var (text, _) in _feed) sb.AppendLine(text);
                _feedLabel.text = sb.ToString();
            }
        }

        private void Build()
        {
            _built = true;
            _root = new GameObject("EliminationHud");
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();

            _pointsLabel = MakeLabel("PointsLabel", new Vector2(0f, 1f), new Vector2(40f, -40f),
                new Vector2(360f, 70f), TextAlignmentOptions.TopLeft, 44, new Color(1f, 0.85f, 0.1f));
            _pointsLabel.fontStyle = FontStyles.Bold;
            _pointsLabel.text = "POINTS  0";

            _remainingLabel = MakeLabel("RemainingLabel", new Vector2(0f, 1f), new Vector2(40f, -110f),
                new Vector2(360f, 44f), TextAlignmentOptions.TopLeft, 26, Color.white);
            _remainingLabel.text = "Players left: -";

            _feedLabel = MakeLabel("FeedLabel", new Vector2(1f, 1f), new Vector2(-40f, -120f),
                new Vector2(440f, 200f), TextAlignmentOptions.TopRight, 24, new Color(1f, 0.6f, 0.6f));
            _feedLabel.text = string.Empty;
        }

        private TMP_Text MakeLabel(string name, Vector2 anchor, Vector2 anchoredPos, Vector2 size,
            TextAlignmentOptions align, float fontSize, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(_root.transform, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var label = obj.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.alignment = align;
            label.color = color;
            return label;
        }
    }
}
