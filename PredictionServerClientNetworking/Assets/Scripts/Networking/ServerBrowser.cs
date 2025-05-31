using System.Collections;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Text;
using System;
using TMPro;

public class ServerBrowser : MonoBehaviour
{
    [SerializeField] private Button createServerButton;
    [SerializeField] private Button cancelServerButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private MainMenu mainMenu;
    private string ip;
    private ushort port;


    private void Awake()
    {
        createServerButton.onClick.AddListener(() =>
        {
            CreateServer();
        });

        cancelServerButton.onClick.AddListener(CancelCreateServer);
        cancelServerButton.gameObject.SetActive(false);
    }

    [ContextMenu("JoinServer")]
    public void JoinServer()
    {
        string keyId = "792545b6-3a4e-4133-bdcc-53f7e37d000f";
        string keySecret = "JIlAeKovmeV56qDVHqBotLyY8eTezsQe";
        byte[] keyByteArray = Encoding.UTF8.GetBytes(keyId + ":" + keySecret);
        string keyBase64 = Convert.ToBase64String(keyByteArray);

        string projectId = "9495bede-abf0-44f1-9238-9b54304bdcce";
        string environmentId = "d0ae987c-75bf-4802-a1f5-ab033a29a7e3";
        string url = $"https://services.api.unity.com/multiplay/servers/v1/projects/{projectId}/environments/{environmentId}/servers";

        WebRequests.Get(url,
        (UnityWebRequest unityWebRequest) =>
        {
            unityWebRequest.SetRequestHeader("Authorization", "Basic " + keyBase64);
            Debug.Log("join web request : " + unityWebRequest.result);
        },
        (string error) =>
        {
            Debug.Log("Error: " + error);
        },
        (string json) =>
        {
            Debug.Log("Success: " + json);
            ListServers listServers = JsonUtility.FromJson<ListServers>("{\"serverList\":" + json + "}");
            foreach (Server server in listServers.serverList)
            {
                //Debug.Log(server.ip + " : " + server.port + " " + server.deleted + " " + server.status);
                if (server.status == ServerStatus.ONLINE.ToString() || server.status == ServerStatus.ALLOCATED.ToString())
                {
                    // Server is Online!
                    ip = server.ip;
                    port = (ushort)server.port;
                    print("ip and port setted");

                    LobbyManager.instance.UpdateLobbyIPandPort(ip, port.ToString());
                }
            }
        }
        );
      
    }

    private IEnumerator WaitUntillReady()
    {
        while (ip == string.Empty)
        {
            yield return null;
        }
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ip, port);

        NetworkManager.Singleton.StartClient();
    }

    [ContextMenu("CreateServer")]
    public void CreateServer()
    {

        /* int NumberOfPlayer = LobbyManager.instance.SendServerBrowserNumberOfPlayersInLobby();

         if (NumberOfPlayer > 1)
         {
             ClientSingleton.Instance.ClientGameManager.MatchMakeAsync(OnMatchMade);
             statusText.text = "Creating Server . . .";
             createServerButton.interactable = false;
         }
         else
         {
             LobbyManager.instance.UpdateLobbyIPandPort(ip, port.ToString());
         }       */

        ClientSingleton.Instance.ClientGameManager.MatchMakeAsync(OnMatchMade);

        mainMenu.ToggleCustomLobbyContainer(true);
        LobbyManager.instance.NotifyLobbyServerCreatingStarted();

        createServerButton.interactable = false;
        cancelServerButton.gameObject.SetActive(true);
    }
    private void OnMatchMade(MatchmakerPollingResult result)
    {
        switch (result)
        {
            case MatchmakerPollingResult.Success:
                FriendsManager.Instance.ChangeStatus(Unity.Services.Friends.Models.Availability.Busy, "In-Match");
                print("Success");
                break;
            case MatchmakerPollingResult.TicketCreationError:               
                print("TicketCreationError");
                break;
            case MatchmakerPollingResult.TicketCancellationError:
                print("TicketCancellationError");
                break;
            case MatchmakerPollingResult.TicketRetrievalError:
                print("TicketRetrievalError");
                break;
            case MatchmakerPollingResult.MatchAssignmentError:
                print("MatchAssignmentError");
                break;
        }


        if(result != MatchmakerPollingResult.Success)
        {
            FriendsManager.Instance.ChangeStatus(Unity.Services.Friends.Models.Availability.Busy, "In-Lobby");
            createServerButton.interactable = true;
            mainMenu.ToggleCustomLobbyContainer(false);
            LobbyManager.instance.NotifyLobbyServerCreatingStopped();
        }
        cancelServerButton.gameObject.SetActive(false);
    }

    async void CancelCreateServer()
    {
        await ClientSingleton.Instance.ClientGameManager.CancelMatchmaking();
        LobbyManager.instance.NotifyLobbyServerCreatingStopped();

        FriendsManager.Instance.ChangeStatus(Unity.Services.Friends.Models.Availability.Busy, "In-Lobby");

        cancelServerButton.gameObject.SetActive(false);
    }

    private void IntializeServerInstance()
    {
        string keyId = "792545b6-3a4e-4133-bdcc-53f7e37d000f";
        string keySecret = "JIlAeKovmeV56qDVHqBotLyY8eTezsQe";
        byte[] keyByteArray = Encoding.UTF8.GetBytes(keyId + ":" + keySecret);
        string keyBase64 = Convert.ToBase64String(keyByteArray);

        string projectId = "9495bede-abf0-44f1-9238-9b54304bdcce";
        string environmentId = "d0ae987c-75bf-4802-a1f5-ab033a29a7e3";
        string url = $"https://services.api.unity.com/auth/v1/token-exchange?projectId={projectId}&environmentId={environmentId}";

        string jsonRequestBody = JsonUtility.ToJson(new TokenExchangeRequest
        {
            scopes = new[] { "multiplay.allocations.create", "multiplay.allocations.list" },
        });

        WebRequests.PostJson(url,
        (UnityWebRequest unityWebRequest) =>
        {
            unityWebRequest.SetRequestHeader("Authorization", "Basic " + keyBase64);
        },
        jsonRequestBody,
        (string error) =>
        {
            Debug.LogError("Error during token exchange: " + error);
        },
        (string json) =>
        {
            Debug.Log("Token exchange success: " + json);
            TokenExchangeResponse tokenExchangeResponse = JsonUtility.FromJson<TokenExchangeResponse>(json);

            // Check if tokenExchangeResponse is not null and contains accessToken
            if (tokenExchangeResponse != null && !string.IsNullOrEmpty(tokenExchangeResponse.accessToken))
            {
                Debug.Log("Access Token: " + tokenExchangeResponse.accessToken);
                CreateAllocation(tokenExchangeResponse.accessToken);
            }
            else
            {
                Debug.LogError("Failed to obtain access token or response is null.");
            }
        });
    }

    private void CreateAllocation(string accessToken)
    {
        string projectId = "9495bede-abf0-44f1-9238-9b54304bdcce";
        string environmentId = "d0ae987c-75bf-4802-a1f5-ab033a29a7e3";
        string fleetId = "6e4e3e11-d548-4b54-b388-5fc881991a13";
        string url = $"https://multiplay.services.api.unity.com/v1/allocations/projects/{projectId}/environments/{environmentId}/fleets/{fleetId}/allocations";

        string jsonRequestBody = JsonUtility.ToJson(new QueueAllocationRequest
        {
            buildConfigurationId = 1275036,
            regionId = "dbd8da32-e8ee-4502-a249-fa0906bfe8b7"
        });

        UnityWebRequest unityWebRequest = new UnityWebRequest(url, "POST");
        unityWebRequest.SetRequestHeader("Authorization", "Bearer " + accessToken);
        unityWebRequest.SetRequestHeader("Content-Type", "application/json");  // Ensure correct content type
        unityWebRequest.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonRequestBody));
        unityWebRequest.downloadHandler = new DownloadHandlerBuffer();

        unityWebRequest.SendWebRequest().completed += (AsyncOperation op) =>
        {
            if (unityWebRequest.result == UnityWebRequest.Result.ConnectionError || unityWebRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error during allocation request: " + unityWebRequest.error);
                Debug.LogError("Server response: " + unityWebRequest.downloadHandler.text);
            }
            else
            {
                Debug.Log("Allocation success: " + unityWebRequest.downloadHandler.text);
                AllocationResponse allocationResponse = JsonUtility.FromJson<AllocationResponse>(unityWebRequest.downloadHandler.text);
                if (allocationResponse != null)
                {
                    Debug.Log("Allocation UUID: " + allocationResponse.allocationId);  // Ensure allocation ID is correctly referenced
                }
                else
                {
                    Debug.LogError("Failed to parse allocation response.");
                }
            }

            unityWebRequest.Dispose();
        };
    }



#if !DEDICATED_SERVER
    private void Start()
    {
        string keyId = "792545b6-3a4e-4133-bdcc-53f7e37d000f";
        string keySecret = "JIlAeKovmeV56qDVHqBotLyY8eTezsQe";
        byte[] keyByteArray = Encoding.UTF8.GetBytes(keyId + ":" + keySecret);
        string keyBase64 = Convert.ToBase64String(keyByteArray);

        string projectId = "9495bede-abf0-44f1-9238-9b54304bdcce";
        string environmentId = "d0ae987c-75bf-4802-a1f5-ab033a29a7e3";
        string url = $"https://services.api.unity.com/multiplay/servers/v1/projects/{projectId}/environments/{environmentId}/servers";

        WebRequests.Get(url,
        (UnityWebRequest unityWebRequest) =>
        {
            unityWebRequest.SetRequestHeader("Authorization", "Basic " + keyBase64);
        },
        (string error) =>
        {
            Debug.Log("Error: " + error);
        },
        (string json) =>
        {
            Debug.Log("Success: " + json);
            ListServers listServers = JsonUtility.FromJson<ListServers>("{\"serverList\":" + json + "}");
            foreach (Server server in listServers.serverList)
            {
                //Debug.Log(server.ip + " : " + server.port + " " + server.deleted + " " + server.status);
                if (server.status == ServerStatus.ONLINE.ToString() || server.status == ServerStatus.ALLOCATED.ToString())
                {
                    ip = server.ip;
                    port = (ushort)server.port;
                }
            }
        }
        );
    }
#endif

    public class TokenExchangeResponse
    {
        public string accessToken;
    }


    [Serializable]
    public class TokenExchangeRequest
    {
        public string[] scopes;
    }

    [Serializable]
    public class QueueAllocationRequest
    {
        public string allocationId;
        public int buildConfigurationId;
        public string payload;
        public string regionId;
        public bool restart;
    }


    private enum ServerStatus
    {
        AVAILABLE,
        ONLINE,
        ALLOCATED
    }

    [Serializable]
    public class ListServers
    {
        public Server[] serverList;
    }

    [Serializable]
    public class Server
    {
        public int buildConfigurationID;
        public string buildConfigurationName;
        public string buildName;
        public bool deleted;
        public string fleetID;
        public string fleetName;
        public string hardwareType;
        public int id;
        public string ip;
        public int locationID;
        public string locationName;
        public int machineID;
        public int port;
        public string status;
    }

    [Serializable]
    public class AllocationResponse
    {
        public string allocationId; // This will contain the new allocation ID
        public string serverIp; // Assuming the server IP is also returned
        public int serverPort; // Assuming the server port is also returned
    }

}