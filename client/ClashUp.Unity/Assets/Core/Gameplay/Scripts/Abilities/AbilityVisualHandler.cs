using System;
using ClashUp.Client.Networking;
using ClashUp.Shared.Abilities;
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
        private readonly MatchAbilitiesHolder _abilities;

        public AbilityVisualHandler(AbilityVisualRegistry registry, MatchHubReceiver receiver,
                                    PlayerViewSystem views, MatchAbilitiesHolder abilities)
        {
            _registry = registry;
            _receiver = receiver;
            _views = views;
            _abilities = abilities;
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
            float aimYaw = obj["aimYaw"]?.Value<float>() ?? 0f;

            if (!_views.TryGetPosition(caster, out var pos)) return;

            var visual = _registry?.GetByAbilityId(abilityId);

            // Triggered area flash: render the exact damage footprint. Prefer the server-derived
            // CastShape; fall back to AbilitiesConfig.Default so it still shows against older/stale
            // servers that don't send CastShape over the wire.
            TelegraphConfig castShape = null;
            bool isTargetPoint = false;
            if (_abilities != null && _abilities.TryGet(new AbilityId(abilityId), out var info))
            {
                castShape = info.CastShape;
                isTargetPoint = info.CastMode == CastMode.TargetPoint;
            }
            castShape ??= DefaultCastShape(abilityId);

            // TargetPoint abilities cast AT the target, not the player. The projectile spawn +
            // explosion at the target point carries the visual, so skip all caster-side flash/VFX
            // here — only play the cast sound.
            if (isTargetPoint)
            {
                if (visual?.CastSound != null)
                    AudioSource.PlayClipAtPoint(visual.CastSound, pos);
                return;
            }

            bool spawnedFlash = false;
            if (castShape != null)
            {
                Color flashColor = visual != null && visual.CastFlashColor.a > 0f
                    ? visual.CastFlashColor
                    : new Color(1f, 0.85f, 0.2f, 0.6f);
                float duration = visual != null && visual.CastFlashDuration > 0f ? visual.CastFlashDuration : 0.22f;

                var flashOrigin = pos;
                flashOrigin.y = 0.03f;
                AbilityAreaFlash.Spawn(castShape, flashOrigin, aimYaw, flashColor, duration);
                spawnedFlash = true;
            }

            if (visual?.CastVfxPrefab != null)
                UnityEngine.Object.Instantiate(visual.CastVfxPrefab, pos, Quaternion.Euler(0f, aimYaw, 0f));
            else if (!spawnedFlash)
                SpawnFallbackFlash(caster);

            if (visual?.CastSound != null)
                AudioSource.PlayClipAtPoint(visual.CastSound, pos);
        }

        private static TelegraphConfig DefaultCastShape(string abilityId)
        {
            foreach (var a in AbilitiesConfig.Default.Abilities)
                if (a.Id.Value == abilityId)
                    return a.CastShape;
            return null;
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
