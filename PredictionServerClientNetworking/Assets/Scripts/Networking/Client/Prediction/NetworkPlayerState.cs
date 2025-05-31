using System;
using Unity.Netcode;
using UnityEngine;

public struct NetworkPlayerState : PRN.IState, INetworkSerializable, IEquatable<NetworkPlayerState>
{
    public uint tick; // 4 bytes
    public Vector3 position; // 2 bytes (quantized x, z)
    public float rotationY; // 1 byte
    public Vector3 movement; // 2 bytes (quantized x, z)
    public byte abilitySkill; // 1 byte
    //public byte basicAttackTarget; // 1 byte
    public byte chargeRange; // 1 byte
                             //public BasicAttackAbilityState basicAttackAbilityState; // 1 byte
                             //public SkillAbilityState skillAbilityState; // 1 byte
                             //public MovementState movementState; // 1 byte
                             // public RecallAbilityState recallAbilityState; // 1 byte

    public void SetTick(int tick) => this.tick = (uint)tick; // Handle larger tick values
    public int GetTick() => (int)tick;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            var reader = serializer.GetFastBufferReader();
            reader.ReadValueSafe(out tick);

            // Deserialization for position (only x and z components)
            reader.ReadValueSafe(out position);

            // Deserialization for rotation (only y component)
            reader.ReadValueSafe(out rotationY);

            // Deserialization for movement (only x and z components)
            reader.ReadValueSafe(out movement);

            reader.ReadValueSafe(out abilitySkill);
            //reader.ReadValueSafe(out basicAttackTarget);

            // Deserialization for chargeRange
            reader.ReadValueSafe(out chargeRange);
            chargeRange = (byte)DequantizeByte(chargeRange, 0f, 100f);

            // Deserialize enums as bytes
            //reader.ReadValueSafe(out basicAttackAbilityState);
            //reader.ReadValueSafe(out skillAbilityState);
            //reader.ReadValueSafe(out movementState);
            //reader.ReadValueSafe(out recallAbilityState);
        }
        else
        {
            var writer = serializer.GetFastBufferWriter();
            writer.WriteValueSafe(tick);

            // Serialization for position (only x and z components)
            writer.WriteValueSafe(position);

            // Serialization for rotation (only y component)
            writer.WriteValueSafe(rotationY);

            // Serialization for movement (only x and z components)
            writer.WriteValueSafe(movement);

            writer.WriteValueSafe(abilitySkill);
            //writer.WriteValueSafe(basicAttackTarget);

            // Serialization for chargeRange
            writer.WriteValueSafe(QuantizeFloat(chargeRange, 0f, 100f));

            // Serialize enums as bytes
            //writer.WriteValueSafe(basicAttackAbilityState);
            //writer.WriteValueSafe(skillAbilityState);
            //writer.WriteValueSafe(movementState);
            //writer.WriteValueSafe(recallAbilityState);
        }
    }

    public bool Equals(NetworkPlayerState other)
    {
        return other.abilitySkill == abilitySkill
            && other.tick == tick
            && other.position == position
            && Mathf.Approximately(other.rotationY, rotationY)
            && other.movement == movement
            && Mathf.Approximately(other.chargeRange, chargeRange);
            //&& other.basicAttackTarget == basicAttackTarget
            //&& other.basicAttackAbilityState == basicAttackAbilityState
            //&& other.skillAbilityState == skillAbilityState
            //&& other.movementState == movementState
            //&& other.recallAbilityState == recallAbilityState;
    }

    // Quantize a float to a byte
    private static byte QuantizeFloat(float value, float minValue, float maxValue)
    {
        float normalized = (value - minValue) / (maxValue - minValue);
        return (byte)(normalized * 255);
    }

    // Dequantize a byte to a float
    private static float DequantizeByte(byte value, float minValue, float maxValue)
    {
        float normalized = value / 255.0f;
        return normalized * (maxValue - minValue) + minValue;
    }
}