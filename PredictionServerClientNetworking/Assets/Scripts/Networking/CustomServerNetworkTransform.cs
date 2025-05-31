using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class CustomServerNetworkTransform : NetworkBehaviour
{
    private NetworkVariable<CustomTransform> networkTransform = new NetworkVariable<CustomTransform>();

    [SerializeField] private float syncInterval = 0.1f;
    private float syncTimer = 0f;

    [Header("Interpolation Settings")]
    [SerializeField] private float positionLerpSpeed = 5f; // Controls the speed of position interpolation
    [SerializeField] private float rotationLerpSpeed = 5f; // Controls the speed of rotation interpolation

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private float currentPositionLerpSpeed = 0f; // Controls the speed of position interpolation
    private float currentRotationLerpSpeed = 0f; // Controls the speed of rotation interpolation

    private void Start()
    {
        // Initialize target position and rotation to the current values
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    private void FixedUpdate()
    {
        if (IsServer)
        {
            syncTimer += Time.deltaTime;
            if (syncTimer >= syncInterval)
            {
                syncTimer = 0f;
                // Update the NetworkVariable with the current transform position and rotation
                CustomTransform compressedTransform = new CustomTransform
                {
                    Position = transform.position,
                    RotationY = transform.eulerAngles.y
                };
                networkTransform.Value = compressedTransform;
            }
        }
        else
        {
            // Clients will interpolate towards the target position and rotation
            InterpolateTransform();
        }
    }


    public override void OnNetworkSpawn()
    {
        if (NetworkManager.Singleton.IsClient)
        {
            Invoke(nameof(SetLerp),0.5f);
            // Subscribe to the NetworkVariable's OnValueChanged event to update target position and rotation
            networkTransform.OnValueChanged -= OnNetworkTransformChanged;
            networkTransform.OnValueChanged += OnNetworkTransformChanged;
        }
    }

    private void SetLerp()
    {
        currentPositionLerpSpeed = positionLerpSpeed;
        currentRotationLerpSpeed = rotationLerpSpeed;
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from the event when the object is disabled to avoid memory leaks
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            currentPositionLerpSpeed = 0;
            currentRotationLerpSpeed = 0;
            // Subscribe to the NetworkVariable's OnValueChanged event to update target position and rotation
            networkTransform.OnValueChanged -= OnNetworkTransformChanged;
        }
    }

    // Called when the network transform value changes
    private void OnNetworkTransformChanged(CustomTransform oldValue, CustomTransform newValue)
    {
        // Update target position and rotation
        targetPosition = newValue.Position;
        targetRotation = Quaternion.Euler(0, newValue.RotationY, 0);
    }

    // Interpolate towards the target position and rotation
    private void InterpolateTransform()
    {
        // Interpolate the position
        transform.position = Vector3.Lerp(transform.position, targetPosition, currentPositionLerpSpeed * Time.deltaTime);
        // Interpolate the rotation
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, currentRotationLerpSpeed * Time.deltaTime);
    }

    [ServerRpc]
    public void MoveServerRpc(Vector3 newPosition, float newRotationY)
    {
        if (IsServer)
        {
            transform.position = newPosition;
            transform.rotation = Quaternion.Euler(0, newRotationY, 0);
        }
    }
}
