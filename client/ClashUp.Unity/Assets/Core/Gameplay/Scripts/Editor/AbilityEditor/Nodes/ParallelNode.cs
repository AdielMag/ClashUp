using ClashUp.Shared.Abilities;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    public sealed class ParallelNode : AbilityGraphNode
    {
        public Port InputPort;
        public Port NextPort;
        public FloatField DelayField;

        public ParallelNode() : base("Parallel")
        {
            titleContainer.style.backgroundColor = new Color(0.32f, 0.13f, 0.5f);
            DelayField = new FloatField("Delay (s)") { value = 0f };
            extensionContainer.Add(DelayField);

            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(AbilityNode));
            InputPort.portName = "In";
            inputContainer.Add(InputPort);

            AddChildPort();

            NextPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(AbilityNode));
            NextPort.portName = "Next";
            outputContainer.Add(NextPort);

            this.AddManipulator(new ContextualMenuManipulator(evt =>
                evt.menu.AppendAction("Add Child Port", _ => AddChildPort())));

            RefreshExpandedState();
            RefreshPorts();
        }

        public Port AddChildPort()
        {
            var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(AbilityNode));
            port.portName = $"Child {outputContainer.childCount}";
            // Insert before NextPort so Next always stays last
            int insertIndex = outputContainer.childCount;
            if (NextPort != null) insertIndex = outputContainer.IndexOf(NextPort);
            outputContainer.Insert(insertIndex, port);
            RefreshPorts();
            return port;
        }
    }
}
