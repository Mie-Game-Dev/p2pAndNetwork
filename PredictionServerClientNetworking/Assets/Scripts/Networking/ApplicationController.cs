using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ApplicationController : MonoBehaviour
{
    private static ApplicationController instance;
    [SerializeField] private ClientSingleton clientPrefab;
    [SerializeField] private HostSingleton hostPrefab;
    [SerializeField] private ServerSingleton serverPrefab;
    [SerializeField] private NetworkObject playerPrefab;
    private ApplicationData appData;
    [SerializeField] private SO_GameSettings gameSetting;

    private const string GameSceneName = "PreEnterRoomTemp";

    [Header("Debug Testing")]
    [SerializeField] private bool isServer;

    public SO_GameSettings GameSettings => gameSetting;
    public bool IsServer => isServer;

    public static ApplicationController Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindObjectOfType<ApplicationController>();

            if (instance == null)
            {
                return null;
            }

            return instance;
        }
    }

    private async void Start()
    {
        DontDestroyOnLoad(gameObject);

        print("Launching a singleton. . . ");

#if !UNITY_EDITOR
        await LaunchInMode(Application.platform == RuntimePlatform.LinuxServer);
#else
        await LaunchInMode(isServer);
#endif
    }

    // determine using relay or dedicated server
    private async Task LaunchInMode(bool isDedicatedServer)
    {
        print("[server] launch mode");
        if (isDedicatedServer)
        {
            print("[server] Instatiate singleton");
            // set framerate to 60fps
            Application.targetFrameRate = 60;

            // create application data for port ip
            appData = new ApplicationData();

            // try to create server
            ServerSingleton serverSingleton = Instantiate(serverPrefab);
            if (serverSingleton != null)
            {
                print("[server] server singleton created");
            }
            else
            {
                print("[server] server singleton fail tp create");
            }
            StartCoroutine(LoadGameSceneAsync(serverSingleton));
        }
        else
        {
            HostSingleton hostSingleton = Instantiate(hostPrefab);
            hostSingleton.CreateHost(playerPrefab);
            print("[client] Spawn playerprefab");

            ClientSingleton clientSingleton = Instantiate(clientPrefab);
            bool autheticated = await clientSingleton.CreateClient();

            if (autheticated)
            {
                clientSingleton.ClientGameManager.GoToMenu();

                //Initialize friends from net bootstrap after being authenticated
                FirebaseClientSidePlayerData.instance.InitiliazeFriendSystem();
            }
        }
    }

    // wait5ing for the scene to fully loaded
    private IEnumerator LoadGameSceneAsync(ServerSingleton serverSingleton)
    {
        AsyncOperation asyncOperation = null;

        asyncOperation = SceneManager.LoadSceneAsync(GameSceneName);

        // wait for next frame and check if its still null// if its done then it will stop loop
        while (!asyncOperation.isDone)
        {
            yield return null;
        }


        Task createServerTask = serverSingleton.CreateServer(playerPrefab);
        yield return new WaitUntil(() => createServerTask.IsCompleted);

        // start up the game server
        Task startServerTask = serverSingleton.GameManager.StartGameServerAsync();
        yield return new WaitUntil(() => startServerTask.IsCompleted);
    }
}