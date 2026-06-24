using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.GameServer;

public class MatchCharactersHolderTests
{
    [Fact]
    public void DefaultsToBakedInRoster_BeforeInitialize()
    {
        var holder = new MatchCharactersHolder();
        holder.Catalog.Get(new CharacterId("brawler")).DisplayName.Should().Be("Brawler");
    }

    [Fact]
    public void Initialize_NullConfig_FallsBackToDefault()
    {
        var holder = new MatchCharactersHolder();
        holder.Initialize(null!);
        holder.Catalog.DefaultId.Value.Should().Be("brawler");
    }

    [Fact]
    public void Initialize_CustomConfig_ReplacesCatalog()
    {
        var holder = new MatchCharactersHolder();
        holder.Initialize(new CharactersConfig
        {
            DefaultCharacterId = "ninja",
            Characters = new[] { new CharacterDefinition { Id = new CharacterId("ninja"), DisplayName = "Ninja" } },
        });

        holder.Catalog.DefaultId.Value.Should().Be("ninja");
        holder.Catalog.Get(new CharacterId("ninja")).DisplayName.Should().Be("Ninja");
    }
}
