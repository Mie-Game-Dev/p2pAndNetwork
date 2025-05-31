using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Map
{
    Default
}

public enum GameMode
{
    Mode3v3,
    Mode5v5,
    Mode3v3Ai,
    Mode1v5Ai,
    Mode2v5Ai,
    Mode3v5Ai,
    Mode4v5Ai,
    Mode5v5Ai,
    Mode3v3Custom,
    Mode5v5Custom,
}

public enum GameType
{
    Custom,
    Classic,
    Ranked,
    VsAi,
    Practice,
    Tutorial
}

public enum GameQueue
{
    Queue3v3,
    Queue5v5,
    Queue5v5Ranked,
    Queue3v3Ai,
    Queue1v5Ai,
    Queue2v5Ai,
    Queue3v5Ai,
    Queue4v5Ai,
    Queue5v5Ai,
    Queue3v3Custom,
    Queue5v5Custom,
}

[Serializable]
public class UserData
{
    public bool reconnect;
    public string userName;
    public string userAuthId;
    // set -1 to set the team is by default not available
    public GameInfo userGamePreferences = new GameInfo();

    public ulong clientId;
    public int characterId = 1;
    public int skinId = 0;
    public int artifakId = 1;
}

[Serializable]
public class BotData
{
    public string userName;
    // set -1 to set the team is by default not available
    public int teamIndex = -1;

    public ulong networkObjectId;
    public int characterId = 1;
    public int artifakId = 1;
}

[Serializable]
public class GameInfo
{
    public Map map;
    public GameMode gameMode;
    public GameQueue gameQueue;
    public int rank;
    public int teamIndex = -1;
    public E_LobbyRoles userRole;

    // the case name very sensitive :> it is based on unity dashboard matchmaking name setup
    public string ToMultiplayQueue()
    {
        return gameQueue switch
        {
            GameQueue.Queue3v3 => "3v3-queue", // solo state queue
            GameQueue.Queue5v5 => "5v5-queue", // team state queue
            GameQueue.Queue5v5Ranked => "5v5-queue-Ranked", // team state queue
            GameQueue.Queue3v3Ai => "3v3-queue-Ai", // team state queue
            GameQueue.Queue1v5Ai => "1v5-queue-Ai", // team state queue
            GameQueue.Queue2v5Ai => "2v5-queue-Ai", // team state queue
            GameQueue.Queue3v5Ai => "3v5-queue-Ai", // team state queue
            GameQueue.Queue4v5Ai => "4v5-queue-Ai", // team state queue
            GameQueue.Queue5v5Ai => "5v5-queue-Ai", // team state queue
            GameQueue.Queue3v3Custom => "3v3-queue-Custom", // team state queue
            GameQueue.Queue5v5Custom => "5v5-queue-Custom", // team state queue
            _ => "solo-queue" // default state when
        };
    }
}