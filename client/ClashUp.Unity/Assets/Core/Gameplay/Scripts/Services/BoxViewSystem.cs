using System;
using System.Collections.Generic;
using ClashUp.Client.Networking;
using ClashUp.Shared.MessagePackObjects;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Renders server-authoritative breakable boxes (objective modes). Boxes are pure render-from-
    /// snapshot: each world packet's <see cref="WorldStatePacket.Boxes"/> drives spawn/move/despawn,
    /// and the <c>box_broken</c> event plays a small break pop. No prefab wiring — primitives are
    /// generated in code, so the system is a no-op in modes that send no boxes (survival).
    /// </summary>
    public sealed class BoxViewSystem : IStartable, IDisposable
    {
        private const float BoxSize = 1.2f; // 2 * BoxSimulation.BoxHalfExtent

        private readonly ClientPredictionWorld _world;
        private readonly MatchHubReceiver _receiver;
        private readonly Dictionary<int, GameObject> _boxes = new();
        private readonly List<int> _removeScratch = new();
        private readonly HashSet<int> _seen = new();
        private Material _material;

        public BoxViewSystem(ClientPredictionWorld world, MatchHubReceiver receiver)
        {
            _world = world;
            _receiver = receiver;
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
            foreach (var go in _boxes.Values)
                if (go != null) UnityEngine.Object.Destroy(go);
            _boxes.Clear();
            if (_material != null) UnityEngine.Object.Destroy(_material);
        }

        private void OnSnapshot(int tick, WorldStatePacket packet)
        {
            _seen.Clear();
            foreach (var b in packet.Boxes)
            {
                _seen.Add(b.Id);
                if (!_boxes.TryGetValue(b.Id, out var go) || go == null)
                {
                    go = CreateBox();
                    _boxes[b.Id] = go;
                }
                go.transform.position = new Vector3(b.X, BoxSize * 0.5f, b.Z);

                // Tint toward red as the box takes damage.
                var r = go.GetComponent<Renderer>();
                if (r != null && b.MaxHealth > 0f)
                    r.material.color = Color.Lerp(new Color(0.6f, 0.35f, 0.15f), new Color(0.85f, 0.75f, 0.2f), b.Health / b.MaxHealth);
            }

            // Despawn boxes no longer present in the snapshot (broken / removed).
            _removeScratch.Clear();
            foreach (var id in _boxes.Keys)
                if (!_seen.Contains(id)) _removeScratch.Add(id);
            foreach (var id in _removeScratch)
            {
                if (_boxes.TryGetValue(id, out var go) && go != null) UnityEngine.Object.Destroy(go);
                _boxes.Remove(id);
            }
        }

        private void OnMatchEvent(MatchEvent evt)
        {
            if (evt.Kind != "box_broken" || string.IsNullOrEmpty(evt.Payload)) return;
            var obj = JObject.Parse(evt.Payload);
            float x = obj["x"]?.Value<float>() ?? 0f;
            float z = obj["z"]?.Value<float>() ?? 0f;
            BreakPop.Spawn(new Vector3(x, 0.6f, z));
        }

        private GameObject CreateBox()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PointBox";
            go.transform.localScale = Vector3.one * BoxSize;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col); // physics is server-side only
            return go;
        }
    }
}
