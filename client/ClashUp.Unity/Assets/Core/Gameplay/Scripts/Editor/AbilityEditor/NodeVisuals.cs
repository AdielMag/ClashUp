using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    /// <summary>
    /// Visual taxonomy for graph nodes. THIS is the extensibility seam.
    ///
    /// To add a new node type (e.g. Spawn) you only touch this file + register the
    /// node in the Add-Node menu:
    ///   1. add a <see cref="NodeCategory"/> value,
    ///   2. add its accent colour to <see cref="AccentColors"/>,
    ///   3. add its icon shape to <see cref="Shapes"/>,
    ///   4. add its uppercase tag to <see cref="Tags"/>.
    /// No new USS and no per-node styling code is required — the base
    /// <see cref="AbilityGraphNode"/> reads everything from here.
    /// </summary>
    public enum NodeCategory
    {
        Root,        // Ability Root
        Action,      // Projectile (generic action)
        Flow,        // Parallel / Sequence
        Hitbox,      // Hitbox (a visually-distinct action)
        Spawn,       // Spawn (added to demonstrate extensibility)
    }

    public enum NodeIconShape { Circle, Triangle, Diamond, Square, Plus, Star }

    public static class NodeVisuals
    {
        /// <summary>Accent colour per category (stripe, icon, ports, edges).</summary>
        public static readonly Dictionary<NodeCategory, Color> AccentColors = new()
        {
            { NodeCategory.Root,   Hex("5b8dd6") }, // --type-root
            { NodeCategory.Action, Hex("62a877") }, // --type-action
            { NodeCategory.Flow,   Hex("9b7bd4") }, // --type-flow
            { NodeCategory.Hitbox, Hex("d6705f") }, // --type-hitbox
            { NodeCategory.Spawn,  Hex("3fae9f") }, // distinct teal accent
        };

        /// <summary>Header icon shape per category.</summary>
        public static readonly Dictionary<NodeCategory, NodeIconShape> Shapes = new()
        {
            { NodeCategory.Root,   NodeIconShape.Circle },
            { NodeCategory.Action, NodeIconShape.Triangle },
            { NodeCategory.Flow,   NodeIconShape.Diamond },
            { NodeCategory.Hitbox, NodeIconShape.Square },
            { NodeCategory.Spawn,  NodeIconShape.Plus },
        };

        /// <summary>Right-aligned uppercase tag shown in the header.</summary>
        public static readonly Dictionary<NodeCategory, string> Tags = new()
        {
            { NodeCategory.Root,   "ROOT" },
            { NodeCategory.Action, "ACTION" },
            { NodeCategory.Flow,   "FLOW" },
            { NodeCategory.Hitbox, "ACTION" },
            { NodeCategory.Spawn,  "ACTION" },
        };

        public static Color Accent(NodeCategory c) =>
            AccentColors.TryGetValue(c, out var col) ? col : Hex("62a877");

        public static NodeIconShape Shape(NodeCategory c) =>
            Shapes.TryGetValue(c, out var s) ? s : NodeIconShape.Circle;

        public static string Tag(NodeCategory c) =>
            Tags.TryGetValue(c, out var t) ? t : "NODE";

        public static Color Hex(string rrggbb)
        {
            ColorUtility.TryParseHtmlString("#" + rrggbb, out var c);
            return c;
        }
    }

    /// <summary>
    /// A 23x23 header icon chip that draws the category's shape via Painter2D.
    /// The chip background tint and the shape colour are supplied by the caller
    /// (derived from the category accent), so no per-category USS is needed.
    /// </summary>
    public sealed class NodeIcon : VisualElement
    {
        private readonly NodeIconShape _shape;
        private readonly Color _color;

        public NodeIcon(NodeIconShape shape, Color color)
        {
            _shape = shape;
            _color = color;
            generateVisualContent += OnGenerate;
        }

        private void OnGenerate(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;

            var painter = mgc.painter2D;
            float cx = r.width * 0.5f;
            float cy = r.height * 0.5f;
            float s = Mathf.Min(r.width, r.height) * 0.32f; // shape half-extent

            painter.fillColor = _color;
            painter.strokeColor = _color;
            painter.lineWidth = Mathf.Max(1.5f, s * 0.45f);
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            switch (_shape)
            {
                case NodeIconShape.Circle:
                    painter.BeginPath();
                    painter.Arc(new Vector2(cx, cy), s, 0f, 360f);
                    painter.Fill();
                    break;

                case NodeIconShape.Triangle:
                    FillPolygon(painter, new[]
                    {
                        new Vector2(cx, cy - s),
                        new Vector2(cx + s * 0.92f, cy + s * 0.8f),
                        new Vector2(cx - s * 0.92f, cy + s * 0.8f),
                    });
                    break;

                case NodeIconShape.Diamond:
                    FillPolygon(painter, new[]
                    {
                        new Vector2(cx, cy - s),
                        new Vector2(cx + s, cy),
                        new Vector2(cx, cy + s),
                        new Vector2(cx - s, cy),
                    });
                    break;

                case NodeIconShape.Square:
                    FillPolygon(painter, new[]
                    {
                        new Vector2(cx - s, cy - s),
                        new Vector2(cx + s, cy - s),
                        new Vector2(cx + s, cy + s),
                        new Vector2(cx - s, cy + s),
                    });
                    break;

                case NodeIconShape.Plus:
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(cx, cy - s));
                    painter.LineTo(new Vector2(cx, cy + s));
                    painter.MoveTo(new Vector2(cx - s, cy));
                    painter.LineTo(new Vector2(cx + s, cy));
                    painter.Stroke();
                    break;

                case NodeIconShape.Star:
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float rad = (i % 2 == 0) ? s : s * 0.45f;
                        float ang = Mathf.Deg2Rad * (-90f + i * 36f);
                        pts[i] = new Vector2(cx + Mathf.Cos(ang) * rad, cy + Mathf.Sin(ang) * rad);
                    }
                    FillPolygon(painter, pts);
                    break;
            }
        }

        private static void FillPolygon(Painter2D painter, Vector2[] pts)
        {
            painter.BeginPath();
            painter.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) painter.LineTo(pts[i]);
            painter.ClosePath();
            painter.Fill();
        }
    }
}
