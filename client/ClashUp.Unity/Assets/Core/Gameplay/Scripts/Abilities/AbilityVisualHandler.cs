using System;
using ClashUp.Client.Networking;
using ClashUp.Shared.MessagePackObjects;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class AbilityVisualHandler : IStartable, IDisposable
    {
        private readonly AbilityVisualRegistry _registry;
        private readonly MatchHubReceiver _receiver;
        private readonly PlayerViewSystem _views;

        public AbilityVisualHandler(AbilityVisualRegistry registry, MatchHubReceiver receiver, PlayerViewSystem views)
        {
            _registry = registry;
            _receiver = receiver;
            _views = views;
        }

        public void Start()
        {
            _receiver.MatchEventOccurred += OnMatchEvent;
        }

        private void OnMatchEvent(MatchEvent evt)
        {
            switch (evt.Kind)
            {
                case "ability_cast":
                    HandleCast(evt.Payload);
                    break;
                case "ability_hit":
                    HandleHit(evt.Payload);
                    break;
                case "projectile_spawn":
                    HandleProjectileSpawn(evt.Payload);
                    break;
                case "projectile_destroy":
                    HandleProjectileDestroy(evt.Payload);
                    break;
            }
        }

        private void HandleCast(string payload)
        {
            if (payload == null) return;
            var obj = JObject.Parse(payload);
            string abilityId = obj["abilityId"]?.Value<string>();
            string caster = obj["caster"]?.Value<string>();
            if (abilityId == null) return;

            var visual = _registry?.GetByAbilityId(abilityId);
            if (visual?.CastVfxPrefab != null)
            {
                _views.TryGetPosition(caster, out var pos);
                UnityEngine.Object.Instantiate(visual.CastVfxPrefab, pos, Quaternion.identity);
            }
            else
            {
                SpawnFallbackFlash(caster);
            }

            if (visual?.CastSound != null)
            {
                _views.TryGetPosition(caster, out var pos);
                AudioSource.PlayClipAtPoint(visual.CastSound, pos);
            }
        }

        private void SpawnFallbackFlash(string casterId)
        {
            if (!_views.TryGetPosition(casterId, out var pos)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "AbilityFlash";
            go.transform.position = pos + Vector3.up * 1.5f;
            go.transform.localScale = Vector3.one * 0.6f;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.material.color = new Color(1f, 0.7f, 0.1f, 0.9f);
            UnityEngine.Object.Destroy(go, 0.3f);
        }

        private void HandleHit(string payload)
        {
            if (payload == null) return;
            var obj = JObject.Parse(payload);
            string target = obj["target"]?.Value<string>();
            string abilityId = obj["abilityId"]?.Value<string>();
            if (target == null || abilityId == null) return;
            if (!_views.TryGetPosition(target, out var pos)) return;

            var visual = _registry.GetByAbilityId(abilityId);
            if (visual?.HitVfxPrefab != null)
                UnityEngine.Object.Instantiate(visual.HitVfxPrefab, pos, Quaternion.identity);

            if (visual?.HitSound != null)
                AudioSource.PlayClipAtPoint(visual.HitSound, pos);
        }

        private void HandleProjectileSpawn(string payload)
        {
            if (payload == null) return;
            // In future: instantiate ProjectilePrefab and drive it client-side
        }

        private void HandleProjectileDestroy(string payload)
        {
            if (payload == null) return;
            // In future: destroy tracked projectile visual
        }

        public void Dispose()
        {
            _receiver.MatchEventOccurred -= OnMatchEvent;
        }
    }
}
