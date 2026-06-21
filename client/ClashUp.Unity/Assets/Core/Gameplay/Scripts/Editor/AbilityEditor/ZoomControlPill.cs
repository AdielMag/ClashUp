using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    /// <summary>
    /// Bottom-left zoom control pill:  –  100%  +  | fit | grid-toggle.
    /// Wires +/- to the graph view transform scale and fit to FrameAll().
    /// </summary>
    public sealed class ZoomControlPill : VisualElement
    {
        private readonly GraphView _graph;
        private readonly Label _zoomLabel;
        private readonly Action _toggleGrid;

        public ZoomControlPill(GraphView graph, Action toggleGrid)
        {
            _graph = graph;
            _toggleGrid = toggleGrid;
            AddToClassList("ae-zoom-pill");

            Add(MakeButton("–", () => Zoom(1f / 1.2f)));   // en-dash minus

            _zoomLabel = new Label("100%");
            _zoomLabel.AddToClassList("ae-zoom-pill__label");
            Add(_zoomLabel);

            Add(MakeButton("+", () => Zoom(1.2f)));
            Add(MakeSeparator());
            Add(MakeButton("⛶", () => _graph.FrameAll()) /* fit */);
            Add(MakeSeparator());
            Add(MakeButton("⌗", () => _toggleGrid?.Invoke()) /* grid toggle */);

            _graph.viewTransformChanged += _ => UpdateLabel();
            UpdateLabel();
        }

        private Button MakeButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("ae-zoom-pill__btn");
            return b;
        }

        private static VisualElement MakeSeparator()
        {
            var s = new VisualElement();
            s.AddToClassList("ae-zoom-pill__sep");
            return s;
        }

        private void Zoom(float factor)
        {
            var tx = _graph.viewTransform;
            float scale = Mathf.Clamp(tx.scale.x * factor,
                ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            // Zoom around the centre of the graph view.
            Vector2 center = _graph.layout.size * 0.5f;
            Vector3 pos = tx.position;
            float k = scale / tx.scale.x;
            pos.x = center.x - (center.x - pos.x) * k;
            pos.y = center.y - (center.y - pos.y) * k;

            _graph.UpdateViewTransform(pos, new Vector3(scale, scale, 1f));
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            _zoomLabel.text = Mathf.RoundToInt(_graph.viewTransform.scale.x * 100f) + "%";
        }
    }
}
