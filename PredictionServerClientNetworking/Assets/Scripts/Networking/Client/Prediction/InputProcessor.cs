using System;
using Unity.Netcode;
using UnityEngine;

public class InputProcessor : NetworkBehaviour, PRN.IProcessor<NetworkPlayerInput, NetworkPlayerState>
{
    [SerializeField] private ServerCharacterMovement serverCharacterMovement;
    [SerializeField] private CharacterAbility characterAbility;
    [SerializeField] private CharacterRecallAbility characterRecallAbility;
    [SerializeField] private BasicAttack basicAttack;
    [SerializeField] private float lerpSpeed = 0.001f;

    public override void OnNetworkSpawn()
    {
        if (serverCharacterMovement == null)
        {
            serverCharacterMovement = GetComponent<ServerCharacterMovement>();
        }

        if (characterAbility == null)
        {
            characterAbility = GetComponent<CharacterAbility>();
        }

        if (basicAttack == null)
        {
            basicAttack = GetComponent<BasicAttack>();
        }

        if (characterRecallAbility == null)
        {
            characterRecallAbility = GetComponent<CharacterRecallAbility>();
        }
    }

    private NetworkPlayerState lastSentState;
    private float throttleInterval = 0.1f; // Initial default value
    private float throttleTimer = 0f;
    private int lastPingStatus = -1; // Track ping status to avoid unnecessary updates

    public NetworkPlayerState Process(NetworkPlayerInput input, TimeSpan deltaTime)
    {
        if (IsClient && ClientSingleton.Instance.ClientGameManager.userData.userGamePreferences.userRole == E_LobbyRoles.PLAYER)
        {
            // Get the current ping status
            int currentPingStatus = GameCalculationUtils.GetPingStatus();

            // Only update throttle if the ping status has changed
            if (currentPingStatus != lastPingStatus)
            {
                // Update the throttleInterval based on ping status ranges
                switch (currentPingStatus)
                {
                    case int ping when ping >= 0 && ping <= 50:
                        throttleInterval = 0f;
                        break;
                    case int ping when ping >= 51 && ping <= 100:
                        throttleInterval = 0.05f;
                        break;
                    case int ping when ping >= 101 && ping <= 180:
                        throttleInterval = 0.1f;
                        break;
                    case int ping when ping >= 181:
                        throttleInterval = float.MaxValue;
                        break;
                }

                // Update the last ping status to the current
                lastPingStatus = currentPingStatus;
            }


            // Throttle updates based on interval
            throttleTimer += (float)deltaTime.TotalSeconds;

            if (throttleTimer <= throttleInterval)
            {
                return lastSentState;
            }
            else
            {
                throttleTimer = 0f; // Reset timer
            }
        }

        // Process inputs for movement, abilities, and other actions
        serverCharacterMovement.Process(input, deltaTime);
        characterAbility.Process(input);
        characterRecallAbility.Process(input);
        basicAttack.Process(input);

        // Create the new state
        var newState = new NetworkPlayerState()
        {
            position = transform.position,
            rotationY = transform.rotation.eulerAngles.y,
            movement = serverCharacterMovement.movement,
            abilitySkill = (byte)characterAbility.currentAbilitySkill,
            //basicAttackTarget = (byte)basicAttack.targetBasicAttack,
            chargeRange = (byte)characterAbility.currentChargeRange
        };

        // Only update if state has changed significantly (delta compression)
        if (!newState.Equals(lastSentState))
        {
            lastSentState = newState;
            return newState;
        }

        // If no update is needed, return the last sent state
        return lastSentState;
    }



    public void Rewind(NetworkPlayerState state)
    {
        if (characterAbility.abilities[state.abilitySkill].SkillType == E_SkillTypeIndicator.Charging)
        {
            if (state.chargeRange > 0)
            {
                characterAbility.currentChargeRange = state.chargeRange;
            }
        }

        if(OwnerClientId != 0)
        {
            // Update position and rotation
            serverCharacterMovement.NavMeshAgent.enabled = false;
        }

        if (GameManager.instance && GameManager.instance.GameState.Value != E_GameState.Ongoing)
        {
            // Lerp the position
            transform.position = Vector3.Lerp(
                transform.position,
                state.position,
                Time.fixedDeltaTime * lerpSpeed * 0.01f);

            // Lerp the rotation
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.Euler(0, state.rotationY, 0),
                Time.fixedDeltaTime * lerpSpeed * 0.1f);

            serverCharacterMovement.movement = Vector3.Lerp(
                serverCharacterMovement.movement,
               state.movement,
                Time.fixedDeltaTime * lerpSpeed * 0.01f);
        }
        else
        {
            transform.position =state.position;
            transform.rotation = Quaternion.Euler(0, state.rotationY, 0);
            serverCharacterMovement.movement = state.movement;
        }

        if (OwnerClientId != 0)
        {
            serverCharacterMovement.NavMeshAgent.enabled = true;
        }
    }
}
