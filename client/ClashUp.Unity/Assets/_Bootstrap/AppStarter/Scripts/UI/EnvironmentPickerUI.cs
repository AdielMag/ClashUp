#if CLASHUP_DEV || UNITY_EDITOR
using System.Linq;
using ClashUp.Client.Networking;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClashUp.Client.AppStarter
{
    public static class EnvironmentPickerUI
    {
        public static async UniTask<ServerEnvironment> ShowAndWaitAsync(EnvironmentConfig config)
        {
            var tcs = new UniTaskCompletionSource<ServerEnvironment>();
            var environments = config.GetAllEnvironments();

            if (EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
                Object.DontDestroyOnLoad(esGo);
            }

            var prefab = Resources.Load<GameObject>("EnvironmentPickerUI");
            var go = Object.Instantiate(prefab);
            go.name = "EnvironmentPickerUI";
            Object.DontDestroyOnLoad(go);

            var dropdown = go.GetComponentInChildren<TMP_Dropdown>();
            dropdown.options = environments
                .Select(e => new TMP_Dropdown.OptionData(e.ToString()))
                .ToList();

            var selectedIndex = System.Array.IndexOf(environments, config.Current);
            dropdown.value = selectedIndex;
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(i => selectedIndex = i);

            var btn = go.GetComponentInChildren<Button>();
            btn.onClick.AddListener(() => tcs.TrySetResult(environments[selectedIndex]));

            var result = await tcs.Task;
            Object.Destroy(go);
            return result;
        }
    }
}
#endif
