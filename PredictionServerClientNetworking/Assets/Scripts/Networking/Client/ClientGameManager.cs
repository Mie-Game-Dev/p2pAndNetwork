using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum E_GameStateForReconnectResult
{
    None,
    GameOngoing,
    GameEnded
}

public class ClientGameManager : IDisposable // c# dispose way
{
    private JoinAllocation allocation;
    private NetworkClient networkClient;
    private MatchplayMatchmaker matchmaker;
    public UserData userData;
    public List<UserData> usersData = new List<UserData>();
    public ulong specatatePlayerid;

    private const string menuSceneName = "MainMenu";

    //paolo
    MatchmakingResult lastGameConnectionData;
    const string LastGameServerIPkey = "LastGameServerIP";
    const string LastGameServerPortPkey = "LastGameServerPort";
    private E_GameStateForReconnectResult gameStateForReconnect = E_GameStateForReconnectResult.None;

    string lastGameServerIP;
    int lastGameServerPort;

    public async Task<bool> InitAsync()
    {
        var options = new InitializationOptions();
        options.SetProfile("main_profile");
#if UNITY_EDITOR
        if (ParrelSync.ClonesManager.IsClone())
        {
            // When using a ParrelSync clone, switch to a different authentication profile to force the clone
            // to sign in as a different anonymous user account.
            string customArgument = ParrelSync.ClonesManager.GetArgument();
            AuthenticationService.Instance.SwitchProfile($"Clone_{customArgument}_Profile_");
        }
#endif
        await UnityServices.InitializeAsync(options);

        networkClient = new NetworkClient(NetworkManager.Singleton);
        matchmaker = new MatchplayMatchmaker();


        //Debug.LogError("[Client] Local client is null");
        AuthState authState = await AuthenticationWrapper.DoAuth();

        if(authState == AuthState.Authenticated)
        {
            // set username of the client
            userData = new UserData
            {
                // get username
                userName = PlayerPrefs.GetString("Player_Name"),
                // get player id
                userAuthId = AuthenticationService.Instance.PlayerId,

                userGamePreferences = new GameInfo
                {
                    rank = (int)FirebaseClientSideAuthentication.instance.PlayerRank
                },
            };

            return true;
        }

        return false;
    }

    public void GoToMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    // this method use to start connect client to dedicated server
    public bool StartClient(string ip, int port, ulong clientId = 0)
    {
        specatatePlayerid = clientId;
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ip, (ushort)port);
        return ConnectClient();
    }

    // this method use to start connect client relay server
    public async Task StartClientAsync(string joinCode)
    {
        try
        {
            // check allocation 
            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        }
        catch (Exception e)
        {

            // Debug.Log(e);
            return;
        }

        // setup relay data// ip 
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
        transport.SetRelayServerData(relayServerData);

        ConnectClient();
    }

    public bool ConnectClient()
    {
        string payload = JsonUtility.ToJson(userData);
        // convert the name into bytes to send over the network
        byte[] payLoadBytes = Encoding.UTF8.GetBytes(payload);
        // sent the payload bytes to the server
        NetworkManager.Singleton.NetworkConfig.ConnectionData = payLoadBytes;

        return NetworkManager.Singleton.StartClient();
    }

    // make matchmaking (by default queue is team)
    public async void MatchMakeAsync(Action<MatchmakerPollingResult> onMatchmakeResponse)
    {
        if (matchmaker.IsMatchmaking) return;


        userData.userGamePreferences.gameQueue = GameQueueMode();
        // get matchmaking
        MatchmakerPollingResult matchResult = await GetMatchAsync();
        onMatchmakeResponse?.Invoke(matchResult);
    }

    private GameQueue GameQueueMode()
    {
        var gameQueue = GameQueue.Queue3v3;
        switch (GameModeManager.Instance.GameSetting.GameMode)
        {
            case GameMode.Mode3v3:

                switch (GameModeManager.Instance.GameSetting.GameType)
                {
                    case GameType.Custom:
                        gameQueue = GameQueue.Queue3v3Custom;
                        break;
                    case GameType.Classic:
                        gameQueue = GameQueue.Queue3v3;
                        break;
                    case GameType.Ranked:
                        break;
                    case GameType.VsAi:
                        gameQueue = GameQueue.Queue3v3Ai;
                        break;
                    case GameType.Practice:
                        break;
                    default:
                        break;
                }

                break;

            case GameMode.Mode5v5:

                switch (GameModeManager.Instance.GameSetting.GameType)
                {
                    case GameType.Custom:
                        gameQueue = GameQueue.Queue5v5Custom;
                        break;
                    case GameType.Classic:
                        gameQueue = GameQueue.Queue5v5;
                        break;
                    case GameType.Ranked:
                        gameQueue = GameQueue.Queue5v5Ranked;
                        break;
                    case GameType.VsAi:
                        gameQueue = GameQueue.Queue5v5Ai;
                        break;
                    case GameType.Practice:
                        break;
                    default:
                        break;
                }

                break;
        }
        return gameQueue;
    }

    // will return when finish matchmaking result : either fail or sucess
    private async Task<MatchmakerPollingResult> GetMatchAsync()
    {
        MatchmakingResult matchmakingResult;

        if (usersData.Count == 0)
        {
            Debug.Log("[ClientGameManager] Matchmaking for UserData: " + userData.userName);
            matchmakingResult = await matchmaker.Matchmake(userData);
        }
        else
        {         
            for(int i = 0; i < usersData.Count; i++)
            {
                Debug.Log("[ClientGameManager] Matchmaking for UsersData: " + usersData[i].userName);
            }
            matchmakingResult = await matchmaker.MatchmakeTeam(usersData);
        }

        Debug.Log("[ClientGameManager] Matchmaking result is: " + matchmakingResult.result.ToString());

        // check if success
        if(matchmakingResult.result == MatchmakerPollingResult.Success)
        {
            // paolo
            if(matchmakingResult.ip == "0.0.0.0")
            {
                Debug.Log("[ClientGameManager] Received IP is invalid: " +  matchmakingResult.ip);
                MainMenu.instance.ShowLoaderAndCancelButton(false);
            }
            else
            {
                SaveLastGameConnectionData(matchmakingResult);

                StartClient(matchmakingResult.ip, matchmakingResult.port);

                if (LobbyManager.instance)
                {
                    Debug.Log("[ClientGameManager] Sending to Lobby the IP of: " + matchmakingResult.ip + " and Port of: " + matchmakingResult.port.ToString());
                    LobbyManager.instance.UpdateLobbyIPandPort(matchmakingResult.ip, matchmakingResult.port.ToString());
                }

                FriendsManager.Instance.ChangeStatus(Unity.Services.Friends.Models.Availability.Busy, "InGame");
                // Pass userAuthId as the third parameter          
            }
        }

        return matchmakingResult.result;
    }

    // cancel matchmaking
    public async Task CancelMatchmaking()
    {
        await matchmaker.CancelMatchmaking();
    }

    public void Disconnect()
    {
        networkClient.Disconnect();
    }

    // implement interface when this object being dispose
    public void Dispose()
    {
        networkClient?.Dispose();
    }

    // paolo------------------------------------------------

    private float connectionAttemptStartTime;
    private const float SINGLE_ATTEMPT_TIMEOUT = 5f;
    private const float TOTAL_CONNECTION_TIMEOUT = 60f;

    public IEnumerator OnGameStarted()
    {
        while (FirebaseReconnect.Instance == null)
        {
            Debug.Log("[client] Wait firebase reconnect to load");
            yield return null;
        }

        Debug.Log("calling for reconnect");

        FirebaseReconnect.Instance.ReadFromFirebase(AuthenticationService.Instance.PlayerId, "Online", connect =>
        {
            Debug.Log($"[client] result firebase reconnect state {connect}");
            Debug.Log($"[client] result firebase reconnect Id {AuthenticationService.Instance.PlayerId}");
            if (!connect)
            {
                gameStateForReconnect = E_GameStateForReconnectResult.GameEnded;
                Debug.Log("[client] game ended");
                return;
            }
            else
            {
                gameStateForReconnect = E_GameStateForReconnectResult.GameOngoing;
                Debug.Log("[client] game running");
            }
        });

        yield return new WaitUntil(() => gameStateForReconnect != E_GameStateForReconnectResult.None);
#if !UNITY_SERVER
        yield return new WaitUntil(() => MastraLoadingPanel.Instance == null);
#endif
        yield return new WaitForSeconds(3f);

        if (gameStateForReconnect == E_GameStateForReconnectResult.GameOngoing)
        {
            Debug.Log("[client] check last game connection data");
            InternetConnectionChecker.instance.OpenPromptReconnectingGame();

            Debug.Log("Reconnecting to last game...");
            lastGameConnectionData = new MatchmakingResult();
            FirebaseClientSidePlayerData.instance.GetIP(OnIpGet);
            FirebaseClientSidePlayerData.instance.GetPort(OnPortGet);
            FirebaseClientSidePlayerData.instance.GetRole(OnRoleGet);

            if (NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[client] NetworkManager.Singleton.IsListening Shutting down network manager");
                NetworkManager.Singleton.Shutdown();
                Debug.Log("[client] reconnect routine NetworkManager.Singleton.ShutdownInProgress");
                yield return new WaitWhile(() => NetworkManager.Singleton.ShutdownInProgress);
            }

            while (!NetworkManager.Singleton.NetworkConfig.ForceSamePrefabs)
            {
                yield return null;
            }

            while (lastGameServerIP == string.Empty || lastGameServerPort == 0)
            {
                yield return null;
            }

            Debug.Log($"[client] Current ip {lastGameServerIP} port {lastGameServerPort}");

            // Start retry logic
            connectionAttemptStartTime = Time.time;
            bool connectionSuccessful = false;

            while (Time.time - connectionAttemptStartTime < TOTAL_CONNECTION_TIMEOUT && !connectionSuccessful)
            {
                Debug.Log($"[client] Attempting to connect to {lastGameServerIP}:{lastGameServerPort}");
                StartClient(lastGameServerIP, lastGameServerPort);

                // Wait for either connection success or timeout
                float attemptStartTime = Time.time;
                while (Time.time - attemptStartTime < SINGLE_ATTEMPT_TIMEOUT)
                {
                    if (NetworkManager.Singleton.IsConnectedClient)
                    {
                        connectionSuccessful = true;
                        Debug.Log("[client] Successfully reconnected!");
                        yield break; // Exit coroutine on success
                    }
                    yield return null;
                }

                if (!connectionSuccessful)
                {
                    Debug.Log("[client] Connection attempt timed out, retrying...");
                    if (NetworkManager.Singleton.IsListening)
                    {
                        NetworkManager.Singleton.Shutdown();
                        yield return new WaitWhile(() => NetworkManager.Singleton.ShutdownInProgress);
                    }
                }
            }

            if (!connectionSuccessful)
            {
                Debug.Log("[client] Failed to reconnect after 60 seconds, giving up");
                if (NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                InternetConnectionChecker.instance.CloseWarning();
                // Handle complete failure (return to main menu, show error, etc.)
            }

            gameStateForReconnect = E_GameStateForReconnectResult.None;
        }
        else
        {
            NetworkManager.Singleton.StopCoroutine(OnGameStarted());
            gameStateForReconnect = E_GameStateForReconnectResult.None;
        }
    }

    internal void SaveLastGameConnectionData(MatchmakingResult matchmakingResult)
    {
        lastGameConnectionData = matchmakingResult; //save for current session
        //LobbyManager.instance.UpdateLobbyIPandPort(lastGameConnectionData.ip, lastGameConnectionData.port.ToString());
        //save it in case the app crashes
        /*PlayerPrefs.SetString(LastGameServerIPkey, lastGameConnectionData.ip);
        PlayerPrefs.SetInt(LastGameServerPortPkey, lastGameConnectionData.port);
        PlayerPrefs.Save();*/

        Debug.Log("[ClientGameManager] Wrting to Firebase the IP: " + matchmakingResult.ip + " and Port: " + matchmakingResult.port.ToString() + "and the role: " + userData.userGamePreferences.userRole.ToString());
        //FirebaseClientSidePlayerData.instance.UpdateFirebase(matchmakingResult.ip, matchmakingResult.port.ToString(), userData.userGamePreferences.userRole.ToString());
    }

    private void OnPortGet(string port)
    {
        Debug.Log($"port save from firebase{port}");
        lastGameServerPort = int.Parse(port);
    }

    private void OnIpGet(string ip)
    {
        Debug.Log($"ip save from firebase{ip}");
        lastGameServerIP = ip;
    }

    private void OnRoleGet(string role)
    {
        Debug.Log($"ip save from firebase{role}");
        if(Enum.TryParse(role, out E_LobbyRoles lobbyRoles))
        {
            ClientSingleton.Instance.ClientGameManager.userData.userGamePreferences.userRole = lobbyRoles;
        }
        else
        {
            Debug.Log("[ClientGameManager] Invalid Role received: " + role);
        }
    }


    public void WriteDisconnetToFirebase()
    {
        FirebaseReconnect.Instance.PlayerWriteConnectionStateToFirebase("Offline");
    }

    public void OnGameEnded()
    {
        InternetConnectionChecker.instance.EnableReconnectingGameButton();
        WriteDisconnetToFirebase();
        lastGameConnectionData = null;
        PlayerPrefs.SetString(LastGameServerIPkey, string.Empty);
        PlayerPrefs.SetInt(LastGameServerPortPkey, -1);
        PlayerPrefs.DeleteKey(LastGameServerIPkey);
        PlayerPrefs.DeleteKey(LastGameServerPortPkey);
        PlayerPrefs.Save();
        NetworkManager.Singleton.StopAllCoroutines();
    }
}
