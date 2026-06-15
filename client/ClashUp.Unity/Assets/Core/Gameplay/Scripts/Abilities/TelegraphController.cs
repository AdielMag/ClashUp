using System;
using ClashUp.Shared.Abilities;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Drives two TelegraphRenderer instances for the local player:
    ///   - Auto-attack telegraph: visible whenever idle/moving.
    ///   - Primary-ability telegraph: shown while the ability button is held (live aim).
    ///   Switches back to auto when the button is released.
    /// </summary>
    public sealed class TelegraphController : IStartable, ITickable, IDisposable
    {
        private readonly IAbilityInput _input;
        private readonly PlayerViewSystem _views;
        private readonly MatchCharactersHolder _characters;
        private readonly MatchAbilitiesHolder _abilities;
        private readonly AbilityVisualRegistry _registry;

        private GameObject _root;
        private TelegraphRenderer _autoRenderer;
        private TelegraphRenderer _primaryRenderer;

        private TelegraphConfig _autoConfig;
        private TelegraphConfig _primaryConfig;
        private TelegraphVisualData _autoVisual;
        private TelegraphVisualData _primaryVisual;

        private bool _isHolding;
        private bool _configResolved;
        private bool _autoShownOnce;
        private int _resolveAttempts;

        private static readonly TelegraphVisualData FallbackAutoVisual = new()
        {
            TelegraphColor = new Color(1f, 1f, 0.6f, 0.25f),
        };
        private static readonly TelegraphVisualData FallbackPrimaryVisual = new()
        {
            TelegraphColor = new Color(1f, 0.5f, 0.1f, 0.45f),
        };

        public TelegraphController(
            IAbilityInput input,
            PlayerViewSystem views,
            MatchCharactersHolder characters,
            MatchAbilitiesHolder abilities,
            AbilityVisualRegistry registry)
        {
            _input      = input;
            _views      = views;
            _characters = characters;
            _abilities  = abilities;
            _registry   = registry;
        }

        public void Start()
        {
            _root = new GameObject("TelegraphRoot");

            var autoGo = new GameObject("AutoTelegraph");
            autoGo.transform.SetParent(_root.transform, false);
            _autoRenderer = autoGo.AddComponent<TelegraphRenderer>();
            _autoRenderer.Hide();

            var primaryGo = new GameObject("PrimaryTelegraph");
            primaryGo.transform.SetParent(_root.transform, false);
            _primaryRenderer = primaryGo.AddComponent<TelegraphRenderer>();
            _primaryRenderer.Hide();

            _input.OnTouching += OnTouching;
            UnityEngine.Debug.Log("[Telegraph] Start() called — root created");
        }

        public void Tick()
        {
            var localTransform = _views.LocalPlayerTransform;
            if (localTransform == null)
            {
                _autoRenderer.Hide();
                _primaryRenderer.Hide();
                return;
            }

            if (!_configResolved && _resolveAttempts < 300)
            {
                _resolveAttempts++;
                TryResolveConfig();
                if (_configResolved)
                {
                    var autoShape = _autoConfig != null ? _autoConfig.Shape.ToString() : "null";
                    var primShape = _primaryConfig != null ? _primaryConfig.Shape.ToString() : "null";
                    UnityEngine.Debug.Log($"[Telegraph] Config resolved after {_resolveAttempts} attempts — autoConfig={autoShape} r={_autoConfig?.Radius} primaryConfig={primShape}");
                }
                else if (_resolveAttempts == 300)
                    UnityEngine.Debug.LogWarning("[Telegraph] Gave up resolving config after 300 ticks");
            }

            var origin = localTransform.position;
            origin.y = 0.02f;

            float playerYaw = localTransform.eulerAngles.y;

            if (_isHolding)
            {
                _autoRenderer.Hide();
                if (_primaryConfig != null)
                {
                    float aimYaw = _input.LiveAimYaw;
                    // Fallback to player facing when no aim direction yet
                    if (Mathf.Approximately(aimYaw, 0f))
                        aimYaw = playerYaw;
                    _primaryRenderer.Show(_primaryConfig, _primaryVisual ?? FallbackPrimaryVisual, origin, aimYaw);
                }
                else
                {
                    _primaryRenderer.Hide();
                }
            }
            else
            {
                _primaryRenderer.Hide();
                if (_autoConfig != null)
                {
                    if (!_autoShownOnce)
                    {
                        _autoShownOnce = true;
                        UnityEngine.Debug.Log($"[Telegraph] Showing auto telegraph at {origin} shape={_autoConfig.Shape} r={_autoConfig.Radius}");
                    }
                    _autoRenderer.Show(_autoConfig, _autoVisual ?? FallbackAutoVisual, origin, playerYaw);
                }
                else
                    _autoRenderer.Hide();
            }
        }

        public void Dispose()
        {
            _input.OnTouching -= OnTouching;
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }

        private void OnTouching(bool active) => _isHolding = active;

        private void TryResolveConfig()
        {
            var charId = _views.LocalPlayerCharacterId;
            if (charId.Value == null) return;

            var charDef = _characters.Catalog.Get(charId);

            if (charDef.AutoAttackId.Value != null && _abilities.TryGet(charDef.AutoAttackId, out var autoInfo))
            {
                _autoConfig = autoInfo.Telegraph;
                var autoVis = _registry?.GetByAbilityId(charDef.AutoAttackId.Value);
                _autoVisual = autoVis?.Telegraph;
            }

            if (charDef.ActiveAbilityId.Value != null && _abilities.TryGet(charDef.ActiveAbilityId, out var primaryInfo))
            {
                _primaryConfig = primaryInfo.Telegraph;
                var primaryVis = _registry?.GetByAbilityId(charDef.ActiveAbilityId.Value);
                _primaryVisual = primaryVis?.Telegraph;
            }

            if (_autoConfig == null && _primaryConfig == null)
            {
                if (_resolveAttempts == 1 || _resolveAttempts == 60 || _resolveAttempts == 150)
                {
                    var autoId = charDef.AutoAttackId.Value != null ? charDef.AutoAttackId.Value : "null";
                    var primId = charDef.ActiveAbilityId.Value != null ? charDef.ActiveAbilityId.Value : "null";
                    UnityEngine.Debug.LogWarning($"[Telegraph] Resolve attempt {_resolveAttempts} failed — charId={charId.Value} autoId={autoId} primId={primId} abilitiesCount={_abilities.Count}");
                }
                return;
            }

            // Mark resolved once we have at least one config
            _configResolved = true;
        }
    }
}
