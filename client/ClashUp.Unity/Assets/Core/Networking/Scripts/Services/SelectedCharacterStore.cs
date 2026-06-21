using ClashUp.Shared.Characters;

namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Holds the character the player picked before matchmaking. Registered in the CoreStarter scope
    /// so the choice survives the Lobby → Matchmaking → Match scene transitions; it is read when the
    /// client builds its <see cref="ClashUp.Shared.MessagePackObjects.MatchJoinRequest"/>.
    /// </summary>
    public sealed class SelectedCharacterStore
    {
        public CharacterId Selected { get; set; } = new CharacterId("brawler");
    }
}
