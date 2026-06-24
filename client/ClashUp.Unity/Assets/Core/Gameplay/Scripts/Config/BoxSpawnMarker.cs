using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Authoring marker for a breakable-box spawn point in a bake scene. The map baker records each
    /// marker's position into <see cref="ClashUp.Shared.Maps.MapData.BoxSpawns"/>; the server spawns a
    /// box there in box-based objective modes (e.g. "elimination").
    /// </summary>
    public sealed class BoxSpawnMarker : MonoBehaviour
    {
    }
}
