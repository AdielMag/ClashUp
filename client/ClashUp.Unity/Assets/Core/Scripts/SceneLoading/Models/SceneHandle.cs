using UnityEngine.SceneManagement;

namespace ClashUp.Client.Core
{
    public readonly struct SceneHandle
    {
        public Scene Scene { get; }
        public bool IsValid => Scene.IsValid();

        public SceneHandle(Scene scene)
        {
            Scene = scene;
        }
    }
}
