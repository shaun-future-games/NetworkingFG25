using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    private InputSystem_Actions inputActions;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Material[] playerMaterials;

    private GameObject arrowObject;

    // We store the input to be processed on the Server's Update cycle
    private Vector2 serverInputVector;

    public override void OnNetworkSpawn()
    {
        inputActions = new InputSystem_Actions();

        // Only the owner needs to listen to hardware input
        if (IsOwner)
        {
            inputActions.Enable();
        }

        SetupPlayerVisuals();
    }

    private void SetupPlayerVisuals()
    {
        // Finding the child object named "Arrow"
        Transform arrowTransform = transform.Find("Arrow");
        if (arrowTransform != null)
        {
            arrowObject = arrowTransform.gameObject;
            uint matIndex = (uint)OwnerClientId % (uint)playerMaterials.Length;
            arrowObject.GetComponent<MeshRenderer>().material = playerMaterials[matIndex];
        }
    }

    void Update()
    {
        if (IsOwner)
        {
            // 1. Client gathers input
            Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();

            // 2. Client sends input to Server
            MoveServerRpc(input);
        }

        if (IsServer)
        {
            // 3. Server performs the actual movement
            MovePlayer();
        }
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2 input)
    {
        // The server receives the client's request and stores the direction
        serverInputVector = input;
    }

    private void MovePlayer()
    {
        if (serverInputVector.sqrMagnitude > 0.01f)
        {
            Vector3 moveDir = new Vector3(serverInputVector.x, 0, serverInputVector.y);
            transform.Translate(moveDir * moveSpeed * Time.deltaTime);
        }
    }

    public override void OnNetworkDespawn()
    {
        inputActions?.Disable();
    }
}