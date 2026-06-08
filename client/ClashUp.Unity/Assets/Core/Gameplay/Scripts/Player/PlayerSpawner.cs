using System;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class PlayerSpawner : IStartable, IDisposable
    {
        private GameObject _lightGo;

        public void Start()
        {
            SpawnLight();
        }

        public void Dispose()
        {
            if (_lightGo != null) UnityEngine.Object.Destroy(_lightGo);
        }

        private void SpawnLight()
        {
            _lightGo = new GameObject("DirectionalLight");
            _lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = _lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.88f);
        }
    }
}
