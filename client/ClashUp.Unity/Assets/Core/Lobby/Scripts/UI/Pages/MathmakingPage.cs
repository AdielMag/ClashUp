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

        /// <summary>Raised with the chosen matchmaking modeId when a mode entry is clicked.</summary>
        public event Action<string> OnModeSelected;

        protected override void Awake()
        {
            base.Awake();
            _pageId          = "matchmaking";
            _pageDisplayName = "MATCHMAKING";

            // Preferred: one button per game mode, each tagged with a GameModeButton (its modeId).
            var modeButtons = GetComponentsInChildren<GameModeButton>(includeInactive: true);
            if (modeButtons.Length > 0)
            {
                foreach (var mb in modeButtons)
                {
                    var button = mb.GetComponent<Button>();
                    if (button == null) continue;
                    string modeId = mb.ModeId;
                    button.onClick.AddListener(() => OnModeSelected?.Invoke(modeId));
                }
                return;
            }

            // Legacy fallback: a single Play button → the default mode.
            if (_playButton == null)
            {
                var t = transform.Find("VScrollViewport/VContent/PlayButton");
                if (t != null) _playButton = t.GetComponent<Button>();
            }

            if (_playButton != null)
                _playButton.onClick.AddListener(() => OnModeSelected?.Invoke("default"));
        }

        public Button PlayButton => _playButton;
    }
}
