using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Shared;

public class CharacterCatalogTests
{
    [Fact]
    public void Get_ReturnsRequestedCharacter()
    {
        var catalog = CharacterCatalog.FromConfig(CharactersConfig.Default);

        catalog.Get(new CharacterId("mage")).DisplayName.Should().Be("Mage");
        catalog.Get(new CharacterId("brawler")).DisplayName.Should().Be("Brawler");
    }

    [Fact]
    public void Get_UnknownId_FallsBackToDefault()
    {
        var catalog = CharacterCatalog.FromConfig(CharactersConfig.Default);

        catalog.Get(new CharacterId("does-not-exist")).DisplayName.Should().Be("Brawler");
        catalog.DefaultId.Value.Should().Be("brawler");
        catalog.Default.DisplayName.Should().Be("Brawler");
    }

    [Fact]
    public void Get_DefaultStructWithNullValue_FallsBackToDefault()
    {
        var catalog = CharacterCatalog.FromConfig(CharactersConfig.Default);
        catalog.Get(default).DisplayName.Should().Be("Brawler");
    }

    [Fact]
    public void NullConfig_UsesBakedInDefaults()
    {
        var catalog = new CharacterCatalog(null!);
        catalog.DefaultId.Value.Should().Be("brawler");
        catalog.Get(new CharacterId("mage")).DisplayName.Should().Be("Mage");
    }

    [Fact]
    public void CustomConfig_IsRespected()
    {
        var config = new CharactersConfig
        {
            DefaultCharacterId = "hero",
            Characters = new[]
            {
                new CharacterDefinition { Id = new CharacterId("hero"), DisplayName = "Hero" },
            },
        };
        var catalog = CharacterCatalog.FromConfig(config);

        catalog.DefaultId.Value.Should().Be("hero");
        catalog.Get(new CharacterId("hero")).DisplayName.Should().Be("Hero");
        catalog.Get(new CharacterId("anything")).DisplayName.Should().Be("Hero", "unknown ids fall back to the configured default");
    }
}
