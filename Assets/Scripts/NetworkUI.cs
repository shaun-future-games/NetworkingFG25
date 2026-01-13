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
        NetworkManager.Singleton.OnConnectionEvent += OnConnectionEvent;
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

    private void ShowConnectionUI(bool show)
    {
        startHostButton.gameObject.SetActive(show);
        startClientButton.gameObject.SetActive(show);
    }

    private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData connectionEventData)
    {
        ShowConnectionUI(false);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
        }
        
    }
}
