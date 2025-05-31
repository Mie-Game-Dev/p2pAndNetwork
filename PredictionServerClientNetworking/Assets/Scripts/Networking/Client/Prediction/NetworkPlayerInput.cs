using System;
using Unity.Netcode;
using UnityEngine;

[System.Serializable]
public struct NetworkPlayerInput : PRN.IInput, INetworkSerializable, IEquatable<NetworkPlayerInput>
{
    public uint tick; // 4 bytes
    public float forward; // 1 bytes
    public float right; // 1 bytes
    public byte abilitySkill; // 1 byte
    public byte basicAttackTarget; // 1 byte
    public byte chargeRange; // 1 byte
    public BasicAttackAbilityState basicAttackAbilityState; // 1 byte
    public SkillAbilityState skillAbilityState; // 1 byte
    public MovementState movementState; // 1 byte
    public RecallAbilityState recallAbilityState; // 1 byte

    public void SetTick(int tick) => this.tick = (uint)tick; // Handle larger tick values
    public int GetTick() => (int)tick;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            var reader = serializer.GetFastBufferReader();
            reader.ReadValueSafe(out tick);

            // Deserialize floats
            reader.ReadValueSafe(out forward);
            reader.ReadValueSafe(out right);

            reader.ReadValueSafe(out abilitySkill);
            reader.ReadValueSafe(out basicAttackTarget);

            reader.ReadValueSafe(out chargeRange);

            // Deserialize enums as bytes
            reader.ReadValueSafe(out basicAttackAbilityState);
            reader.ReadValueSafe(out skillAbilityState);
            reader.ReadValueSafe(out movementState);
            reader.ReadValueSafe(out recallAbilityState);
        }
        else
        {
            var writer = serializer.GetFastBufferWriter();
            writer.WriteValueSafe(tick);

            // Serialize floats
            writer.WriteValueSafe(forward);
            writer.WriteValueSafe(right);

            writer.WriteValueSafe(abilitySkill);
            writer.WriteValueSafe(basicAttackTarget);

            writer.WriteValueSafe(chargeRange);

            // Serialize enums as bytes
            writer.WriteValueSafe(basicAttackAbilityState);
            writer.WriteValueSafe(skillAbilityState);
            writer.WriteValueSafe(movementState);
            writer.WriteValueSafe(recallAbilityState);
        }
    }

    public bool Equals(NetworkPlayerInput other)
    {
        return other.abilitySkill == abilitySkill
            && other.tick == tick
            && Mathf.Approximately(other.forward, forward)
            && Mathf.Approximately(other.right, right)
            && other.basicAttackTarget == basicAttackTarget
            && Mathf.Approximately(other.chargeRange, chargeRange)
            && other.basicAttackAbilityState == basicAttackAbilityState
            && other.skillAbilityState == skillAbilityState
            && other.movementState == movementState
            && other.recallAbilityState == recallAbilityState;
    }
}
