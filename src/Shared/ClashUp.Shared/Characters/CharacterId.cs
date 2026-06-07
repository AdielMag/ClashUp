using System;
using MessagePack;

namespace ClashUp.Shared.Characters
{
    [MessagePackObject]
    public readonly struct CharacterId : IEquatable<CharacterId>
    {
        [Key(0)]
        public string Value { get; }

        public CharacterId(string value) => Value = value;

        public bool Equals(CharacterId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CharacterId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value ?? string.Empty;
    }
}
