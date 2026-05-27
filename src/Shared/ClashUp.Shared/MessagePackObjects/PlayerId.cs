using MessagePack;

namespace ClashUp.Shared.MessagePackObjects;

[MessagePackObject]
public readonly struct PlayerId : IEquatable<PlayerId>
{
    [Key(0)]
    public string Value { get; }

    public PlayerId(string value) => Value = value;

    public bool Equals(PlayerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is PlayerId other && Equals(other);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    public override string ToString() => Value ?? string.Empty;
}
