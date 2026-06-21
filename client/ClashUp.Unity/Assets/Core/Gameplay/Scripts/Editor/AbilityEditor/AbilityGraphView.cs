using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    public sealed class AbilityGraphView : GraphView
    {
        private const float MiniMapWidth = 210f;
        private const float MiniMapHeight = 160f;

        private readonly DottedGridBackground _grid;
        private readonly MiniMap _miniMap;

        /// <summary>Last cursor position over the canvas, in graph-content space.</summary>
        public Vector2 LastMousePosition { get; private set; } = new Vector2(300f, 200f);

        public AbilityGraphView()
        {
            AddToClassList("ae-graphview");

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // Dotted canvas grid (replaces the line-only GridBackground).
            _grid = new DottedGridBackground(this);
            Insert(0, _grid);
            _grid.StretchToParentSize();

            // MiniMap, pinned bottom-right. An anchored MiniMap positions itself
            // via SetPosition (it ignores style.left/top), so we drive it that way.
            // The "MINIMAP" header is a CHILD of the minimap so it always tracks
            // the panel and renders on top of the built-in "MiniMap <zoom>" title.
            _miniMap = new MiniMap { anchored = true };
            _miniMap.AddToClassList("ae-minimap");
            _miniMap.SetPosition(new Rect(0, 0, MiniMapWidth, MiniMapHeight));
            Add(_miniMap);

            var miniHeader = new Label("MINIMAP");
            miniHeader.AddToClassList("ae-minimap-header");
            miniHeader.pickingMode = PickingMode.Ignore;
            _miniMap.Add(miniHeader);

            // Zoom control pill, pinned bottom-left.
            var pill = new ZoomControlPill(this, ToggleGrid);
            pill.style.left = 16;
            Add(pill);

            // Track the cursor in graph-content space so new nodes spawn there.
            // localMousePosition is relative to this GraphView; ChangeCoordinatesTo
            // maps it into the panned/zoomed content layer. (WorldToLocal on the
            // raw panel position is unreliable when the GraphView is not the whole
            // window.) MouseDown trickles down so the value is fresh before a
            // right-click context menu is populated.
            RegisterCallback<MouseMoveEvent>(e => TrackMouse(e.localMousePosition));
            RegisterCallback<MouseDownEvent>(e => TrackMouse(e.localMousePosition), TrickleDown.TrickleDown);

            // Keep the floating overlays anchored on resize.
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var sz = layout.size;
                _miniMap.SetPosition(new Rect(
                    sz.x - MiniMapWidth - 16, sz.y - MiniMapHeight - 16,
                    MiniMapWidth, MiniMapHeight));
                pill.style.top = sz.y - pill.resolvedStyle.height - 16;
            });

            style.flexGrow = 1;
        }

        private void TrackMouse(Vector2 mouseLocalToGraphView) =>
            LastMousePosition = this.ChangeCoordinatesTo(contentViewContainer, mouseLocalToGraphView);

        private void ToggleGrid() =>
            _grid.style.display = _grid.resolvedStyle.display == DisplayStyle.None
                ? DisplayStyle.Flex : DisplayStyle.None;

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            ports.ForEach(port =>
            {
                if (port != startPort && port.direction != startPort.direction)
                    compatible.Add(port);
            });
            return compatible;
        }

        public void BuildContextMenu(ContextualMenuPopulateEvent evt)
        {
            var pos = LastMousePosition; // set by the MouseDown that opened this menu
            evt.menu.AppendAction("Add Parallel Node",   _ => CreateParallelNode(pos));
            evt.menu.AppendAction("Add Hitbox Node",     _ => CreateHitboxNode(pos));
            evt.menu.AppendAction("Add Projectile Node", _ => CreateProjectileNode(pos));
            evt.menu.AppendAction("Add Spawn Node",      _ => CreateSpawnNode(pos));
        }

        public RootNode CreateRootNode()
        {
            var node = new RootNode();
            node.SetPosition(new Rect(50, 200, 280, 400));
            AddElement(node);
            return node;
        }

        public ParallelNode CreateParallelNode(float x, float y) => CreateParallelNode(new Vector2(x, y));
        public ParallelNode CreateParallelNode(Vector2 pos)
        {
            var node = new ParallelNode();
            node.SetPosition(new Rect(pos, new Vector2(250, 200)));
            AddElement(node);
            return node;
        }

        public HitboxNode CreateHitboxNode(float x, float y) => CreateHitboxNode(new Vector2(x, y));
        public HitboxNode CreateHitboxNode(Vector2 pos)
        {
            var node = new HitboxNode();
            node.SetPosition(new Rect(pos, new Vector2(250, 350)));
            AddElement(node);
            return node;
        }

        public ProjectileNode CreateProjectileNode(float x, float y) => CreateProjectileNode(new Vector2(x, y));
        public ProjectileNode CreateProjectileNode(Vector2 pos)
        {
            var node = new ProjectileNode();
            node.SetPosition(new Rect(pos, new Vector2(250, 300)));
            AddElement(node);
            return node;
        }

        public SpawnNode CreateSpawnNode(float x, float y) => CreateSpawnNode(new Vector2(x, y));
        public SpawnNode CreateSpawnNode(Vector2 pos)
        {
            var node = new SpawnNode();
            node.SetPosition(new Rect(pos, new Vector2(250, 200)));
            AddElement(node);
            return node;
        }

        public void Connect(Port from, Port to)
        {
            var edge = from.ConnectTo(to);
            AddElement(edge);
        }

        public void ClearGraph()
        {
            DeleteElements(graphElements.ToList());
        }
    }
}
