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

        public ProjectileNode() : base("Projectile")
        {
            titleContainer.style.backgroundColor = new Color(0.1f, 0.42f, 0.18f);

            DelayField = new FloatField("Delay (s)") { value = 0f };
            SpeedField = new FloatField("Speed") { value = 10f };
            RadiusField = new FloatField("Radius") { value = 0.2f };
            MaxRangeField = new FloatField("Max Range") { value = 15f };
            MaxPierceField = new IntegerField("Pierce (0=destroy)") { value = 0 };
            OnHitEffectField = new EnumField("On Hit Effect", HitboxEffect.Damage);
            OnHitAmountField = new FloatField("On Hit Amount") { value = 10f };

            extensionContainer.Add(DelayField);
            extensionContainer.Add(SpeedField);
            extensionContainer.Add(RadiusField);
            extensionContainer.Add(MaxRangeField);
            extensionContainer.Add(MaxPierceField);
            extensionContainer.Add(OnHitEffectField);
            extensionContainer.Add(OnHitAmountField);

            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(AbilityNode));
            InputPort.portName = "In";
            inputContainer.Add(InputPort);

            NextPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(AbilityNode));
            NextPort.portName = "Next";
            outputContainer.Add(NextPort);

            RefreshExpandedState();
            RefreshPorts();
        }
    }
}
