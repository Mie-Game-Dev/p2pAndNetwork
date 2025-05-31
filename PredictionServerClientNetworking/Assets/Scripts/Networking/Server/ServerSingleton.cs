using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;

public class ServerSingleton : MonoBehaviour
{
    private static ServerSingleton instance;
    [SerializeField] private int port;
    public ServerGameManager GameManager { get; private set; }

    public static ServerSingleton Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindObjectOfType<ServerSingleton>();

            if(instance == null)
            {
                return null;
            }

            return instance;
        }
    }

    void Start()
    {
        DontDestroyOnLoad(gameObject);
        // print("I am the server singleton");
    }

    // task to ensure run this before running server setup
    public async Task CreateServer(NetworkObject playerPrefab)
    {
        // initialize server
        await UnityServices.InitializeAsync();

        //setup ip,port,qport,singleton
        GameManager = new ServerGameManager(
            ApplicationData.IP(),
            ApplicationData.Port(),
            ApplicationData.QPort(),
            NetworkManager.Singleton,
            playerPrefab);

        // print("[server] intialize ServerGameManager");
    }

    [ServerRpc(RequireOwnership = false)]
    public void AssignTeamServerRpc(FixedString64Bytes userAuthId, int team)
    {
        GameManager.NetworkServer.GetClientDataByAuthId(userAuthId).userGamePreferences.teamIndex = team;
    }

    private void OnDestroy()
    {
        GameManager?.Dispose();
    }
}
