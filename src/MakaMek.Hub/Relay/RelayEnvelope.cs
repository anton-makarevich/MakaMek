namespace Sanet.MakaMek.Hub.Relay;

/// <summary>
/// Opaque envelope fanned out by the relay hub. The hub never deserializes <see cref="Payload"/>.
/// </summary>
public sealed record RelayEnvelope(
    string SenderId,
    string Payload,
    string SchemaVersion,
    long SequenceNumber,
    DateTime Timestamp);
