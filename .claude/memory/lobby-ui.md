---
name: lobby-ui
description: "Lobby pager UI architecture — horizontal page scroll, vertical per-page scroll, bottom bar, play button wiring"
metadata: 
  node_type: memory
  type: project
  originSessionId: d658cddd-d2ef-49fe-88d4-3e302f77aedd
---

# Lobby UI Architecture

## Scene Structure (`Lobby.unity`)

```
LobbyCanvas (ScreenSpaceOverlay, CanvasScaler 1080×1920, GraphicRaycaster)
├── ScrollViewport (LobbyHorizontalScroll, Mask, Image α=0.01 RaycastTarget)
│   └── HContent (RectTransform, 5 children = 5 pages)
│       ├── Page_SETTINGS (idx 0, x=0)
│       ├── Page_CHARACTER (idx 1, x=1080)
│       ├── Page_MATCHMAKING (idx 2, x=2160) ← defaultPage
│       ├── Page_GUILDS (idx 3, x=3240)
│       └── Page_STORE (idx 4, x=4320)
└── BottomBar (LobbyBottomBar, 5 dot buttons in IndicatorRow HorizontalLayoutGroup)

LobbyLifetimeScope (root, VContainer scope for LobbyEntryPoint)
```

Each page has:
- `Canvas` (overrideSorting=false, child of root canvas) + `GraphicRaycaster` + `LobbyCanvasOptimizer`
- `VScrollViewport` (RectMask2D + `LobbyVerticalScroll` with `_content = VContent`)
- `VContent` (VerticalLayoutGroup + ContentSizeFitter + 10 cards + optional extras)

## Key Scripts

| Script | Location | Role |
|--------|----------|------|
| `LobbyHorizontalScroll` | `Core/Lobby/Scripts/UI/` | Owns all drag input; horizontal = page snap, vertical = delegate to LobbyVerticalScroll |
| `LobbyVerticalScroll` | `Core/Lobby/Scripts/UI/` | Per-page content scroll; driven via public `BeginDrag/Drag/EndDrag` — NOT an EventSystem handler |
| `LobbyBottomBar` | `Core/Lobby/Scripts/UI/` | 5 dot indicators; `SetCurrentPage(idx)` changes size/alpha; dot Button.onClick → `_mainScroll.GoToPage(idx)` |
| `LobbyPage` | `Core/Lobby/Scripts/UI/` | Base class; `_pageCanvas`, `_optimizer`, `OnPageShown/Hidden`, `Initialize` |
| `LobbyCanvasOptimizer` | `Core/Lobby/Scripts/UI/` | LateUpdate AABB check; enables/disables Canvas when page scrolls in/out of view |
| `MathmakingPage` | `Core/Lobby/Scripts/UI/Pages/` | Subclass of LobbyPage; exposes `event Action OnPlayClicked`; wires `_playButton.onClick` in Awake |

**IMPORTANT TYPO**: The class is `MathmakingPage` (missing the 'c') — this is intentional at this point. Use this spelling everywhere: in `FindFirstObjectByType<MathmakingPage>()`, serialized field references, etc.

## Play Button Flow

```
MathmakingPage.Awake()
  → _playButton.onClick.AddListener(() => OnPlayClicked?.Invoke())

LobbyUI.Create()
  → FindFirstObjectByType<MathmakingPage>()
  → page.OnPlayClicked += () => ui.OnPlayClicked?.Invoke()

LobbyEntryPoint.StartAsync()
  → ui = LobbyUI.Create()
  → ui.OnPlayClicked += () => playClicked.TrySetResult()
  → await playClicked.Task
  → _flow.EnterMatchmaking()
```

## Axis Locking Logic (LobbyHorizontalScroll)

`_lockMinPixels = 8` threshold before axis is decided. Then:
- `ax > ay * _lockRatio` (default ratio=2) → locked horizontal (page switch)
- `ay > ax * _lockRatio` → locked vertical (delegate to LobbyVerticalScroll)
- Otherwise → dominant axis wins

`_defaultPage = 2` (MATCHMAKING center page, index 2).

## Layout Details

- `ScrollViewport.offsetMin = (0, 110)` — pixel-exact 110px gap at bottom for BottomBar
- `BottomBar.sizeDelta = (0, 110)`, anchored to bottom
- Active dot: white, 16×16px; inactive: 30% opacity, 10×10px
- Cards: 220px preferred height via `LayoutElement`; PlayButton: 160px preferred height
- `Mask` on ScrollViewport (not RectMask2D) — required to clip child `Canvas` components

## VContainer Integration

`LobbyLifetimeScope` must be in the scene as a root GameObject with:
- `autoRun: 1`
- Empty `parentReference.TypeName` (inherits from enqueued parent via `GameFlowController`)
- `Transform` (not RectTransform) + fileID in `SceneRoots.m_Roots`

`GameFlowController` calls `LifetimeScope.EnqueueParent(_scope)` before `_sceneLoader.LoadAdditiveAsync("Lobby")`.

## Script GUIDs

| Script | GUID |
|--------|------|
| `MathmakingPage.cs` | `378fc87a06a6763419df9df9158e3a1d` |
| `LobbyLifetimeScope.cs` | `0bbea71861e1f7c4383ee885be33a60c` |
| `LobbyHorizontalScroll.cs` | (in asmdef `ClashUp.Lobby`) |
| `LobbyVerticalScroll.cs` | (in asmdef `ClashUp.Lobby`) |
| `LobbyBottomBar.cs` | (in asmdef `ClashUp.Lobby`) |
