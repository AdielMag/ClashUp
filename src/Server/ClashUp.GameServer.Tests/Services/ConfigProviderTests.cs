using System.Text.Json;
using ClashUp.Server.Services.Matchmaking;
using ClashUp.Server.Services.Persistence;
using ClashUp.Shared.MessagePackObjects;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Services;

/// <summary>Fake config repo that records read counts so caching can be asserted.</summary>
internal sealed class FakeConfigRepository : IConfigRepository
{
    private readonly Dictionary<string, string> _docs = new();
    public int Reads { get; private set; }

    public FakeConfigRepository Set(string key, string json)
    {
        _docs[key] = json;
        return this;
    }

    public Task<ConfigDoc?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        Reads++;
        return Task.FromResult(_docs.TryGetValue(key, out var v)
            ? new ConfigDoc { Key = key, Value = v }
            : null);
    }

    public Task UpsertAsync(ConfigDoc doc, CancellationToken ct = default)
    {
        _docs[doc.Key] = doc.Value;
        return Task.CompletedTask;
    }
}

public class MatchConfigProviderTests
{
    [Fact]
    public async Task ReturnsDefault_WhenNoDocExists()
    {
        var provider = new MatchConfigProvider(new FakeConfigRepository());
        var config = await provider.GetAsync("default");
        config.Should().BeEquivalentTo(new MatchConfig());
    }

    [Fact]
    public async Task DeserializesStoredConfig()
    {
        var repo = new FakeConfigRepository().Set("match:ranked",
            JsonSerializer.Serialize(new MatchConfig { NumberOfTeams = 2, TeamSize = 3, MapId = "arena_tdm" }));
        var provider = new MatchConfigProvider(repo);

        var config = await provider.GetAsync("ranked");

        config.NumberOfTeams.Should().Be(2);
        config.TeamSize.Should().Be(3);
        config.MapId.Should().Be("arena_tdm");
    }

    [Fact]
    public async Task CachesResult_AvoidingRepeatRepositoryReads()
    {
        var repo = new FakeConfigRepository();
        var provider = new MatchConfigProvider(repo);

        await provider.GetAsync("default");
        await provider.GetAsync("default");

        repo.Reads.Should().Be(1, "the second lookup is served from the 60s cache");
    }
}

public class CharacterConfigProviderTests
{
    [Fact]
    public async Task ReturnsBakedInDefault_WhenNoDocExists()
    {
        var provider = new CharacterConfigProvider(new FakeConfigRepository());
        var config = await provider.GetAsync();
        config.DefaultCharacterId.Should().Be(CharactersConfig.Default.DefaultCharacterId);
        config.Characters.Should().HaveCount(CharactersConfig.Default.Characters.Count);
    }

    [Fact]
    public async Task DeserializesStoredRoster()
    {
        var stored = new CharactersConfig
        {
            DefaultCharacterId = "tank",
            Characters = new[] { new ClashUp.Shared.Characters.CharacterDefinition { Id = new ClashUp.Shared.Characters.CharacterId("tank"), DisplayName = "Tank" } },
        };
        var repo = new FakeConfigRepository().Set("characters:registry", JsonSerializer.Serialize(stored));
        var provider = new CharacterConfigProvider(repo);

        var config = await provider.GetAsync();

        config.DefaultCharacterId.Should().Be("tank");
        config.Characters.Should().ContainSingle(c => c.DisplayName == "Tank");
    }

    [Fact]
    public async Task CachesResult()
    {
        var repo = new FakeConfigRepository();
        var provider = new CharacterConfigProvider(repo);

        await provider.GetAsync();
        await provider.GetAsync();

        repo.Reads.Should().Be(1);
    }
}
