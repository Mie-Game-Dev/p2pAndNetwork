using UnityEngine;

public class NetworkCharacterStateConsistencyChecker : MonoBehaviour, PRN.IStateConsistencyChecker<NetworkPlayerState>
{
    // You need to implement this method
    // serverState is the one sent back by the server to the client
    // ownerState is the corresponding state the client predicted (they have the same tick value)
    public bool IsConsistent(NetworkPlayerState serverState, NetworkPlayerState ownerState) =>
        Vector3.Distance(serverState.position, ownerState.position) <= .01f
        && Mathf.Abs(serverState.rotationY - ownerState.rotationY) <= .1f
        && Vector3.Distance(serverState.movement, ownerState.movement) <= .01f
        && ownerState.abilitySkill == serverState.abilitySkill
        //&& ownerState.skillAbilityState == serverState.skillAbilityState
        //&& ownerState.basicAttackAbilityState == serverState.basicAttackAbilityState
        //&& ownerState.movementState == serverState.movementState
        //&& ownerState.recallAbilityState == serverState.recallAbilityState
        //&& ownerState.basicAttackTarget == serverState.basicAttackTarget
        && Mathf.Approximately(ownerState.chargeRange, serverState.chargeRange);
}
