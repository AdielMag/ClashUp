using System;
using MessagePack;

namespace ClashUp.Shared.Abilities
{
    [MessagePackObject]
    public readonly struct AbilityId : IEquatable<AbilityId>
    {
        [Key(0)] public string Value { get; init; }

        public AbilityId(string value) => Value = value;

        public bool Equals(AbilityId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AbilityId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode(StringComparison.Ordinal) ?? 0;
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(AbilityId a, AbilityId b) => a.Equals(b);
        public static bool operator !=(AbilityId a, AbilityId b) => !a.Equals(b);
    }
}
