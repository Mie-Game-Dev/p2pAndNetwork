using System;
using Unity.Netcode;
using UnityEngine;

// Implementing IEquatable for CompressedTransform
public struct CustomTransform : INetworkSerializable, IEquatable<CustomTransform>
{
    private short compressedPosX;
    private short compressedPosY;
    private short compressedPosZ;
    private byte compressedRotY;

    private const float PositionRange = 500f;
    private const float RotationRange = 360f;
    private const float CompressionFactor = 32767f;

    public Vector3 Position
    {
        get
        {
            return new Vector3(
                compressedPosX / CompressionFactor * PositionRange,
                compressedPosY / CompressionFactor * PositionRange,
                compressedPosZ / CompressionFactor * PositionRange
            );
        }
        set
        {
            compressedPosX = (short)(value.x / PositionRange * CompressionFactor);
            compressedPosY = (short)(value.y / PositionRange * CompressionFactor);
            compressedPosZ = (short)(value.z / PositionRange * CompressionFactor);
        }
    }

    public float RotationY
    {
        get
        {
            return compressedRotY / 255f * RotationRange;
        }
        set
        {
            compressedRotY = (byte)(value / RotationRange * 255f);
        }
    }

    // Implement the NetworkSerialize method for INetworkSerializable
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref compressedPosX);
        serializer.SerializeValue(ref compressedPosY);
        serializer.SerializeValue(ref compressedPosZ);
        serializer.SerializeValue(ref compressedRotY);
    }

    // Implementing IEquatable<CompressedTransform> for efficient equality checking
    public bool Equals(CustomTransform other)
    {
        return compressedPosX == other.compressedPosX &&
               compressedPosY == other.compressedPosY &&
               compressedPosZ == other.compressedPosZ &&
               compressedRotY == other.compressedRotY;
    }

    // Override the Equals method for value-type comparison
    public override bool Equals(object obj)
    {
        if (obj is CustomTransform)
        {
            return Equals((CustomTransform)obj);
        }
        return false;
    }

    // Override GetHashCode for use in collections or hashing algorithms
    public override int GetHashCode()
    {
        // Combine hash codes of fields
        return compressedPosX.GetHashCode() ^
               compressedPosY.GetHashCode() ^
               compressedPosZ.GetHashCode() ^
               compressedRotY.GetHashCode();
    }

    // Override == and != operators for easy comparison
    public static bool operator ==(CustomTransform left, CustomTransform right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CustomTransform left, CustomTransform right)
    {
        return !(left == right);
    }
}
