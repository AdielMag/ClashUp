using System;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class PlayerSpawner : IStartable, IDisposable
    {
        private readonly IMovementInput _movementInput;
        private GameObject _playerGo;
        private GameObject _groundGo;
        private GameObject _lightGo;

        public Transform PlayerTransform { get; private set; }

        public PlayerSpawner(IMovementInput movementInput)
        {
            _movementInput = movementInput;
        }

        public void Start()
        {
            SpawnLight();
            SpawnGround();
            SpawnPlayer();
        }

        public void Dispose()
        {
            if (_playerGo != null) UnityEngine.Object.Destroy(_playerGo);
            if (_groundGo != null) UnityEngine.Object.Destroy(_groundGo);
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

        private void SpawnGround()
        {
            _groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _groundGo.name = "Ground";
            _groundGo.transform.localScale = new Vector3(20f, 1f, 20f);

            var mat = _groundGo.GetComponent<Renderer>().material;
            mat.mainTexture = CreateGridTexture();
            mat.mainTextureScale = new Vector2(20f, 20f);
        }

        private static Texture2D CreateGridTexture()
        {
            const int size = 128;
            const int lineWidth = 3;
            var baseColor = new Color(0.25f, 0.55f, 0.18f);
            var lineColor = new Color(0.12f, 0.32f, 0.08f);

            var tex = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = (x < lineWidth || y < lineWidth) ? lineColor : baseColor;

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void SpawnPlayer()
        {
            _playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _playerGo.name = "Player";
            _playerGo.transform.position = new Vector3(0f, 1f, 0f);

            _playerGo.AddComponent<CharacterController>();

            var movement = _playerGo.AddComponent<PlayerMovement>();
            movement.Initialize(_movementInput);

            PlayerTransform = _playerGo.transform;
        }
    }
}
