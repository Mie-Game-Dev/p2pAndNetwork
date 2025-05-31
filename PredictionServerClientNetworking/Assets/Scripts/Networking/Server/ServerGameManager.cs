using Proyecto26;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class ServerGameManager : IDisposable
{
    private string serverIP;
    private int serverPort;
    private int queryPort;
    private MatchplayBackfiller backfiller;
#if UNITY_SERVER
    public MultiplayAllocationService multiplayAllocationService;//handle health of server
#endif
    private Dictionary<string, int> teamIdToTeamIndex = new Dictionary<string, int>();
    public string FirebaseIDToken { get; private set; }

    public NetworkServer NetworkServer { get; private set; }

    const string Firebase_API = "AIzaSyCONcZg8X-V3WrdisC2rgwS2aRjBhHYzKc";

    public ServerGameManager(string serverIP, int serverPort, int queryPort, NetworkManager manager, NetworkObject playerPrefab)
    {
        this.serverIP = serverIP;
        this.serverPort = serverPort;
        this.queryPort = queryPort;
        NetworkServer = new NetworkServer(manager, playerPrefab);
#if UNITY_SERVER
        multiplayAllocationService = new MultiplayAllocationService();
#endif
    }


    // call once sucessfully connect to ugs
    public async Task StartGameServerAsync()
    {

#if UNITY_SERVER
        await multiplayAllocationService.BeginServerCheck();
#endif

        // Wait until ForceSamePrefabs is set to true
        while (!NetworkManager.Singleton.NetworkConfig.ForceSamePrefabs)
        {
            Debug.LogWarning("Waiting for ForceSamePrefabs to be enabled...");
            await Task.Delay(1000); // Check every second
        }

        try
        {
#if UNITY_SERVER

            // get match making data
            MatchmakingResults matchmakerPayLoad = await GetMatchmakerPayload();
            // chekc if matchmaker payload sucessfully get
            if (matchmakerPayLoad != null)
            {
                // backfilling : getting more player get into games
                await StartBackfill(matchmakerPayLoad);
                NetworkServer.OnUserJoin += UserJoined;
                NetworkServer.OnUserLeft += UserLeft;
            }
            else
            {
                Debug.LogWarning("Matchmaker payload timed out");
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
        }

        // check server open? 
        if (!NetworkServer.OpenConnection(serverIP, serverPort))
        {
            Debug.LogWarning("Network server did not start as exoected");
            return;
        }

        LoginFirebaseAsGuest();
    }

    private void LoginFirebaseAsGuest()
    {
        string url = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + Firebase_API;

        // Construct JSON data
        string postData = "{\"returnSecureToken\":true}";

        // Create RequestHelper instance
        RequestHelper requestHelper = new RequestHelper
        {
            Uri = url,
            BodyString = postData,
            ContentType = "application/json"
        };

        // Send the request using Proyecto26
        RestClient.Post<SignInResponse>(requestHelper).Then(response =>
        {
            //Save the token for future use
            FirebaseIDToken = response.idToken;
            Debug.Log($"[Firebase] {response.localId}");
        });
    }

#if UNITY_SERVER
    // get matchmaking
    private async Task<MatchmakingResults> GetMatchmakerPayload()
    {
        Task<MatchmakingResults> matchmakerPayloadTask = multiplayAllocationService.SubscribeAndAwaitMatchmakerAllocation();


        // to ensure the matchmaking result not keep hangging
        if (await Task.WhenAny(matchmakerPayloadTask, Task.Delay(20000)) == matchmakerPayloadTask) // if our match makerpayload equal self or less than delay 20s : if not then it will shutdown
        {
            return matchmakerPayloadTask.Result;
        }

        return null;
    }
#endif

    private async Task StartBackfill(MatchmakingResults payload)
    {
        backfiller = new MatchplayBackfiller($"{serverIP}:{serverPort}", payload.QueueName, payload.MatchProperties, 20);

        var amountbot = 10 - backfiller.MatchPlayerCount;
        Debug.Log($"[ServerGameManager] MatchPlayerCount {backfiller.MatchPlayerCount}");
        Debug.Log($"[ServerGameManager] Bot amount {amountbot}");
        // Start a coroutine to add bots one by one
        NetworkManager.Singleton.StartCoroutine(AddBotsOneByOne(amountbot));

        // check if player max reach or not?
        if (backfiller.NeedsPlayers())
        {
            await backfiller.BeginBackfilling();
        }
    }

    private IEnumerator AddBotsOneByOne(int amount)
    {
        yield return new WaitForSeconds(5f); 

        for (int i = 0; i < amount; i++)
        {
            ServerMatchmakingHandler.Instance.playerEnterCount.Value += 1;
            yield return new WaitForSeconds(2f); 
        }
    }

    private void UserJoined(UserData user)
    {
        //backfiller.AddPlayerToMatch(user);
        // detect team id

        Team team = backfiller.GetTeamByUserId(user.userAuthId);
        Debug.Log($"{user.userAuthId}||||{team.TeamId} has joined the game");

        // convert team id to teamindex
        if (!teamIdToTeamIndex.TryGetValue(team.TeamId, out int teamIndex)) // check if team already in dictionary
        {
            teamIndex = teamIdToTeamIndex.Count;
            teamIdToTeamIndex.Add(team.TeamId, teamIndex);
        }

        // if team available directly assign
        user.userGamePreferences.teamIndex = teamIndex;

#if UNITY_SERVER 
        //add into multiplay allocation to keep track on the player in server
        multiplayAllocationService.AddPlayer();
#endif

        if (!backfiller.NeedsPlayers() && backfiller.IsBackfilling)
        {
            // _ : means discard the return value
            _ = backfiller.StopBackfill();

        }
    }

    private void UserLeft(UserData user)
    {
        int playerCount = backfiller.RemovePlayerFromMatch(user.userAuthId);
#if UNITY_SERVER
        multiplayAllocationService.RemovePlayer();
#endif

        if (playerCount <= 0 && GameManager.instance != null && GameManager.instance.GameState.Value == E_GameState.End)
        {
            // close server
            CloseServer();
            return;
        }

        if (backfiller.NeedsPlayers() && !backfiller.IsBackfilling)
        {
            _ = backfiller.BeginBackfilling();
        }
    }

    public async void CloseServer()
    {
        await backfiller.StopBackfill();
        Dispose();
        Application.Quit();
    }

    public void Dispose()
    {
        NetworkServer.OnUserJoin -= UserJoined;
        NetworkServer.OnUserLeft -= UserLeft;

        backfiller?.Dispose();
#if UNITY_SERVER
        multiplayAllocationService?.Dispose();
#endif
        NetworkServer?.Dispose();
    }
}
