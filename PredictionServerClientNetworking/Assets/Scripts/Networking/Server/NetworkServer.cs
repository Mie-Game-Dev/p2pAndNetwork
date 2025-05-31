using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Collections;

// use for storing data of server
public class NetworkServer : IDisposable
{
    private NetworkManager networkManager;
    private NetworkObject playerPrefab;

    public Action<UserData> OnUserJoin;
    public Action<UserData> OnUserLeft;

    public Action<string> OnClientLeft;

    // get clientID / sent to ugs
    public Dictionary<ulong, FixedString64Bytes> ClientIdToAuth = new Dictionary<ulong, FixedString64Bytes>();
    // sent the id to get thge name user data
    public Dictionary<FixedString64Bytes, UserData> ClientData = new Dictionary<FixedString64Bytes, UserData>();
    // bot Data
    public Dictionary<ulong, BotData> BotData = new Dictionary<ulong, BotData>();
    ///
    //define constructor
    public NetworkServer(NetworkManager networkManager, NetworkObject playerPrefab)
    {
        this.networkManager = networkManager;
        this.playerPrefab = playerPrefab;

        networkManager.ConnectionApprovalCallback -= ApprovalCheck;
        networkManager.ConnectionApprovalCallback += ApprovalCheck;
        // OnUserJoin += OnClientReConnect;
        networkManager.OnServerStarted -= OnNetworkReady;
        networkManager.OnServerStarted += OnNetworkReady;
    }

    // check dedicated server already open?
    public bool OpenConnection(string ip, int port)
    {
        UnityTransport transport = networkManager.gameObject.GetComponent<UnityTransport>();

        // set connection data
        transport.SetConnectionData(ip, (ushort)port);
        return networkManager.StartServer();
    }

    private void OnClientReConnect(UserData userData)
    {
        if(MatchStatServer.Instance != null)
        {
            Debug.Log("OnClientReConnect " + userData.userAuthId);
            Debug.Log("OnClientReConnect " + MatchStatServer.Instance.GetHeroByAuthenticationId(userData.userAuthId));
            //GetClientDataByAuthId(userData.userAuthId).characterId = userData.characterId;
            MatchStatServer.Instance.GetHeroByAuthenticationId(userData.userAuthId).ChangeOwnership(userData.clientId);
        }
        // Debug.Log("change ownership " + userData.clientId);
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // convert byte to string : but not real name string
        string payload = System.Text.Encoding.UTF8.GetString(request.Payload);
        // convert the string into the real name data to readable
        UserData userData = JsonUtility.FromJson<UserData>(payload);
        if (userData == null)
        {
            Debug.Log("user data is null");
        }

#if UNITY_SERVER
        WriteTofireBaseIPandPort(userData, ServerSingleton.Instance.GameManager.multiplayAllocationService.multiplayService.ServerConfig.IpAddress, ServerSingleton.Instance.GameManager.multiplayAllocationService.multiplayService.ServerConfig.Port.ToString(), userData.userGamePreferences.userRole);

        Debug.Log($"[NetworkServer] ip and port : {ServerSingleton.Instance.GameManager.multiplayAllocationService.multiplayService.ServerConfig.IpAddress} {ServerSingleton.Instance.GameManager.multiplayAllocationService.multiplayService.ServerConfig.Port}");
#endif
        if (userData.userGamePreferences.gameQueue == GameQueue.Queue5v5Custom && userData.userGamePreferences.teamIndex == -1)
        {
            if (SceneManager.GetActiveScene().name == "PreEnterRoomTemp" && userData.userGamePreferences.userRole == E_LobbyRoles.SPECTATOR)
            {
                Debug.Log($"[server] user role is now : {userData.userGamePreferences.userRole}");
                BlockPlayerFromSpawn(response);
                return;
            }
            else if (userData.userGamePreferences.userRole == E_LobbyRoles.NONE)
            {
                BlockPlayerFromSpawn(response);
                return;
            }
        }

#if UNITY_EDITOR
        if (userData.userGamePreferences.userRole == E_LobbyRoles.SPECTATOR)
        {
            BlockPlayerFromSpawn(response);
            return;
        }
#endif

        Debug.Log($"[server] user role : {userData.userGamePreferences.userRole}");

        bool reconnected = false;
        userData.reconnect = false;
        Debug.Log($"Host ApprovalCheck: connecting client: ({request.ClientNetworkId}) - {userData}");
        if (ClientData.TryGetValue(userData.userAuthId, out var clientInfo))
        {
            userData = clientInfo;
            ulong oldClientId = clientInfo.clientId;
            userData.reconnect = true;
            Debug.Log($"Duplicate ID Found: {userData.userAuthId}, Disconnecting Old user");
            Debug.Log($"The user current character id : {clientInfo.characterId}"); // Use existing characterId from clientInfo

            // Preserve existing characterId or other important data when updating
            userData.userName = clientInfo.userName;
            userData.characterId = clientInfo.characterId;
            userData.userGamePreferences.teamIndex = clientInfo.userGamePreferences.teamIndex; // Preserve other important data if necessary
            userData.skinId = clientInfo.skinId; // Preserve other important data if necessary
            userData.artifakId = clientInfo.artifakId; // Preserve other important data if necessary
            userData.userGamePreferences.userRole = clientInfo.userGamePreferences.userRole; // Preserve other important data if necessary

            SendClientDisconnected(request.ClientNetworkId, ConnectStatus.LoggedInAgain);
            WaitToDisconnect(oldClientId);
            reconnected = userData.reconnect;
        }

        Debug.Log("[server] USER AUTH ID DICTIONARY" + userData.userAuthId);
        Debug.Log("[server] Current Character ID DICTIONARY" + userData.characterId);
        userData.clientId = request.ClientNetworkId;
        Debug.Log("[server] Current Client ID DICTIONARY" + userData.clientId);
        // set dictionary id / userdataauthid
        // Extra safeguard to ensure only non-spectators are added
        if (userData.userGamePreferences.gameQueue == GameQueue.Queue5v5Custom && userData.userGamePreferences.teamIndex != -1)
        {
            if(userData.userGamePreferences.userRole == E_LobbyRoles.PLAYER)
            {
                ClientIdToAuth[request.ClientNetworkId] = userData.userAuthId;
                ClientData[userData.userAuthId] = userData;
                GetClientDataByAuthId(userData.userAuthId).userGamePreferences.teamIndex = userData.userGamePreferences.teamIndex;
                Debug.Log($"[server] user {userData.clientId} team is now : {GetClientDataByAuthId(userData.userAuthId).userGamePreferences.teamIndex}");
            }
        }
        else
        {
            ClientIdToAuth[request.ClientNetworkId] = userData.userAuthId;
            ClientData[userData.userAuthId] = userData;
            OnUserJoin?.Invoke(userData);
        }

        if (reconnected) OnClientReConnect(userData);
        _ = SpawnPlayerDelayed(request.ClientNetworkId);
        // approve the connection
        response.Approved = true;
        //allow to create own object
        response.CreatePlayerObject = false;
        OnlineServerConnectionState();
        Debug.Log($"[Network Server] Current Player Dictionary Data Count {ClientData.Count}");
    }

    private static void BlockPlayerFromSpawn(NetworkManager.ConnectionApprovalResponse response)
    {
        // spectator
        response.Approved = true;
        response.CreatePlayerObject = false;
        response.Position = null;
        response.Rotation = null;
        response.Pending = false;
    }

    private async Task SpawnPlayerDelayed(ulong clientId)
    {
        await Task.Delay(1000);

        NetworkObject playerInstance = GameObject.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        playerInstance.SpawnAsPlayerObject(clientId);

        // paolo
/*        if (SceneManager.GetActiveScene().name == "PreEnterRoomTemp")
        {
            Debug.Log("[Server Spawning the player in the standard flow");
        }
        else
        {
            Debug.Log("[Server Spawning the player in the reconnection flow");
            Debug.Log("[Server Moving player to the current scene: " + SceneManager.GetActiveScene().name);
            //move the player out of the MainScene, and put it in my scene
            SceneManager.MoveGameObjectToScene(playerInstance.gameObject, SceneManager.GetActiveScene());
        }*/
        //playerInstance.
    }

    private void OnNetworkReady()
    {
        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
        networkManager.OnClientConnectedCallback -= OnClientReConnect;
        networkManager.OnClientConnectedCallback += OnClientReConnect;
    }

    private void OnClientReConnect(ulong clientId)
    {
        if (ClientIdToAuth.TryGetValue(clientId, out FixedString64Bytes authId))
        {
            if(ClientData.TryGetValue(authId, out UserData userData))
            {
                if(userData.reconnect)
                {
                    // Debug.Log("print networks server reconnect");
                    if (MatchStatServer.Instance != null)
                    {
                        NetworkManager.Singleton.StartCoroutine(AssignOwnership(clientId, authId));
                    }
                }
            }
            
        }
        
    }

    private IEnumerator AssignOwnership(ulong clientId, FixedString64Bytes authId)
    {
        yield return new WaitForSeconds(10);
        MatchStatServer.Instance.GetHeroByAuthenticationId(authId.ToString()).ChangeOwnership(clientId);
    }

    // remove the disconnect player data from dictionary
    private void OnClientDisconnect(ulong clientId)
    {
        if (networkManager.IsListening) return;
        if (ClientIdToAuth.TryGetValue(clientId, out FixedString64Bytes authId))
        {
            if(NetworkManager.Singleton.IsServer && GameManager.instance) GameManager.instance.GetNetworkObject(clientId).ChangeOwnership(0);
            ClientIdToAuth.Remove(clientId);
            OnUserLeft?.Invoke(ClientData[authId]);
            ClientData.Remove(authId);
        }
    }

    public void UpdateClientDictionary(ulong clientId, FixedString64Bytes authId, UserData userData)
    {
        if (ClientIdToAuth.TryGetValue(clientId, out FixedString64Bytes authId1))
        {
            // if(authId1 != null)
            // {
                Debug.Log("update client dictionary auth id");
                ClientIdToAuth[clientId] = authId;
            // }
            // else
            // {
            //     Debug.Log("add client dictionary client id");
            //     ClientIdToAuth.Add(clientId, authId);
            // }
            if (ClientData.TryGetValue(authId, out UserData data))
            {
                // if(data != null)
                // {
                    Debug.Log("update client dictionary user data");
                    ClientData[authId] = userData;
                // }
                // else
                // {
                //     Debug.Log("add client dictionary user data");
                //     ClientData.Add(authId, userData);
                // }
            }
            else
            {
                Debug.Log("add client dictionary user data");
                ClientData.Add(authId, userData);
            }
        }
        else
        {
            Debug.Log("add both client dictionary client id and auth id");
            ClientIdToAuth.Add(clientId, authId);
            ClientData.Add(authId, userData);
        }
    }

    public UserData GetUserDataByClientId(ulong clientId)
    {
        if (ClientIdToAuth.TryGetValue(clientId, out FixedString64Bytes authId))
        {
            if (ClientData.TryGetValue(authId, out UserData data))
            {
                return data;
            }

            return null;
        }

        return null;
    }

    public UserData GetClientDataByAuthId(FixedString64Bytes authId)
    {
        if (ClientData.TryGetValue(authId, out UserData userData))
        {
            return userData;
        }
        else
        {
            return null; // If the authId is not found, return null
        }
    }

    public List<UserData> GetUserDataByTeamId(int teamId)
    {
       return ClientData
        .Where(pair => pair.Value.userGamePreferences.teamIndex == teamId) // Filter by teamId
        .Select(pair => pair.Value)
        .ToList();
    }

    public void ResetServerConnectionState()
    {
        Debug.Log("[server] Send to Firebase offline connection state");
        foreach (var entry in ClientData)
        {
            // Access the key (FixedString64Bytes) and value (UserData) in the dictionary
            var key = entry.Key;
            var userData = entry.Value;

            // Pass userAuthId as the third parameter
            WriteDisconnetToFirebase(userData);
        }
    }

    public void OnlineServerConnectionState()
    {
        Debug.Log("[server] Send to Firebase online connection state");
        foreach (var entry in ClientData)
        {
            // Access the key (FixedString64Bytes) and value (UserData) in the dictionary
            var key = entry.Key;
            var userData = entry.Value;

            // Pass userAuthId as the third parameter
            WriteConnetToFirebase(userData);
        }
    }

    public void WriteTofireBaseIPandPort(UserData userData, string ip, string port, E_LobbyRoles role)
    {
        FirebaseReconnect.Instance.WriteToFirebaseIpAndPort(userData.userAuthId, "Online", ip, port.ToString(), role.ToString(),ServerSingleton.Instance.GameManager.FirebaseIDToken);
    }

    private void WriteConnetToFirebase(UserData userData)
    {
        FirebaseReconnect.Instance.WriteToFirebase(userData.userAuthId, "Online", ServerSingleton.Instance.GameManager.FirebaseIDToken);
    }

    private void WriteDisconnetToFirebase(UserData userData)
    {
        FirebaseReconnect.Instance.WriteToFirebase(userData.userAuthId, "Offline", ServerSingleton.Instance.GameManager.FirebaseIDToken);
    }

    private void SendClientConnected(ulong clientId, ConnectStatus status)
    {
        var writer = new FastBufferWriter(sizeof(ConnectStatus), Allocator.Temp);
        writer.WriteValueSafe(status);
        Debug.Log($"Send Network Client Connected to : {clientId}");
        MatchplayNetworkMessenger.SendMessageTo(NetworkMessage.LocalClientConnected, clientId, writer);
    }

    private void SendClientDisconnected(ulong clientId, ConnectStatus status)
    {
        var writer = new FastBufferWriter(sizeof(ConnectStatus), Allocator.Temp);
        writer.WriteValueSafe(status);
        Debug.Log($"Send networkClient Disconnected to : {clientId}");
        MatchplayNetworkMessenger.SendMessageTo(NetworkMessage.LocalClientDisconnected, clientId, writer);
    }

    private async void WaitToDisconnect(ulong clientId)
    {
        await Task.Delay(500);
        networkManager.DisconnectClient(clientId);
    }

    public void SetCharacter(ulong clientId, int characterId)
    {
        if (ClientIdToAuth.TryGetValue(clientId, out FixedString64Bytes auth))
        {
            if (ClientData.TryGetValue(auth, out UserData data))
            {
                data.characterId = characterId;
            }
        }
    }

    public void SetSkin(ulong clientId, int skinId)
    {
        if (ClientIdToAuth.TryGetValue(clientId, out FixedString64Bytes auth))
        {
            if (ClientData.TryGetValue(auth, out UserData data))
            {
                data.skinId = skinId;
            }
        }
    }

    public void SetBotCharacter(ulong networkObjectId, int characterId)
    {
        if (BotData.TryGetValue(networkObjectId, out BotData data))
        {
            data.characterId = characterId;
        }
    }

    public void SetArtifak(ulong clientId, int artifakId)
    {
        if (ClientIdToAuth.TryGetValue(clientId, out FixedString64Bytes auth))
        {
            if (ClientData.TryGetValue(auth, out UserData data))
            {
                data.artifakId = artifakId;
            }
        }
    }

    // unsubscribe anything subscribed
    public void Dispose()
    {
        if (networkManager == null) return;

        networkManager.ConnectionApprovalCallback -= ApprovalCheck;
        networkManager.OnServerStarted -= OnNetworkReady;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        networkManager.OnClientConnectedCallback -= OnClientReConnect;

        if(networkManager.IsListening)
        {
            networkManager.Shutdown();
        }
    }
}
