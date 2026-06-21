using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    /// <summary>
    /// Dotted canvas background. GraphView's built-in <see cref="GridBackground"/>
    /// only draws lines, so this custom element draws two layers of dots that
    /// pan and zoom with the graph's view transform.
    /// </summary>
    public sealed class DottedGridBackground : VisualElement
    {
        private readonly GraphView _graph;

        private const float FineSpacing = 24f;
        private const float CoarseSpacing = 120f;

        private static readonly Color CanvasBg   = new Color(27f / 255f, 28f / 255f, 31f / 255f);
        private static readonly Color FineDot     = new Color(1f, 1f, 1f, 0.022f);
        private static readonly Color CoarseDot   = new Color(1f, 1f, 1f, 0.05f);

        public DottedGridBackground(GraphView graph)
        {
            _graph = graph;
            pickingMode = PickingMode.Ignore;
            style.backgroundColor = CanvasBg;
            generateVisualContent += OnGenerate;
            _graph.viewTransformChanged += _ => MarkDirtyRepaint();
        }

        private void OnGenerate(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;

            var tx = _graph.viewTransform;
            float scale = tx.scale.x;
            Vector2 offset = new Vector2(tx.position.x, tx.position.y);

            DrawLayer(mgc.painter2D, r, CoarseSpacing * scale, offset, CoarseDot, 1.6f);
            DrawLayer(mgc.painter2D, r, FineSpacing * scale, offset, FineDot, 1.1f);
        }

        private static void DrawLayer(Painter2D painter, Rect r, float spacing, Vector2 offset, Color color, float radius)
        {
            if (spacing < 6f) return; // too dense to be useful / cheap-out

            float startX = Mathf.Repeat(offset.x, spacing);
            float startY = Mathf.Repeat(offset.y, spacing);

            painter.fillColor = color;
            for (float x = startX; x < r.width; x += spacing)
            {
                for (float y = startY; y < r.height; y += spacing)
                {
                    painter.BeginPath();
                    painter.Arc(new Vector2(x, y), radius, 0f, 360f);
                    painter.Fill();
                }
            }
        }
    }
}
