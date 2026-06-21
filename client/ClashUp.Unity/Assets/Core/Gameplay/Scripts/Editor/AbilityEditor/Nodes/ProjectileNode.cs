using ClashUp.Shared.Abilities;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    public sealed class ProjectileNode : AbilityGraphNode
    {
        public Port InputPort;
        public Port NextPort;
        public FloatField DelayField;
        public FloatField SpeedField;
        public FloatField RadiusField;
        public FloatField MaxRangeField;
        public IntegerField MaxPierceField;
        public EnumField OnHitEffectField;
        public FloatField OnHitAmountField;
        public FloatField AoeRadiusField;
        public FloatField AoeAmountField;
        public EnumField AoeEffectField;
        public FloatField LifetimeField;

        public ProjectileNode() : base("Projectile", NodeCategory.Action)
        {
            DelayField = new FloatField("Delay (s)") { value = 0f };
            SpeedField = new FloatField("Speed") { value = 10f };
            RadiusField = new FloatField("Radius") { value = 0.2f };
            MaxRangeField = new FloatField("Max Range") { value = 15f };
            MaxPierceField = new IntegerField("Pierce (0=destroy)") { value = 0 };
            OnHitEffectField = new EnumField("On Hit Effect", HitboxEffect.Damage);
            OnHitAmountField = new FloatField("On Hit Amount") { value = 10f };
            AoeRadiusField = new FloatField("AoE Radius (0=single)") { value = 0f };
            AoeAmountField = new FloatField("AoE Amount") { value = 0f };
            AoeEffectField = new EnumField("AoE Effect", HitboxEffect.Damage);
            LifetimeField = new FloatField("Lifetime (s, 0=auto)") { value = 0f };

            extensionContainer.Add(DelayField);
            extensionContainer.Add(SpeedField);
            extensionContainer.Add(RadiusField);
            extensionContainer.Add(MaxRangeField);
            extensionContainer.Add(MaxPierceField);
            extensionContainer.Add(OnHitEffectField);
            extensionContainer.Add(OnHitAmountField);
            extensionContainer.Add(AoeRadiusField);
            extensionContainer.Add(AoeAmountField);
            extensionContainer.Add(AoeEffectField);
            extensionContainer.Add(LifetimeField);

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
