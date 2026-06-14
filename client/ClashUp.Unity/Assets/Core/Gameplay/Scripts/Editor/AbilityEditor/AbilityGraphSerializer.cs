using System.Collections.Generic;
using System.Linq;
using ClashUp.Shared.Abilities;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    public static class AbilityGraphSerializer
    {
        private const float TickRate = 30f;

        public static AbilityDefinition SerializeGraph(AbilityGraphView graphView)
        {
            var root = graphView.nodes.ToList().OfType<RootNode>().FirstOrDefault();
            if (root == null) return null;

            var telegraph = new TelegraphConfig
            {
                Shape = root.GetMappedShape(),
                Radius = root.TelegraphRadiusField.value,
                Length = root.TelegraphLengthField.value,
                Angle = root.TelegraphAngleField.value,
                ShowDurationTicks = SecondsToTicks(root.TelegraphShowDurationField.value),
            };

            string visualGuid = null;
            var visualConfig = root.VisualConfigField.value as ClashUp.Client.Gameplay.AbilityVisualConfig;
            if (visualConfig != null)
            {
                string path = AssetDatabase.GetAssetPath(visualConfig);
                visualGuid = AssetDatabase.AssetPathToGUID(path);
            }

            return new AbilityDefinition
            {
                Id = new AbilityId(root.IdField.value),
                DisplayName = root.DisplayNameField.value,
                CooldownTicks = SecondsToTicks(root.CooldownField.value),
                ButtonIndex = root.ButtonIndexField.value,
                Telegraph = telegraph,
                RootNode = BuildChain(root.OutputPort),
                VisualConfigGuid = visualGuid,
            };
        }

        // Serialize a sequential chain starting from an output port.
        private static AbilityNode BuildChain(Port outputPort)
        {
            var edge = outputPort?.connections.FirstOrDefault();
            if (edge == null) return null;

            return edge.input.node switch
            {
                HitboxNode hb       => BuildHitbox(hb),
                ProjectileNode proj => BuildProjectile(proj),
                ParallelNode par    => BuildParallel(par),
                _ => null,
            };
        }

        private static AbilityNode BuildHitbox(HitboxNode hb)
        {
            return new AbilityNode
            {
                Type = AbilityNodeType.Hitbox,
                DelayTicks = SecondsToTicks(hb.DelayField.value),
                Next = BuildChain(hb.NextPort),
                Hitbox = new HitboxConfig
                {
                    Effect = (HitboxEffect)hb.EffectField.value,
                    Amount = hb.AmountField.value,
                    Radius = hb.RadiusField.value,
                    OffsetForward = hb.OffsetForwardField.value,
                    DurationTicks = SecondsToTicks(hb.DurationField.value),
                    HitIntervalTicks = SecondsToTicks(hb.HitIntervalField.value),
                    HitSelf = hb.HitSelfToggle.value,
                    HitAllies = hb.HitAlliesToggle.value,
                },
            };
        }

        private static AbilityNode BuildProjectile(ProjectileNode proj)
        {
            return new AbilityNode
            {
                Type = AbilityNodeType.Projectile,
                DelayTicks = SecondsToTicks(proj.DelayField.value),
                Next = BuildChain(proj.NextPort),
                Projectile = new ProjectileConfig
                {
                    Speed = proj.SpeedField.value,
                    Radius = proj.RadiusField.value,
                    MaxRange = proj.MaxRangeField.value,
                    MaxPierceCount = proj.MaxPierceField.value,
                    OnHitEffect = (HitboxEffect)proj.OnHitEffectField.value,
                    OnHitAmount = proj.OnHitAmountField.value,
                },
            };
        }

        private static AbilityNode BuildParallel(ParallelNode par)
        {
            var children = new List<AbilityNode>();
            foreach (Port port in par.outputContainer.Children().OfType<Port>())
            {
                if (port == par.NextPort) continue;
                var child = BuildChain(port);
                if (child != null) children.Add(child);
            }
            return new AbilityNode
            {
                Type = AbilityNodeType.Parallel,
                DelayTicks = SecondsToTicks(par.DelayField.value),
                Children = children.ToArray(),
                Next = BuildChain(par.NextPort),
            };
        }

        public static void DeserializeToGraph(AbilityGraphView graphView, AbilityDefinition definition)
        {
            graphView.ClearGraph();

            var root = graphView.CreateRootNode();
            root.IdField.value = definition.Id.Value ?? string.Empty;
            root.DisplayNameField.value = definition.DisplayName ?? string.Empty;
            root.CooldownField.value = TicksToSeconds(definition.CooldownTicks);
            root.ButtonIndexField.value = definition.ButtonIndex;

            if (definition.Telegraph != null)
            {
                root.SetMappedShape(definition.Telegraph.Shape);
                root.TelegraphRadiusField.value = definition.Telegraph.Radius;
                root.TelegraphLengthField.value = definition.Telegraph.Length;
                root.TelegraphAngleField.value = definition.Telegraph.Angle;
                root.TelegraphShowDurationField.value = TicksToSeconds(definition.Telegraph.ShowDurationTicks);
            }

            if (!string.IsNullOrEmpty(definition.VisualConfigGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(definition.VisualConfigGuid);
                if (!string.IsNullOrEmpty(path))
                    root.VisualConfigField.value = AssetDatabase.LoadAssetAtPath<ClashUp.Client.Gameplay.AbilityVisualConfig>(path);
            }

            if (definition.RootNode != null)
                PlaceNode(graphView, definition.RootNode, root.OutputPort, 350f, 0f);
        }

        private static void PlaceNode(AbilityGraphView graphView, AbilityNode data, Port parentOutput, float x, float y)
        {
            if (data == null) return;

            Node node = null;
            Port nextOutput = null;

            switch (data.Type)
            {
                case AbilityNodeType.Hitbox:
                {
                    var hb = graphView.CreateHitboxNode(x, y);
                    if (data.Hitbox != null)
                    {
                        hb.DelayField.value = TicksToSeconds(data.DelayTicks);
                        hb.EffectField.value = data.Hitbox.Effect;
                        hb.AmountField.value = data.Hitbox.Amount;
                        hb.RadiusField.value = data.Hitbox.Radius;
                        hb.OffsetForwardField.value = data.Hitbox.OffsetForward;
                        hb.DurationField.value = TicksToSeconds(data.Hitbox.DurationTicks);
                        hb.HitIntervalField.value = TicksToSeconds(data.Hitbox.HitIntervalTicks);
                        hb.HitSelfToggle.value = data.Hitbox.HitSelf;
                        hb.HitAlliesToggle.value = data.Hitbox.HitAllies;
                    }
                    graphView.Connect(parentOutput, hb.InputPort);
                    nextOutput = hb.NextPort;
                    node = hb;
                    break;
                }
                case AbilityNodeType.Projectile:
                {
                    var proj = graphView.CreateProjectileNode(x, y);
                    if (data.Projectile != null)
                    {
                        proj.DelayField.value = TicksToSeconds(data.DelayTicks);
                        proj.SpeedField.value = data.Projectile.Speed;
                        proj.RadiusField.value = data.Projectile.Radius;
                        proj.MaxRangeField.value = data.Projectile.MaxRange;
                        proj.MaxPierceField.value = data.Projectile.MaxPierceCount;
                        proj.OnHitEffectField.value = data.Projectile.OnHitEffect;
                        proj.OnHitAmountField.value = data.Projectile.OnHitAmount;
                    }
                    graphView.Connect(parentOutput, proj.InputPort);
                    nextOutput = proj.NextPort;
                    node = proj;
                    break;
                }
                case AbilityNodeType.Parallel:
                {
                    var par = graphView.CreateParallelNode(x, y);
                    par.DelayField.value = TicksToSeconds(data.DelayTicks);
                    graphView.Connect(parentOutput, par.InputPort);
                    if (data.Children != null)
                    {
                        for (int i = 0; i < data.Children.Length; i++)
                        {
                            Port childPort = i < par.outputContainer.IndexOf(par.NextPort)
                                ? (Port)par.outputContainer[i]
                                : par.AddChildPort();
                            PlaceNode(graphView, data.Children[i], childPort, x + 300f, y + i * 240f);
                        }
                    }
                    nextOutput = par.NextPort;
                    node = par;
                    break;
                }
            }

            if (data.Next != null && nextOutput != null)
                PlaceNode(graphView, data.Next, nextOutput, x + 300f, y);
        }

        private static int SecondsToTicks(float seconds) => Mathf.RoundToInt(seconds * TickRate);
        private static float TicksToSeconds(int ticks) => ticks / TickRate;
    }
}
