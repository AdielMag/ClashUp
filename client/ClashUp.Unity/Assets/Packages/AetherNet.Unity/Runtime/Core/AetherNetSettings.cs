using UnityEngine;

namespace AetherNet
{
    /// <summary>
    /// Project-wide AetherNet configuration.
    /// Place one instance under a Resources/ folder so it auto-loads at boot.
    /// Settings are applied in both editor and runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "AetherNetSettings", menuName = "AetherNet/Settings")]
    public sealed class AetherNetSettings : ScriptableObject
    {
        [Header("Coordinate Mapping")]
        [Tooltip("Which Unity plane the 2D simulation maps onto.\nXY = side-view / platformer.\nXZ = top-down.")]
        [SerializeField] private SimulationPlane _plane = SimulationPlane.XY;

        [Header("Unit Conversion")]
        [Tooltip("Unity world units per simulation meter.\n100 = pixel-art convention (1 Unity unit = 100 px = 1 m).\n1 = direct 1:1 mapping.")]
        [Min(0.001f)]
        [SerializeField] private float _pixelsPerMeter = 100f;

        public SimulationPlane Plane          => _plane;
        public float           PixelsPerMeter => _pixelsPerMeter;

        /// <summary>Apply this settings asset to SimulationConstants.</summary>
        public void Apply()
        {
            SimulationConstants.Plane          = _plane;
            SimulationConstants.PixelsPerMeter = _pixelsPerMeter;
        }

        private static AetherNetSettings _instance;

        /// <summary>
        /// Loads the first AetherNetSettings from Resources/.
        /// Returns null if none exists (defaults remain).
        /// </summary>
        public static AetherNetSettings Load()
        {
            if (_instance == null)
                _instance = Resources.Load<AetherNetSettings>("AetherNetSettings");
            return _instance;
        }

        /// <summary>
        /// Loads and applies settings. Called automatically at runtime and in editor.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoApply()
        {
            var settings = Load();
            if (settings != null)
                settings.Apply();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorAutoApply()
        {
            var settings = Load();
            if (settings != null)
                settings.Apply();
        }
#endif
    }
}
