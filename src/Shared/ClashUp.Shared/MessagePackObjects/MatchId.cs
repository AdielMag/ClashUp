using System;

using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public readonly struct MatchId : IEquatable<MatchId>
    {
        [Key(0)]
        public string Value { get; }

        public MatchId(string value) => Value = value;

        public bool Equals(MatchId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is MatchId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value ?? string.Empty;
    }
}
