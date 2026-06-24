namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Holds the active match's objective type (from <c>JoinResult.ObjectiveType</c>), set once by
    /// the match runner after join. HUD/screen systems read it to decide what mode-specific UI to show.
    /// </summary>
    public sealed class MatchModeHolder
    {
        public string ObjectiveType { get; private set; } = "survival";

        public bool IsElimination => ObjectiveType == "elimination";

        public void Initialize(string objectiveType)
        {
            ObjectiveType = string.IsNullOrEmpty(objectiveType) ? "survival" : objectiveType;
        }
    }
}
