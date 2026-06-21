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
            DelayField = new FloatField("Delay (s)") { value = 0f,
                tooltip = "Delay before this hitbox activates, measured from when the previous node completes." };
            EffectField = new EnumField("Effect", HitboxEffect.Damage) {
                tooltip = "Effect applied to targets inside the hitbox (Damage, Heal, ...)." };
            AmountField = new FloatField("Amount") { value = 10f,
                tooltip = "Magnitude of the effect." };
            RadiusField = new FloatField("Radius") { value = 1.5f,
                tooltip = "Hitbox radius (Circle) or half-width (Capsule), in meters." };
            OffsetForwardField = new FloatField("Offset Forward") { value = 0f,
                tooltip = "How far in front of the caster the hitbox is placed (meters)." };
            DurationField = new FloatField("Duration (s, 0=instant)") { value = 0f,
                tooltip = "How long the hitbox stays active. 0 = a single instant overlap check." };
            HitIntervalField = new FloatField("Interval (s, 0=once)") { value = 0f,
                tooltip = "Re-tick interval for applying the effect over the duration. 0 = hit each target once." };
            HitSelfToggle = new Toggle("Hit Self") { value = false,
                tooltip = "Whether the hitbox can affect the caster." };
            HitAlliesToggle = new Toggle("Hit Allies") { value = false,
                tooltip = "Whether the hitbox can affect allied players." };

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
