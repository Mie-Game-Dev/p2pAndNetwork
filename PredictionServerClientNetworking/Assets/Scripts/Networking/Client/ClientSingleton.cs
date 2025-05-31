using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientSingleton : MonoBehaviour
{
    private static ClientSingleton instance;
    public ClientGameManager ClientGameManager { get; private set; }
    public MatchmakingResult matchmakingResult { get; set; }

    public static ClientSingleton Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindObjectOfType<ClientSingleton>();

            if(instance == null)
            {
                return null;
            }

            return instance;
        }
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Debug.Log("[client] subscribing to client callback");
// #if!UNITY_EDITOR
        
        SceneManager.sceneLoaded -= HandleSceneChanged;
        SceneManager.sceneLoaded += HandleSceneChanged;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleServerDisconnect;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleServerDisconnect;
        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
// #endif
    }

    private void HandleClientConnected(ulong obj)
    {
        InternetConnectionChecker.instance.CloseWarning();
    }

    private void HandleServerDisconnect(ulong id)
    {
       if(id == 0)
       {
            ClientGameManager.OnGameEnded();
       }
    }

    private void HandleSceneChanged(Scene arg0, LoadSceneMode arg1)
    {
        GameEndedCallback();

        if(arg0.name == "MainMenu")
        {
            StartCoroutine(ClientGameManager.OnGameStarted());
        }
    }

    private void GameEndedCallback()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.OnEndGame -= ClientGameManager.OnGameEnded;
            GameManager.instance.OnEndGame += ClientGameManager.OnGameEnded;
        }
    }

    public async Task<bool> CreateClient()
    {
        ClientGameManager = new ClientGameManager();
        await ClientGameManager.InitAsync();
        return true;
    }

    private void OnDestroy()
    {
        // #if !UNITY_EDITOR
        SceneManager.sceneLoaded -= HandleSceneChanged;
        if(GameManager.instance != null )
        {
            GameManager.instance.OnEndGame -= ClientGameManager.OnGameEnded;
        }
// #endif
        ClientGameManager?.Dispose();
    }
}
