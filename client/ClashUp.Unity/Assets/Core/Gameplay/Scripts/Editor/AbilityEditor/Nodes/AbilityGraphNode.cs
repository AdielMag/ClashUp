using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace ClashUp.Client.Gameplay.Editor.AbilityEditor
{
    public abstract class AbilityGraphNode : Node
    {
        protected AbilityGraphNode(string title)
        {
            this.title = title;
        }
    }
}
