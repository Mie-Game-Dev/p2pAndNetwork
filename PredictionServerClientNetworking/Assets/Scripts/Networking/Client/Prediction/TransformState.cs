using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TransformState : INetworkSerializable, IEquatable<TransformState>
{
    public int Tick;
    public Vector3 Position;
    public Quaternion Rotation;
    public bool HasStartedMoving;

    public bool Equals(TransformState other)
    {
        throw new NotImplementedException();
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            var reader = serializer.GetFastBufferReader();
            reader.ReadValueSafe(out Tick);
            reader.ReadValueSafe(out Position);
            reader.ReadValueSafe(out Rotation);
            reader.ReadValueSafe(out HasStartedMoving);
        }
        else
        {
            var reader = serializer.GetFastBufferWriter();
            reader.WriteValueSafe(Tick);
            reader.WriteValueSafe(Position);
            reader.WriteValueSafe(Rotation);
            reader.WriteValueSafe(HasStartedMoving);
        }
    }
}
