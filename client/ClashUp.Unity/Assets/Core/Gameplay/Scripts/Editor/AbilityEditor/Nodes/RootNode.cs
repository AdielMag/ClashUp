using ClashUp.Shared.Abilities;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    public enum TelegraphShapeType  { Circle, Line }
    public enum TelegraphOriginType { Caster, Target }

    public sealed class RootNode : AbilityGraphNode
    {
        public TextField IdField;
        public TextField DisplayNameField;
        public FloatField CooldownField;
        public IntegerField ButtonIndexField;
        public EnumField TriggerModeField;
        public EnumField CastModeField;
        public FloatField AutoRangeField;

        public EnumField TelegraphTypeField;
        public EnumField TelegraphOriginField;
        public FloatField TelegraphRadiusField;
        public FloatField TelegraphLengthField;
        public FloatField TelegraphAngleField;
        public FloatField TelegraphForwardOffsetField;
        public FloatField TelegraphShowDurationField;

        public ObjectField VisualConfigField;
        public Port OutputPort;

        public RootNode() : base("Ability Root", NodeCategory.Root)
        {
            capabilities &= ~Capabilities.Deletable;

            IdField = new TextField("Ability ID") { value = "new_ability" };
            DisplayNameField = new TextField("Display Name") { value = "New Ability" };
            CooldownField = new FloatField("Cooldown (s)") { value = 1f };
            ButtonIndexField = new IntegerField("Button (0-3)") { value = 0 };
            TriggerModeField = new EnumField("Trigger Mode", TriggerMode.Manual);
            CastModeField = new EnumField("Cast Mode", CastMode.Aimed);
            AutoRangeField = new FloatField("Auto Range") { value = 0f };

            TelegraphTypeField = new EnumField("Shape", TelegraphShapeType.Circle);
            TelegraphOriginField = new EnumField("Origin", TelegraphOriginType.Caster);
            TelegraphRadiusField = new FloatField("Radius") { value = 1.5f };
            TelegraphLengthField = new FloatField("Length") { value = 3f };
            TelegraphAngleField = new FloatField("Cone Spread (deg)") { value = 45f };
            TelegraphForwardOffsetField = new FloatField("Forward Offset") { value = 0f };
            TelegraphForwardOffsetField.tooltip =
                "TargetCircle: fixed forward offset in directional modes, or the MAX target distance " +
                "when Cast Mode = TargetPoint (joystick distance scales 0..this).";
            TelegraphShowDurationField = new FloatField("Show (s)") { value = 0f };

            extensionContainer.Add(IdField);
            extensionContainer.Add(DisplayNameField);
            extensionContainer.Add(CooldownField);
            extensionContainer.Add(ButtonIndexField);
            extensionContainer.Add(TriggerModeField);
            extensionContainer.Add(CastModeField);
            extensionContainer.Add(AutoRangeField);
            extensionContainer.Add(MakeSectionHeader("Telegraph"));
            extensionContainer.Add(TelegraphTypeField);
            extensionContainer.Add(TelegraphOriginField);
            extensionContainer.Add(TelegraphRadiusField);
            extensionContainer.Add(TelegraphLengthField);
            extensionContainer.Add(TelegraphAngleField);
            extensionContainer.Add(TelegraphForwardOffsetField);
            extensionContainer.Add(TelegraphShowDurationField);

            extensionContainer.Add(MakeSectionHeader("Visuals"));
            VisualConfigField = new ObjectField("Visual Config") { objectType = typeof(AbilityVisualConfig), allowSceneObjects = false };
            extensionContainer.Add(VisualConfigField);

            TelegraphTypeField.RegisterValueChangedCallback(_ => UpdateTelegraphFieldVisibility());
            TelegraphOriginField.RegisterValueChangedCallback(_ => UpdateTelegraphFieldVisibility());
            UpdateTelegraphFieldVisibility();

            OutputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(AbilityNode));
            OutputPort.portName = "Out";
            StylePort(OutputPort);
            outputContainer.Add(OutputPort);

            RefreshExpandedState();
            RefreshPorts();
        }

        public TelegraphShape GetMappedShape()
        {
            var type   = (TelegraphShapeType)TelegraphTypeField.value;
            var origin = (TelegraphOriginType)TelegraphOriginField.value;
            return (type, origin) switch
            {
                (TelegraphShapeType.Circle, TelegraphOriginType.Caster) => TelegraphShape.CircleAroundCaster,
                (TelegraphShapeType.Circle, TelegraphOriginType.Target) => TelegraphShape.TargetCircle,
                (TelegraphShapeType.Line,   TelegraphOriginType.Caster) => TelegraphShape.ForwardLine,
                (TelegraphShapeType.Line,   TelegraphOriginType.Target) => TelegraphShape.ForwardCone,
                _ => TelegraphShape.CircleAroundCaster,
            };
        }

        public void SetMappedShape(TelegraphShape shape)
        {
            switch (shape)
            {
                case TelegraphShape.CircleAroundCaster:
                    TelegraphTypeField.value   = TelegraphShapeType.Circle;
                    TelegraphOriginField.value = TelegraphOriginType.Caster;
                    break;
                case TelegraphShape.TargetCircle:
                    TelegraphTypeField.value   = TelegraphShapeType.Circle;
                    TelegraphOriginField.value = TelegraphOriginType.Target;
                    break;
                case TelegraphShape.ForwardLine:
                    TelegraphTypeField.value   = TelegraphShapeType.Line;
                    TelegraphOriginField.value = TelegraphOriginType.Caster;
                    break;
                case TelegraphShape.ForwardCone:
                    TelegraphTypeField.value   = TelegraphShapeType.Line;
                    TelegraphOriginField.value = TelegraphOriginType.Target;
                    break;
            }
        }

        private void UpdateTelegraphFieldVisibility()
        {
            var type   = (TelegraphShapeType)TelegraphTypeField.value;
            var origin = (TelegraphOriginType)TelegraphOriginField.value;
            bool isCircle = type == TelegraphShapeType.Circle;
            bool isCone   = type == TelegraphShapeType.Line && origin == TelegraphOriginType.Target;

            // Forward offset is only meaningful for target-origin shapes (the telegraph slides away
            // from the caster), e.g. a ranged TargetCircle for a projectile-AoE.
            bool isTarget = origin == TelegraphOriginType.Target;

            TelegraphRadiusField.style.display = isCircle ? DisplayStyle.Flex : DisplayStyle.None;
            TelegraphLengthField.style.display = !isCircle ? DisplayStyle.Flex : DisplayStyle.None;
            TelegraphAngleField.style.display  = isCone ? DisplayStyle.Flex : DisplayStyle.None;
            TelegraphForwardOffsetField.style.display = isTarget ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Full-bleed section divider row (styled via AbilityEditor.uss).
        private static VisualElement MakeSectionHeader(string text)
        {
            var row = new VisualElement();
            row.AddToClassList("ae-section");
            var label = new Label(text.ToUpperInvariant());
            label.AddToClassList("ae-section__label");
            row.Add(label);
            return row;
        }
    }
}
