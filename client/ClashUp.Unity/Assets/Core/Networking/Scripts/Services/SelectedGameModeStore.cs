namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Holds the game mode the player picked in the lobby (the matchmaking <c>ModeId</c>). Registered
    /// in the CoreStarter scope so the choice survives Lobby → Matchmaking → Match scene transitions;
    /// read when the client builds its <see cref="ClashUp.Shared.MessagePackObjects.QueueRequest"/>.
    /// </summary>
    public sealed class SelectedGameModeStore
    {
        public string Selected { get; set; } = "default";
    }
}
