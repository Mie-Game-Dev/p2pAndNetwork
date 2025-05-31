using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using UnityEngine;

public class PingIP : MonoBehaviour
{
    public string ipAddress = "192.168.3.184"; // Replace with the static IP you want to ping
    private System.Net.NetworkInformation.Ping pingSender;
    private PingOptions options;
    private byte[] buffer;
    private int timeout = 120; // Time in milliseconds
    private bool isPinging;

    void Start()
    {
        pingSender = new System.Net.NetworkInformation.Ping();
        options = new PingOptions();
        buffer = new byte[32]; // Default size of the ping buffer
        isPinging = true;
        StartPinging();
    }

    async void StartPinging()
    {
        while (isPinging)
        {
            await PingIPAddress(ipAddress);
            await Task.Delay(1000); // Wait for 1 second
        }
    }

    async Task PingIPAddress(string ip)
    {
        PingReply reply = await Task.Run(() => pingSender.Send(ip, timeout, buffer, options));
        if (reply.Status == IPStatus.Success)
        {
            UnityEngine.Debug.Log($"Ping to {ip} successful. Roundtrip time: {reply.RoundtripTime} ms");
        }
        else
        {
            UnityEngine.Debug.Log($"Ping to {ip} failed. Status: {reply.Status}");
        }
    }

    void OnDestroy()
    {
        isPinging = false;
    }

    public string sharedFolderName = "SharedFolder"; // Replace with the shared folder name

    public void OpenFolder()
    {
        string folderPath = $"\\\\{ipAddress}\\{sharedFolderName}";
        Process.Start("explorer.exe", folderPath);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            OpenFolder();
        }
    }
}
