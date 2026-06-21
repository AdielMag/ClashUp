using ClashUp.Shared.Abilities;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    /// <summary>
    /// Spawn node — added to prove the visual system is extensible. It picks up
    /// the full restyle (stripe / icon chip / category tag / port colour / field
    /// styling) automatically just by declaring its <see cref="NodeCategory"/>;
    /// no new USS was written for it.
    ///
    /// NOTE: this is a visual placeholder. The shared data model
    /// (AbilityNodeType) has no Spawn entry yet, so the serializer simply ignores
    /// a Spawn node — behaviour, data model and JSON are unchanged. Wire it into
    /// AbilityNode + AbilityGraphSerializer when the runtime gains Spawn support.
    /// </summary>
    public sealed class SpawnNode : AbilityGraphNode
    {
        public Port InputPort;
        public Port NextPort;
        public TextField PrefabIdField;
        public IntegerField CountField;
        public FloatField SpreadField;

        public SpawnNode() : base("Spawn", NodeCategory.Spawn)
        {
            PrefabIdField = new TextField("Prefab Id") { value = "" };
            CountField = new IntegerField("Count") { value = 1 };
            SpreadField = new FloatField("Spread") { value = 0f };

            extensionContainer.Add(PrefabIdField);
            extensionContainer.Add(CountField);
            extensionContainer.Add(SpreadField);

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
