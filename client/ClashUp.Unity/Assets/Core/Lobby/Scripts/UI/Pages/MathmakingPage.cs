using System;
using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.Lobby.UI.Pages
{
    public class MathmakingPage : LobbyPage
    {
        // Serialized fallback — set in inspector or via build script.
        // If null, Awake finds it by path instead.
        [SerializeField] private Button _playButton;

        public event Action OnPlayClicked;

        protected override void Awake()
        {
            base.Awake();
            _pageId          = "matchmaking";
            _pageDisplayName = "MATCHMAKING";

            // Resolve button — serialized ref first, then hierarchy path fallback.
            if (_playButton == null)
            {
                var t = transform.Find("VScrollViewport/VContent/PlayButton");
                if (t != null) _playButton = t.GetComponent<Button>();
            }

            if (_playButton != null)
                _playButton.onClick.AddListener(() => OnPlayClicked?.Invoke());
        }

        public Button PlayButton => _playButton;
    }
}
