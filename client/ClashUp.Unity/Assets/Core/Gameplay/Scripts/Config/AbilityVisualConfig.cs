using System;
using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    [CreateAssetMenu(fileName = "AbilityVisualConfig", menuName = "ClashUp/Ability Visual Config")]
    public sealed class AbilityVisualConfig : ScriptableObject
    {
        public GameObject CastVfxPrefab;
        public GameObject ProjectilePrefab;
        public GameObject HitVfxPrefab;
        public TelegraphVisualData Telegraph;
        public AudioClip CastSound;
        public AudioClip HitSound;

        [Header("Cast Area Flash")]
        [Tooltip("Color of the triggered damage-footprint flash (alpha must be > 0 to be used).")]
        public Color CastFlashColor = new Color(1f, 0.85f, 0.2f, 0.6f);
        [Tooltip("Fade-out duration of the cast flash, in seconds.")]
        public float CastFlashDuration = 0.22f;
    }

    [Serializable]
    public sealed class TelegraphVisualData
    {
        public Material TelegraphMaterial;
        public Color TelegraphColor = new Color(1f, 0.8f, 0f, 0.5f);
    }
}
