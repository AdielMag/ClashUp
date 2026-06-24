using System;
using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Renders server-authoritative loose point orbs (objective modes). Orbs are render-from-snapshot:
    /// each world packet's <see cref="WorldStatePacket.Orbs"/> sets target positions, which Tick lerps
    /// toward (snapshots are 30 Hz; orbs move while scattering). Orbs absent from a snapshot despawn.
    /// No prefab wiring — primitives are generated in code; no-op when no orbs are sent (survival).
    /// </summary>
    public sealed class PointOrbViewSystem : IStartable, ITickable, IDisposable
    {
        private const float OrbVisualScale = 0.5f;
        private const float LerpRate = 14f; // position smoothing toward latest snapshot
        private const float SpinDegPerSec = 120f;
        private const float BobAmplitude = 0.12f;
        private const float BobSpeed = 3f;

        private sealed class OrbView
        {
            public GameObject Go;
            public Vector3 Target;
            public float Phase;
        }

        private readonly ClientPredictionWorld _world;
        private readonly Dictionary<int, OrbView> _orbs = new();
        private readonly List<int> _removeScratch = new();
        private readonly HashSet<int> _seen = new();

        public PointOrbViewSystem(ClientPredictionWorld world)
        {
            _world = world;
        }

        public void Start() => _world.SnapshotDecoded += OnSnapshot;

        public void Dispose()
        {
            _world.SnapshotDecoded -= OnSnapshot;
            foreach (var o in _orbs.Values)
                if (o.Go != null) UnityEngine.Object.Destroy(o.Go);
            _orbs.Clear();
        }

        private void OnSnapshot(int tick, WorldStatePacket packet)
        {
            _seen.Clear();
            foreach (var o in packet.Orbs)
            {
                _seen.Add(o.Id);
                if (!_orbs.TryGetValue(o.Id, out var view) || view.Go == null)
                {
                    view = new OrbView { Go = CreateOrb(), Phase = (o.Id * 0.7f) % (Mathf.PI * 2f) };
                    view.Go.transform.position = new Vector3(o.X, 0.4f, o.Z);
                    _orbs[o.Id] = view;
                }
                view.Target = new Vector3(o.X, 0.4f, o.Z);
            }

            // Despawn orbs no longer present (collected / removed).
            _removeScratch.Clear();
            foreach (var id in _orbs.Keys)
                if (!_seen.Contains(id)) _removeScratch.Add(id);
            foreach (var id in _removeScratch)
            {
                if (_orbs.TryGetValue(id, out var view) && view.Go != null) UnityEngine.Object.Destroy(view.Go);
                _orbs.Remove(id);
            }
        }

        public void Tick()
        {
            if (_orbs.Count == 0) return;
            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-LerpRate * dt);

            foreach (var o in _orbs.Values)
            {
                if (o.Go == null) continue;
                o.Phase += dt * BobSpeed;
                var pos = Vector3.Lerp(o.Go.transform.position, o.Target, k);
                pos.y = o.Target.y + Mathf.Sin(o.Phase) * BobAmplitude;
                o.Go.transform.position = pos;
                o.Go.transform.Rotate(Vector3.up, SpinDegPerSec * dt, Space.World);
            }
        }

        private static GameObject CreateOrb()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "PointOrb";
            go.transform.localScale = Vector3.one * OrbVisualScale;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col); // physics is server-side only
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(1f, 0.85f, 0.1f, 1f); // gold
            return go;
        }
    }
}
