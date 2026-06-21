using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    /// <summary>
    /// Base for every node in the ability graph. All category-driven visuals
    /// (accent stripe, header icon chip, category tag, port colour) are applied
    /// here from <see cref="NodeVisuals"/> — concrete node types add zero styling
    /// code. Structural styling lives in AbilityEditor.uss (class "clashup-node").
    /// </summary>
    public abstract class AbilityGraphNode : Node
    {
        public NodeCategory Category { get; }
        protected Color Accent => NodeVisuals.Accent(Category);

        protected AbilityGraphNode(string title, NodeCategory category)
        {
            Category = category;
            this.title = title;

            AddToClassList("clashup-node");
            ApplyCategoryChrome();

            // Once attached, mirror each field's tooltip onto its label so the
            // description shows when hovering the label too (otherwise the label's
            // built-in truncation tooltip — the full label text — takes over).
            // Runs for every node type automatically; no per-field wiring needed.
            RegisterCallback<AttachToPanelEvent>(_ => PropagateFieldTooltips());
        }

        private void PropagateFieldTooltips()
        {
            this.Query<VisualElement>(className: "unity-base-field").ForEach(field =>
            {
                if (string.IsNullOrEmpty(field.tooltip)) return;
                var label = field.Q<Label>(className: "unity-base-field__label");
                if (label != null) label.tooltip = field.tooltip;
            });
        }

        private void ApplyCategoryChrome()
        {
            var accent = Accent;

            // 1. Accent stripe — first child of the node border.
            var stripe = new VisualElement();
            stripe.AddToClassList("ae-accent-stripe");
            stripe.style.backgroundColor = accent;
            mainContainer.Insert(0, stripe);

            // 2. Header icon chip (background = accent @ ~16% alpha, shape = full accent).
            var chip = new VisualElement();
            chip.AddToClassList("ae-icon-chip");
            chip.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.16f);

            var icon = new NodeIcon(NodeVisuals.Shape(Category), accent);
            icon.style.position = Position.Absolute;
            icon.style.left = 0; icon.style.right = 0;
            icon.style.top = 0; icon.style.bottom = 0;
            chip.Add(icon);

            titleContainer.Insert(0, chip); // before #title-label

            // 3. Category tag — right-aligned uppercase chip.
            var tag = new Label(NodeVisuals.Tag(Category));
            tag.AddToClassList("ae-cat-tag");
            titleContainer.Add(tag);
        }

        /// <summary>
        /// Colour a port with the node's category accent so the dot fills with it
        /// and connected edges inherit the source node's colour. Call after the
        /// port is created.
        /// </summary>
        protected void StylePort(Port port)
        {
            if (port == null) return;
            port.portColor = Accent;
        }
    }
}
