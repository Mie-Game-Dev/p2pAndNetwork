using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkClient : IDisposable // c# dispose way
{
    private NetworkManager networkManager;
    private const string MenuSceneName = "MainMenu";

    public NetworkClient(NetworkManager networkManager)
    {
        this.networkManager = networkManager;

        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
    }

    // remove the disconnect player data from dictionary
    private void OnClientDisconnect(ulong clientId)
    {
        // check if not host and not local cliebnt id
        if (clientId != 0 && clientId != networkManager.LocalClientId) return;

        Disconnect();
    }

    public void Disconnect()
    {
        // check if in game scene
        if (SceneManager.GetActiveScene().name != MenuSceneName)
        {
            SceneManager.LoadScene(MenuSceneName);
        }

        if (networkManager.IsConnectedClient)
        {
            //Debug.Log("[Relay] I have exited the relay");
            networkManager.Shutdown();
        }
    }

    public void Dispose()
    {
        if(networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        }
    }
}
