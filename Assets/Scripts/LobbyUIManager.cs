using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class LobbyUIManager : MonoBehaviour
{
    public InputField ipInput;
    public GameObject startPanel;
    public GameObject waitingPanel;
    public GameObject startButton;
    public string gameSceneName;

    public Text playerNumText;
    public bool isHost = false; 

    private void Start()
    {
        startPanel.SetActive(true);
        waitingPanel.SetActive(false);
        startButton.SetActive(false);
    }

    public void Update()
    {
        if (waitingPanel.activeSelf)
        {
            playerNumText.text = "Joined Players: " + NetworkManager.Singleton.ConnectedClients.Count;
            if(NetworkManager.Singleton.ConnectedClients.Count == 2 && isHost)
            {
                startButton.SetActive(true);
            }
        }
    }

    public void StartBtnOnClick()
    {
        if (isHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public void HostBtnOnClick()
    {
        NetworkManager.Singleton.StartHost();
        startPanel.SetActive(false);
        waitingPanel.SetActive(true);
        isHost = true;
    }

    public void JoinBtnOnClick()
    {
        if(ipInput.text != "")
        {
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ipInput.text, 7777);
        }

        NetworkManager.Singleton.StartClient();
        startPanel.SetActive(false);
        waitingPanel.SetActive(true); 
        isHost = false;

    }
}
