using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class HostGameManager : IDisposable
{
    private Allocation allocation;
    private NetworkObject playerPrefab;
    private int MaxConnections = 10;
    private string lobbyId;
    private const string GameSceneName = "WaitingRoomTemp";

    public string JoinCode { get; private set; }
    public NetworkServer NetworkServer { get; private set; }

    public HostGameManager(NetworkObject playerPrefab)
    {
        this.playerPrefab = playerPrefab;
    }

    public int ConnectionAmount(int amount)
    {
        MaxConnections = amount;
        Debug.Log("Max Coneection is :" + MaxConnections);
        return MaxConnections;
    }
    public int GetConnectionAmount()
    {
        return MaxConnections;
    }

    public async Task StartHostAsync(bool isPrivate)
    {
        try
        {
            // give allocation with max connection of 20
            allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);
        }
        catch (Exception e)
        {

            Debug.Log(e);
            return;
        }

        try
        {
            // Get the code of allocation
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            /*LobbyManager.instance.RelayActive = true;
            LobbyManager.instance.RelayCode = JoinCode;*/
            Debug.Log(JoinCode);
        }
        catch (Exception e)
        {

            Debug.Log(e);
            return;
        }
        // setup relay data// ip 
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
        transport.SetRelayServerData(relayServerData);

        try
        {
            CreateLobbyOptions lobbyOptions = new CreateLobbyOptions();
            // make lobby options private or not
            lobbyOptions.IsPrivate = isPrivate;
            lobbyOptions.Data = new Dictionary<string, DataObject>()
            {
                {
                    "JoinCode", new DataObject
                    (visibility: DataObject.VisibilityOptions.Member,
                    value: JoinCode)
                }
            };

            GameModeManager.Instance.SetMaxPlayerAmountToConnect();
            // Get player name
            string playeName = FirebaseClientSidePlayerData.instance.PlayerName;
            // set the lobby name based on the player name
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync($"{playeName}'s Lobby", MaxConnections, lobbyOptions);
            lobbyId = lobby.Id;


/*            //--when the leader finds a match through the matchmaker
            UpdateLobbyOptions updateLobbyOptions = new UpdateLobbyOptions();
            updateLobbyOptions.Data = lobby.Data;
            updateLobbyOptions.Data["ServerIP"] = new DataObject(visibility: DataObject.VisibilityOptions.Member, value: "198.165.22.13");
            updateLobbyOptions.Data["Serverport"] = new DataObject(visibility: DataObject.VisibilityOptions.Member, value: "12345");
            Lobby updatedLobby = await Lobbies.Instance.UpdateLobbyAsync(lobbyId, updateLobbyOptions);

            DateTime lastTimeLobbyWasUpdated; //initalize this to some old date 
            if (updatedLobby.LastUpdated != lastTimeLobbyWasUpdated)
            {

                //networkManager.StartClient(ip, port);
                //the lobby was updated, I need to check if the owner added server ip/port data
                lastTimeLobbyWasUpdated = updatedLobby.LastUpdated;
                string ip = updatedLobby.Data["ServerIP"].Value;
                string port = updatedLobby.Data["Serverport"].Value;
            }

            //-----------------
            //paolo*/
            HostSingleton.Instance.StartCoroutine(HeartBeatLobby(15)); // 15 seconds based on UGS efficiency of heartbeat ping
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return;
        }

        // make a network server to hook up the connection client approval callback
        NetworkServer = new NetworkServer(NetworkManager.Singleton, playerPrefab);

        // set username of the client
        // SetUserDataFoNextConnnection();
        SetUserDataFoTutorial();

        NetworkManager.Singleton.StartHost();
        //NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
        //GameModeManager.Instance.OpenLobby();
    }

    internal void SetUserDataFoNextConnnection()
    {
        var userData = new UserData
        {
            // get username
            userName = FirebaseClientSidePlayerData.instance.PlayerName,
            // get player id
            userAuthId = AuthenticationService.Instance.PlayerId
        };
        string payload = JsonUtility.ToJson(userData);
        // convert the name into bytes to send over the network
        byte[] payLoadBytes = Encoding.UTF8.GetBytes(payload);
        // sent the payload bytes to the server
        NetworkManager.Singleton.NetworkConfig.ConnectionData = payLoadBytes;
    }

    internal void SetUserDataFoTutorial()
    {
        var userData = new UserData
        {
            // get username
            userName = FirebaseClientSidePlayerData.instance.PlayerName,
            // get player id
            userAuthId = AuthenticationService.Instance.PlayerId
            // set game info
        };
        string payload = JsonUtility.ToJson(userData);
        // convert the name into bytes to send over the network
        byte[] payLoadBytes = Encoding.UTF8.GetBytes(payload);
        // sent the payload bytes to the server
        NetworkManager.Singleton.NetworkConfig.ConnectionData = payLoadBytes;
    }

    // keep pinging to UGS when lobby still exist
    private IEnumerator HeartBeatLobby(float waitTimeSeconds)
    {
        WaitForSecondsRealtime delay = new WaitForSecondsRealtime(waitTimeSeconds);

        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    public void Dispose()
    {
        ShutDown();
    }

    public async void ShutDown()
    {

        if (string.IsNullOrEmpty(lobbyId)) return;

        HostSingleton.Instance.StopCoroutine(nameof(HeartBeatLobby));

        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }

        lobbyId = string.Empty;
        NetworkServer?.Dispose();
    }
}
