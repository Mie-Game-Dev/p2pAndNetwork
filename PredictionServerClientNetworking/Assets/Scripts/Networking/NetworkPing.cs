using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;
using System.Threading;
using System.Collections;

public class NetworkPing : MonoBehaviour
{
    public TextMeshProUGUI pingStatusText;
    public Image pingIcon;
    public Sprite goodPingSprite;
    public Color goodColor;
    public Color mediumColor;
    public Color highColor;
    public Sprite mediumPingSprite;
    public Sprite badPingSprite;

    private SynchronizationContext mainThreadContext;
    private Thread pingThread;
    private bool isRunning = true;
    public enum PingStatus
    {
        goodPing,
        mediumPing,
        badPing
    }

    public static PingStatus CurrentPingStatus { get; private set; }

    private void Start()
    {
        StartCoroutine(WaitForNetworkManager());
    }

    private IEnumerator WaitForNetworkManager()
    {
        while (!NetworkManager.Singleton.IsListening)
        {
            yield return null; // Wait for the next frame
        }

        if (NetworkManager.Singleton.IsServer) yield break;

        // Cache the main thread context
        mainThreadContext = SynchronizationContext.Current;

        // Start a background thread to update ping status
        pingThread = new Thread(UpdatePingStatus);
        pingThread.Start();
    }

    private void UpdatePingStatus()
    {
        while (isRunning)
        {
            try
            {
                int ping = GameCalculationUtils.GetPingStatus();

                // Update UI on the main thread
                if (mainThreadContext != null)
                {
                    mainThreadContext.Post(_ => UpdateUI(ping), null);
                }
                else
                {
                    //Debug.LogError("Main thread context is null.");
                }

                // Wait for some time before checking ping again
                Thread.Sleep(1000); // Adjust as needed
            }
            catch (System.Exception e)
            {
               // Debug.LogError("Exception in UpdatePingStatus: " + e);
            }
        }
    }

    private void UpdateUI(int ping)
    {
        // Choose sprite based on ping
        Sprite sprite = null;
        if (ping <= 100)
        {
            if(CurrentPingStatus != PingStatus.goodPing)
            {
                CurrentPingStatus = PingStatus.goodPing;
                sprite = goodPingSprite;
                pingStatusText.color = goodColor; 
            }
        }
        else if (ping > 100 && ping <= 180)
        {
            if(CurrentPingStatus != PingStatus.mediumPing)
            {
                CurrentPingStatus = PingStatus.mediumPing;
                sprite = mediumPingSprite;
                pingStatusText.color = mediumColor;
            }
            
        }
        else
        {
            if(CurrentPingStatus != PingStatus.badPing)
            {
                CurrentPingStatus = PingStatus.badPing;
                sprite = badPingSprite;
                pingStatusText.color = highColor;
            }
        }

        // Update UI elements
        if(sprite != null) pingIcon.sprite = sprite;
        pingStatusText.text = $"{ping}ms";
    }

    private void OnDisable()
    {
        // Gracefully stop the thread
        isRunning = false;
        if (pingThread != null && pingThread.IsAlive)
        {
            pingThread.Join();
        }
        //print("on disable thread");
    }

    private void OnDestroy()
    {
        // Gracefully stop the thread
        isRunning = false;
        if (pingThread != null && pingThread.IsAlive)
        {
            pingThread.Join();
        }
        //print("on destroy thread");
    }
}
