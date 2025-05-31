using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkMovement : NetworkBehaviour
{
    [SerializeField] ServerCharacterMovement serverCharacterMovement;

    private int tick = 0;
    private float tickRate = 1 / 60f;
    private float tickDeltaTime = 0f;

    private const int BufferSize = 1024;

    private InputState[] inputStates = new InputState[BufferSize]; // input storage
    private TransformState[] transformStates = new TransformState[BufferSize]; // transform storage

    // lastest transform from server
    public NetworkVariable<TransformState> ServerTransformState = new NetworkVariable<TransformState>();
    private TransformState previousTransformStates;

    private void OnEnable()
    {
        ServerTransformState.OnValueChanged -= OnServerStateChange;
        ServerTransformState.OnValueChanged += OnServerStateChange;
    }

    private void OnServerStateChange(TransformState previousValue, TransformState newValue)
    {
        previousTransformStates = previousValue;
    }

    // local player
    public void ProcessLocalPlayerMovement(Vector3 movementInput)
    {
        tickDeltaTime += Time.deltaTime;

        // check if we have exceed tick rate to send input form local
        if (tickDeltaTime > tickRate)
        {
            int bufferIndex = tick % BufferSize;

            // client side
            if (!IsServer)
            {
                // send to server our movement and move use network transform
                MovePlayerServerRpc(tick, movementInput);
                // move locally instant without need to wait for server move using network transform
                MovePlayer(movementInput);
            }
            else
            {
                MovePlayerDirect(tick, movementInput);
            }


            InputState inputState = new InputState()
            {
                Tick = tick,
                moveInput = movementInput,
            };

            TransformState transformState = new TransformState()
            {
                Tick = tick,
                Position = transform.position,
                Rotation = transform.rotation,
                HasStartedMoving = true
            };

            inputStates[bufferIndex] = inputState;
            transformStates[bufferIndex] = transformState;

            tickDeltaTime -= tickRate;
            tick++;
        }
    }

    // it will be process in other player
    public void ProcessSimulatedPlayerMovement()
    {
        tickDeltaTime += Time.deltaTime;
        if(tickDeltaTime > tickRate)
        {
            if(ServerTransformState.Value.HasStartedMoving)
            {
                transform.position = ServerTransformState.Value.Position;
                transform.rotation = ServerTransformState.Value.Rotation;  
            }

            tickDeltaTime -= tickRate;
            tick++;
        }
    }

    [ServerRpc]
    private void MovePlayerServerRpc(int tick, Vector3 movementInput)
    {
        MovePlayerDirect(tick, movementInput);
    }

    private void MovePlayerDirect(int tick, Vector3 movementInput)
    {
        MovePlayer(movementInput);

        TransformState transformState = new TransformState()
        {
            Tick = tick,
            Position = transform.position,
            Rotation = transform.rotation,
            HasStartedMoving = true
        };

        previousTransformStates = ServerTransformState.Value;
        ServerTransformState.Value = transformState;
    }

    private void MovePlayer(Vector3 movementInput)
    {
        // pass to navmesh move
       // serverCharacterMovement.SetMoveDirection(movementInput, tickRate);
    }
}
