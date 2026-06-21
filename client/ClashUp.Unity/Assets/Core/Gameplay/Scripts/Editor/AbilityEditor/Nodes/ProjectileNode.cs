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
            DelayField = new FloatField("Delay (s)") { value = 0f,
                tooltip = "Delay before this projectile is fired, measured from when the previous node completes." };
            SpeedField = new FloatField("Speed") { value = 10f,
                tooltip = "Projectile travel speed (meters/second)." };
            RadiusField = new FloatField("Radius") { value = 0.2f,
                tooltip = "Projectile collision radius (meters)." };
            MaxRangeField = new FloatField("Max Range") { value = 15f,
                tooltip = "Maximum distance the projectile travels before expiring (meters)." };
            MaxPierceField = new IntegerField("Pierce (0=destroy)") { value = 0,
                tooltip = "How many targets the projectile passes through. 0 = destroyed on the first hit." };
            OnHitEffectField = new EnumField("On Hit Effect", HitboxEffect.Damage) {
                tooltip = "Effect applied to a directly-hit target (Damage, Heal, ...)." };
            OnHitAmountField = new FloatField("On Hit Amount") { value = 10f,
                tooltip = "Magnitude of the On Hit Effect." };
            AoeRadiusField = new FloatField("AoE Radius (0=single)") { value = 0f,
                tooltip = "Explosion radius on impact (meters). 0 = single-target, no area effect." };
            AoeAmountField = new FloatField("AoE Amount") { value = 0f,
                tooltip = "Magnitude of the area-of-effect applied within AoE Radius on impact." };
            AoeEffectField = new EnumField("AoE Effect", HitboxEffect.Damage) {
                tooltip = "Effect type applied to everyone inside the AoE radius." };
            LifetimeField = new FloatField("Lifetime (s, 0=auto)") { value = 0f,
                tooltip = "Seconds before the projectile self-destructs. 0 = auto-computed from Max Range / Speed." };

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
