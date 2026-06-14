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

        public EnumField TelegraphTypeField;
        public EnumField TelegraphOriginField;
        public FloatField TelegraphRadiusField;
        public FloatField TelegraphLengthField;
        public FloatField TelegraphAngleField;
        public FloatField TelegraphShowDurationField;

        public ObjectField VisualConfigField;
        public Port OutputPort;

        public RootNode() : base("Ability Root")
        {
            capabilities &= ~Capabilities.Deletable;
            titleContainer.style.backgroundColor = new Color(0.13f, 0.33f, 0.53f);

            IdField = new TextField("Ability ID") { value = "new_ability" };
            DisplayNameField = new TextField("Display Name") { value = "New Ability" };
            CooldownField = new FloatField("Cooldown (s)") { value = 1f };
            ButtonIndexField = new IntegerField("Button (0-3)") { value = 0 };

            TelegraphTypeField = new EnumField("Shape", TelegraphShapeType.Circle);
            TelegraphOriginField = new EnumField("Origin", TelegraphOriginType.Caster);
            TelegraphRadiusField = new FloatField("Radius") { value = 1.5f };
            TelegraphLengthField = new FloatField("Length") { value = 3f };
            TelegraphAngleField = new FloatField("Cone Spread (deg)") { value = 45f };
            TelegraphShowDurationField = new FloatField("Show (s)") { value = 0f };

            extensionContainer.Add(IdField);
            extensionContainer.Add(DisplayNameField);
            extensionContainer.Add(CooldownField);
            extensionContainer.Add(ButtonIndexField);
            extensionContainer.Add(MakeSectionHeader("Telegraph"));
            extensionContainer.Add(TelegraphTypeField);
            extensionContainer.Add(TelegraphOriginField);
            extensionContainer.Add(TelegraphRadiusField);
            extensionContainer.Add(TelegraphLengthField);
            extensionContainer.Add(TelegraphAngleField);
            extensionContainer.Add(TelegraphShowDurationField);

            extensionContainer.Add(MakeSectionHeader("Visuals"));
            VisualConfigField = new ObjectField("Visual Config") { objectType = typeof(AbilityVisualConfig), allowSceneObjects = false };
            extensionContainer.Add(VisualConfigField);

            TelegraphTypeField.RegisterValueChangedCallback(_ => UpdateTelegraphFieldVisibility());
            TelegraphOriginField.RegisterValueChangedCallback(_ => UpdateTelegraphFieldVisibility());
            UpdateTelegraphFieldVisibility();

            OutputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(AbilityNode));
            OutputPort.portName = "Out";
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

            TelegraphRadiusField.style.display = isCircle ? DisplayStyle.Flex : DisplayStyle.None;
            TelegraphLengthField.style.display = !isCircle ? DisplayStyle.Flex : DisplayStyle.None;
            TelegraphAngleField.style.display  = isCone ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static VisualElement MakeSectionHeader(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            label.style.paddingLeft = 6;
            label.style.paddingTop = 4;
            label.style.paddingBottom = 4;
            label.style.marginTop = 6;
            label.style.color = new Color(0.75f, 0.75f, 0.75f);
            return label;
        }
    }
}
