using System;
using System.Collections.Generic;
using ClashUp.Client.Networking;
using ClashUp.Shared.Abilities;
using ClashUp.Shared.MessagePackObjects;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Renders server-authoritative projectiles as cosmetic dead-reckoned visuals.
    /// On <c>projectile_spawn</c> it instantiates a prefab and flies it along the deterministic path
    /// (origin + yaw + speed). On <c>projectile_destroy</c> it removes the visual and spawns the
    /// impact/explosion effect at the authoritative impact point. The visual self-despawns at
    /// MaxRange so a missed (fire-and-forget) destroy event never leaves a projectile flying forever.
    /// </summary>
    public sealed class ProjectileViewSystem : IStartable, ITickable, IDisposable
    {
        private sealed class ProjectileView
        {
            public GameObject Go;
            public Vector3 Dir;
            public float Speed;
            public float MaxRange;
            public float Traveled;
            public float Age;
            public string AbilityId;
        }

        // Safety-net lifetime: if a projectile_destroy event is missed, despawn the visual anyway.
        private const float MaxVisualSeconds = 4f;

        private readonly MatchHubReceiver _receiver;
        private readonly AbilityVisualRegistry _registry;
        private readonly Dictionary<int, ProjectileView> _projectiles = new();
        private readonly List<int> _expiredScratch = new();

        public ProjectileViewSystem(MatchHubReceiver receiver, AbilityVisualRegistry registry)
        {
            _receiver = receiver;
            _registry = registry;
        }

        public void Start()
        {
            _receiver.MatchEventOccurred += OnMatchEvent;
        }

        public void Dispose()
        {
            _receiver.MatchEventOccurred -= OnMatchEvent;
            foreach (var p in _projectiles.Values)
                if (p.Go != null) UnityEngine.Object.Destroy(p.Go);
            _projectiles.Clear();
        }

        private void OnMatchEvent(MatchEvent evt)
        {
            switch (evt.Kind)
            {
                case "projectile_spawn": HandleSpawn(evt.Payload); break;
                case "projectile_destroy": HandleDestroy(evt.Payload); break;
            }
        }

        private void HandleSpawn(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;
            var obj = JObject.Parse(payload);
            int id = obj["id"]?.Value<int>() ?? 0;
            if (id == 0 || _projectiles.ContainsKey(id)) return;

            string abilityId = obj["abilityId"]?.Value<string>();
            float x = obj["x"]?.Value<float>() ?? 0f;
            float z = obj["z"]?.Value<float>() ?? 0f;
            float yaw = obj["yaw"]?.Value<float>() ?? 0f;
            float speed = obj["speed"]?.Value<float>() ?? 0f;
            float maxRange = obj["maxRange"]?.Value<float>() ?? 0f;

            float yawRad = yaw * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            var spawnPos = new Vector3(x, 1f, z);

            var visual = _registry?.GetByAbilityId(abilityId);
            GameObject go = visual?.ProjectilePrefab != null
                ? UnityEngine.Object.Instantiate(visual.ProjectilePrefab, spawnPos, Quaternion.LookRotation(dir))
                : CreateFallbackProjectile(spawnPos);

            _projectiles[id] = new ProjectileView
            {
                Go = go,
                Dir = dir,
                Speed = speed,
                MaxRange = maxRange,
                Traveled = 0f,
                AbilityId = abilityId,
            };
        }

        private void HandleDestroy(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;
            var obj = JObject.Parse(payload);
            int id = obj["id"]?.Value<int>() ?? 0;
            float x = obj["x"]?.Value<float>() ?? 0f;
            float z = obj["z"]?.Value<float>() ?? 0f;
            float aoeRadius = obj["aoeRadius"]?.Value<float>() ?? 0f;

            // The visual may have already self-despawned at MaxRange; spawn the impact regardless so
            // the explosion always plays at the authoritative point.
            string abilityId = null;
            if (_projectiles.TryGetValue(id, out var p))
            {
                abilityId = p.AbilityId;
                if (p.Go != null) UnityEngine.Object.Destroy(p.Go);
                _projectiles.Remove(id);
            }

            SpawnImpact(abilityId, new Vector3(x, 0f, z), aoeRadius);
        }

        private void SpawnImpact(string abilityId, Vector3 impact, float aoeRadius)
        {
            var visual = abilityId != null ? _registry?.GetByAbilityId(abilityId) : null;

            if (visual?.HitVfxPrefab != null)
                UnityEngine.Object.Instantiate(visual.HitVfxPrefab, impact + Vector3.up * 0.5f, Quaternion.identity);

            if (aoeRadius > 0f)
            {
                Color color = visual != null && visual.CastFlashColor.a > 0f
                    ? visual.CastFlashColor
                    : new Color(0.3f, 0.6f, 1f, 0.6f);
                float duration = visual != null && visual.CastFlashDuration > 0f ? visual.CastFlashDuration : 0.3f;
                var footprint = new TelegraphConfig { Shape = TelegraphShape.TargetCircle, Radius = aoeRadius };
                var origin = impact;
                origin.y = 0.03f;
                AbilityAreaFlash.Spawn(footprint, origin, 0f, color, duration);
            }

            if (visual?.HitSound != null)
                AudioSource.PlayClipAtPoint(visual.HitSound, impact);
        }

        private static GameObject CreateFallbackProjectile(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.4f;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            var renderer = go.GetComponent<Renderer>();
            renderer.material.color = new Color(0.3f, 0.6f, 1f, 1f);
            return go;
        }

        public void Tick()
        {
            if (_projectiles.Count == 0) return;

            float dt = Time.deltaTime;
            _expiredScratch.Clear();

            foreach (var kvp in _projectiles)
            {
                var p = kvp.Value;
                float step = p.Speed * dt;
                p.Traveled += step;
                p.Age += dt;
                if (p.Go != null)
                    p.Go.transform.position += p.Dir * step;

                // Self-despawn safety net: projectile_destroy carries the authoritative impact +
                // explosion, but it's fire-and-forget and may be missed. Travelling projectiles cap
                // at MaxRange; detonate-at-origin ones (MaxRange 0) wait for the destroy event but
                // still time out so a missed event never leaks a visual.
                bool travelled = p.MaxRange > 0f && p.Traveled >= p.MaxRange;
                if (p.Go == null || travelled || p.Age >= MaxVisualSeconds)
                    _expiredScratch.Add(kvp.Key);
            }

            foreach (var id in _expiredScratch)
            {
                if (_projectiles.TryGetValue(id, out var p) && p.Go != null)
                    UnityEngine.Object.Destroy(p.Go);
                _projectiles.Remove(id);
            }
        }
    }
}
