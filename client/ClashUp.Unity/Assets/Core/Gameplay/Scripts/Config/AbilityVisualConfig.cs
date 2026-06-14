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
    }

    [Serializable]
    public sealed class TelegraphVisualData
    {
        public Material TelegraphMaterial;
        public Color TelegraphColor = new Color(1f, 0.8f, 0f, 0.5f);
    }
}
