using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUI : MonoBehaviour
{
    [SerializeField]
    private Button startHostButton;
    [SerializeField]
    private Button startClientButton;


    private void OnEnable()
    {
        startHostButton.onClick.AddListener(OnStartHostButtonClicked);
        startClientButton.onClick.AddListener(OnStartClientButtonClicked);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDisable()
    {
        startHostButton.onClick.RemoveListener(OnStartHostButtonClicked);
        startClientButton.onClick.RemoveListener(OnStartClientButtonClicked);
    }

    private void OnStartHostButtonClicked()
    {
        NetworkManager.Singleton.StartHost();
    }

    private void OnStartClientButtonClicked()
    {
        NetworkManager.Singleton.StartClient();
    }
}
