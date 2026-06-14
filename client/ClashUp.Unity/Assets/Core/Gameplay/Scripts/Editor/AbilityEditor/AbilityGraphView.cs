using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    public sealed class AbilityGraphView : GraphView
    {
        public AbilityGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var bg = new GridBackground();
            Insert(0, bg);
            bg.StretchToParentSize();

            style.flexGrow = 1;
        }

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
            evt.menu.AppendAction("Add Parallel Node",   _ => CreateParallelNode(GetEventPosition(evt)));
            evt.menu.AppendAction("Add Hitbox Node",     _ => CreateHitboxNode(GetEventPosition(evt)));
            evt.menu.AppendAction("Add Projectile Node", _ => CreateProjectileNode(GetEventPosition(evt)));
        }

        private Vector2 GetEventPosition(ContextualMenuPopulateEvent evt) =>
            contentViewContainer.WorldToLocal(evt.mousePosition);

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
