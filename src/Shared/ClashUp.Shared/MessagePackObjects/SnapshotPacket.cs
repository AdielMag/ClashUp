using System;
using MessagePack;

namespace ClashUp.Shared.MessagePackObjects;

/// <summary>
/// MessagePack envelope around AetherNet's binary delta blob. The
/// receiver applies it against its baseline at <see cref="BaselineTick"/>.
/// </summary>
[MessagePackObject]
public sealed class SnapshotPacket
{
    [Key(0)] public int Tick { get; init; }
    [Key(1)] public long ServerStampMs { get; init; }
    [Key(2)] public int BaselineTick { get; init; }
    [Key(3)] public ReadOnlyMemory<byte> DeltaBlob { get; init; }
}
