using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    private InputSystem_Actions inputActions;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float rotationSmoothTime = 0.05f;
    private float currentVelocity;

    [Header("Combat & Health")]
    public NetworkVariable<int> Health = new NetworkVariable<int>(100);
    [SerializeField] private Transform holdPosition;
    [SerializeField] private float maxChargeTime = 3f;
    [SerializeField] private float maxThrowForce = 25f;
    [SerializeField] private float minThrowForce = 5f;

    private float currentChargeTime = 0f;
    private bool isCharging = false;
    private GameObject heldBall; // Only tracked on Server
    private Collider heldBallCollider;

    [Header("Camera Settings")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.5f, -3.5f);
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float verticalMin = -30f;
    [SerializeField] private float verticalMax = 60f;

    private float camRotationX = 0f;
    private float camRotationY = 0f;

    [Header("References")]
    [SerializeField] private Material[] playerMaterials;
    private Rigidbody rb;
    private Camera playerCamera;

    private Vector2 serverInputVector;
    private float serverCameraYRotation;
    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            inputActions = new InputSystem_Actions();
            inputActions.Enable();

            inputActions.Player.Jump.performed += _ => JumpServerRpc();
            inputActions.Player.Interact.performed += _ => TryPickupBallServerRpc();
            inputActions.Player.Attack.started += _ => StartChargeServerRpc();
            inputActions.Player.Attack.canceled += _ => ReleaseThrowServerRpc();

            playerCamera = Camera.main;
            playerCamera.transform.SetParent(null);
            Cursor.lockState = CursorLockMode.Locked;
        }

        SetupPlayerVisuals();
        SpawnCharacterAtSpawnPoint();
    }

    private void Update()
    {
        if (IsOwner)
        {
            HandleCameraRotation();
            Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();
            MoveServerRpc(input, playerCamera.transform.eulerAngles.y);
        }

        if (IsServer)
        {
            CheckGrounded();
            MovePlayer();
            HandleCharging();
        }
    }

    private void HandleCameraRotation()
    {
        if (playerCamera == null) return;

        Vector2 mouseDelta = inputActions.Player.Look.ReadValue<Vector2>() * mouseSensitivity;

        camRotationY += mouseDelta.x;
        camRotationX -= mouseDelta.y;
        camRotationX = Mathf.Clamp(camRotationX, verticalMin, verticalMax);

        Quaternion rotation = Quaternion.Euler(camRotationX, camRotationY, 0);

        playerCamera.transform.position = transform.position + (rotation * cameraOffset);
        playerCamera.transform.LookAt(transform.position + Vector3.up * 1.5f);
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2 input, float cameraYRotation)
    {
        serverInputVector = input;
        serverCameraYRotation = cameraYRotation;
    }

    [ServerRpc]
    private void JumpServerRpc()
    {
        if (isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void MovePlayer()
    {
        Vector3 forward = Quaternion.Euler(0, serverCameraYRotation, 0) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0, serverCameraYRotation, 0) * Vector3.right;

        forward.y = 0;
        right.y = 0;

        Vector3 moveDir = (forward.normalized * serverInputVector.y + right.normalized * serverInputVector.x).normalized;

        if (moveDir.sqrMagnitude > 0.01f)
        {
            Vector3 nextPosition = rb.position + moveDir * moveSpeed * Time.deltaTime;
            rb.MovePosition(nextPosition);

            float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(rb.rotation.eulerAngles.y, targetAngle, ref currentVelocity, rotationSmoothTime);
            rb.MoveRotation(Quaternion.Euler(0, angle, 0));
        }
    }

    private void CheckGrounded()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }

    private void SetupPlayerVisuals()
    {
        Transform arrowTransform = transform.Find("Arrow");
        if (arrowTransform != null && playerMaterials.Length > 0)
        {
            uint matIndex = (uint)OwnerClientId % (uint)playerMaterials.Length;
            arrowTransform.GetComponent<MeshRenderer>().material = playerMaterials[matIndex];
        }
    }

    private void SpawnCharacterAtSpawnPoint()
    {
        var spawner = NetworkManager.Singleton.GetComponent<RandomPositionPlayerSpawner>();
        if (spawner != null)
        {
            var nextSpawnPosition = spawner.GetNextSpawnPosition();
            transform.position = nextSpawnPosition;
            if (nextSpawnPosition.x < 0)
            {
                transform.rotation = Quaternion.Euler(0, 90, 0);
                camRotationY = 90;
            }
            else
            {
                transform.rotation = Quaternion.Euler(0, -90, 0);
                camRotationY = -90;
            }
        }
    }

    [ServerRpc]
    private void TryPickupBallServerRpc()
    {
        if (heldBall != null) return;

        Collider[] colliders = Physics.OverlapSphere(transform.position, 2f);
        foreach (var col in colliders)
        {
            if (col.CompareTag("Ball"))
            {
                if (col.TryGetComponent(out NetworkObject ballNetObj))
                {
                    PickupBall(ballNetObj); // Now correctly passing NetworkObject
                    break;
                }
            }
        }
    }

    private void PickupBall(NetworkObject ballNetObj)
    {
        heldBall = ballNetObj.gameObject;

        if (heldBall.TryGetComponent(out heldBallCollider))
        {
            Collider playerCollider = GetComponent<Collider>();
            Physics.IgnoreCollision(playerCollider, heldBallCollider, true);
        }

        var ballRb = heldBall.GetComponent<Rigidbody>();
        ballRb.isKinematic = true;
        ballRb.useGravity = false; // Disable gravity while held

        // Parent to the PlayerPrefab
        // Use the NetworkObject's TrySetParent to ensure it replicates to all clients
        bool success = ballNetObj.TrySetParent(this.NetworkObject, false);

        if (success)
        {
            // Snap the ball exactly to the holdPosition's LOCAL coordinates
            // since the ball is now a sibling/child within the same hierarchy
            heldBall.transform.localPosition = holdPosition.localPosition;
            heldBall.transform.localRotation = holdPosition.localRotation;

            if (heldBall.TryGetComponent(out BallProperties props))
                props.SetHeldState(true, OwnerClientId);
        }
    }

    [ServerRpc]
    private void StartChargeServerRpc() { if (heldBall != null) isCharging = true; }

    private void HandleCharging()
    {
        if (isCharging && heldBall != null)
        {
            currentChargeTime = Mathf.Min(currentChargeTime + Time.deltaTime, maxChargeTime);
        }
    }

    [ServerRpc]
    private void ReleaseThrowServerRpc()
    {
        if (heldBall == null) return;

        if (heldBall.TryGetComponent(out NetworkObject ballNetObj))
        {
            isCharging = false;
            float powerPercent = currentChargeTime / maxChargeTime;
            float totalForce = Mathf.Lerp(minThrowForce, maxThrowForce, powerPercent);

            // 1. Unparent first
            ballNetObj.TrySetParent((Transform)null, true);

            // 2. Re-enable Physics
            var ballRb = heldBall.GetComponent<Rigidbody>();
            ballRb.isKinematic = false;
            ballRb.useGravity = true;

            if (heldBallCollider != null)
            {
                Physics.IgnoreCollision(GetComponent<Collider>(), heldBallCollider, false);
            }

            // 3. Calculate direction based on Camera look direction
            // We use the playerCamera's forward if available, otherwise transform.forward
            Vector3 throwDir = playerCamera != null ? playerCamera.transform.forward : transform.forward;

            ballRb.AddForce(throwDir * totalForce, ForceMode.Impulse);

            if (heldBall.TryGetComponent(out BallProperties props))
                props.SetThrown(powerPercent);

            heldBall = null;
            heldBallCollider = null;
            currentChargeTime = 0;
        }
    }

    public void TakeDamage(int damage, Vector3 knockbackForce)
    {
        if (!IsServer) return;
        Health.Value -= damage;
        rb.AddForce(knockbackForce, ForceMode.Impulse);
    }
}