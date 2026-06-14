using System.Collections.Generic;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    [System.Serializable]
    public struct PageEntry
    {
        public string pageKey;
        public GameObject prefab;
    }

    [SerializeField] private PageEntry[] pageEntries;

    private readonly Dictionary<string, GameObject> _prefabs = new();
    private GameObject _currentPageInstance;
    private Transform _pagesContainer;

    void Awake()
    {
        _pagesContainer = new GameObject("PagesContainer").transform;
        _pagesContainer.SetParent(transform);

        foreach (var entry in pageEntries)
            if (entry.prefab != null)
                _prefabs[entry.pageKey.ToLower()] = entry.prefab;
    }

    public void ShowPage(string pageKey)
    {
        pageKey = pageKey.ToLower();
        if (!_prefabs.TryGetValue(pageKey, out var prefab))
        {
            Debug.LogWarning($"No prefab registered for page '{pageKey}'.");
            return;
        }

        if (_currentPageInstance != null)
            Destroy(_currentPageInstance);

        _currentPageInstance = Instantiate(prefab, _pagesContainer);
        _currentPageInstance.SetActive(true);
    }

    public void OnSubdomainReached(string subdomain) => ShowPage(subdomain);

    public void LoadPageDirectly(string pageKey) => ShowPage(pageKey);
}
