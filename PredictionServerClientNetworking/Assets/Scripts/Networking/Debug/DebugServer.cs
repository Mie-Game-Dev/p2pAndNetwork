using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class DebugServer : MonoBehaviour
{
    [SerializeField] private TMP_InputField ip;
    [SerializeField] private TMP_InputField port;
    [SerializeField] private Button startClient;
    [SerializeField] private Button startTutorial;
    [SerializeField] private TMP_Dropdown clientRole;

    [SerializeField] private string ipServer = "127.0.0.1";    
    [SerializeField] private string portServer = "7777";    

    private void Start()
    {
// #if UNITY_EDITOR
        ip.text = "127.0.0.1";
        port.text = "7777";
        ipServer = ip.text;
        portServer = port.text;
        startClient.onClick.RemoveAllListeners();
        startClient.onClick.AddListener(StartClient);
        startTutorial.onClick.AddListener(() => {MainMenu.instance.StartTutorial();});
        clientRole.onValueChanged.AddListener(ChangeCientRole);
// #else

//         gameObject.SetActive(false); 
// #endif
    }
#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            NetworkManager.Singleton.Shutdown();
            InternetConnectionChecker.instance.CloseWarning();
        }
    }
#endif

    private void ChangeCientRole(int value)
    {
        switch (value)
        {
            case 0:
                print($"Player Current Role is None");
                break;
            case 1:
                ClientSingleton.Instance.ClientGameManager.userData.userGamePreferences.userRole = E_LobbyRoles.PLAYER;
                print($"Player Current Role is Player");
                break;
            case 2:
                ClientSingleton.Instance.ClientGameManager.userData.userGamePreferences.userRole = E_LobbyRoles.SPECTATOR;
                print($"Player Current Role is Spectator");
                break;
            default:
                ClientSingleton.Instance.ClientGameManager.userData.userGamePreferences.userRole = E_LobbyRoles.PLAYER;
                break;
        }
    }

    private void StartClient()
    {
        int portId = int.Parse(port.text);
        var gameConnectionData = new MatchmakingResult();
        gameConnectionData.ip = ipServer;
        gameConnectionData.port = portId;
        ClientSingleton.Instance.ClientGameManager.SaveLastGameConnectionData(gameConnectionData);
        ClientSingleton.Instance.ClientGameManager.StartClient(ip.text, portId);
    }

    
}
