using ClashUp.Shared.Abilities;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    public sealed class HitboxNode : AbilityGraphNode
    {
        public Port InputPort;
        public Port NextPort;
        public FloatField DelayField;
        public EnumField EffectField;
        public FloatField AmountField;
        public FloatField RadiusField;
        public FloatField OffsetForwardField;
        public FloatField DurationField;
        public FloatField HitIntervalField;
        public Toggle HitSelfToggle;
        public Toggle HitAlliesToggle;

        public HitboxNode() : base("Hitbox", NodeCategory.Hitbox)
        {
            DelayField = new FloatField("Delay (s)") { value = 0f };
            EffectField = new EnumField("Effect", HitboxEffect.Damage);
            AmountField = new FloatField("Amount") { value = 10f };
            RadiusField = new FloatField("Radius") { value = 1.5f };
            OffsetForwardField = new FloatField("Offset Forward") { value = 0f };
            DurationField = new FloatField("Duration (s, 0=instant)") { value = 0f };
            HitIntervalField = new FloatField("Interval (s, 0=once)") { value = 0f };
            HitSelfToggle = new Toggle("Hit Self") { value = false };
            HitAlliesToggle = new Toggle("Hit Allies") { value = false };

            extensionContainer.Add(DelayField);
            extensionContainer.Add(EffectField);
            extensionContainer.Add(AmountField);
            extensionContainer.Add(RadiusField);
            extensionContainer.Add(OffsetForwardField);
            extensionContainer.Add(DurationField);
            extensionContainer.Add(HitIntervalField);
            extensionContainer.Add(HitSelfToggle);
            extensionContainer.Add(HitAlliesToggle);

            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(AbilityNode));
            InputPort.portName = "In";
            StylePort(InputPort);
            inputContainer.Add(InputPort);

            NextPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(AbilityNode));
            NextPort.portName = "Next";
            StylePort(NextPort);
            outputContainer.Add(NextPort);

            RefreshExpandedState();
            RefreshPorts();
        }
    }
}
