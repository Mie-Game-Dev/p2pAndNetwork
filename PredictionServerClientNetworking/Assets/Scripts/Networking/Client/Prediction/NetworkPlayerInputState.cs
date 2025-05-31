using System;
using Unity.Netcode;

public struct NetworkPlayerInputState : INetworkSerializable, IEquatable<NetworkPlayerInputState>
{
    public NetworkPlayerInput input;
    public NetworkPlayerState state;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            var reader = serializer.GetFastBufferReader();
            reader.ReadValueSafe(out input);
            reader.ReadValueSafe(out state);
        }
        else
        {
            var writer = serializer.GetFastBufferWriter();
            writer.WriteValueSafe(input);
            writer.WriteValueSafe(state);
        }
    }

    public bool Equals(NetworkPlayerInputState other)
    {
        // Assuming NetworkPlayerInput and NetworkPlayerState also implement IEquatable
        return input.Equals(other.input) && state.Equals(other.state);
    }

    public override bool Equals(object obj)
    {
        if (obj is NetworkPlayerInputState other)
        {
            return Equals(other);
        }
        return false;
    }

    public override int GetHashCode()
    {
        unchecked // Overflow is fine, just wrap
        {
            int hash = 17;
            hash = hash * 23 + input.GetHashCode();
            hash = hash * 23 + state.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(NetworkPlayerInputState left, NetworkPlayerInputState right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NetworkPlayerInputState left, NetworkPlayerInputState right)
    {
        return !(left == right);
    }
}
