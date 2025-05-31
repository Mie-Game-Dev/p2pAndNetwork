using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DebugManager : MonoBehaviour
{
    public static DebugManager Instance;
    public bool isDedicatedServer;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }
}
