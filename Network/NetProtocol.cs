using System.Text.Json;
using System.Text.Json.Serialization;
using Asciifactory.Entities;
using Asciifactory.Items;
using Asciifactory.Machines;
using Asciifactory.WorldGen;

namespace Asciifactory.Network;

/// <summary>
/// Player colors available in multiplayer.
/// </summary>
public enum PlayerColor
{
    Yellow = 0,
    Cyan = 1,
    Green = 2,
    Magenta = 3,
}

/// <summary>
/// Info about a multiplayer player (setup phase).
/// </summary>
public class PlayerInfo
{
    public string Nickname { get; set; } = "Player";
    public PlayerColor Color { get; set; } = PlayerColor.Yellow;
    public bool IsHost { get; set; }
}

/// <summary>
/// Message types for client → server communication.
/// </summary>
public enum ClientMessageType
{
    /// <summary>Initial handshake: sends PlayerInfo.</summary>
    Hello,
    /// <summary>Player input command.</summary>
    Input,
    /// <summary>Chat message.</summary>
    Chat,
    /// <summary>Player is ready in lobby.</summary>
    Ready,
    /// <summary>Client disconnecting.</summary>
    Disconnect,
}

/// <summary>
/// Message types for server → client communication.
/// </summary>
public enum ServerMessageType
{
    /// <summary>Welcome message with assigned player index.</summary>
    Welcome,
    /// <summary>Lobby state update (players list).</summary>
    LobbyUpdate,
    /// <summary>Game is starting.</summary>
    GameStart,
    /// <summary>Full state snapshot for a tick.</summary>
    StateSnapshot,
    /// <summary>Tile change (mined resource).</summary>
    TileChange,
    /// <summary>Machine placed or removed.</summary>
    MachineChange,
    /// <summary>Chat message from another player.</summary>
    Chat,
    /// <summary>Player disconnected.</summary>
    PlayerLeft,
    /// <summary>Victory!</summary>
    Victory,
}

// ========== CLIENT → SERVER MESSAGES ==========

public class ClientMessage
{
    public ClientMessageType Type { get; set; }
    public string? Payload { get; set; }
    
    public static ClientMessage Hello(PlayerInfo info) => new()
    {
        Type = ClientMessageType.Hello,
        Payload = JsonSerializer.Serialize(info),
    };
    
    public static ClientMessage Input(int inputCommand) => new()
    {
        Type = ClientMessageType.Input,
        Payload = inputCommand.ToString(),
    };
    
    public static ClientMessage Chat(string text) => new()
    {
        Type = ClientMessageType.Chat,
        Payload = text,
    };
    
    public static ClientMessage Ready() => new()
    {
        Type = ClientMessageType.Ready,
    };
    
    public static ClientMessage Disconnect() => new()
    {
        Type = ClientMessageType.Disconnect,
    };
}

// ========== SERVER → CLIENT MESSAGES ==========

public class ServerMessage
{
    public ServerMessageType Type { get; set; }
    public string? Payload { get; set; }
    
    public static ServerMessage Welcome(int playerIndex, int worldSeed) => new()
    {
        Type = ServerMessageType.Welcome,
        Payload = JsonSerializer.Serialize(new { playerIndex, worldSeed }),
    };
    
    public static ServerMessage LobbyUpdate(LobbyState lobby) => new()
    {
        Type = ServerMessageType.LobbyUpdate,
        Payload = JsonSerializer.Serialize(lobby),
    };
    
    public static ServerMessage GameStart(int worldSeed) => new()
    {
        Type = ServerMessageType.GameStart,
        Payload = worldSeed.ToString(),
    };
    
    public static ServerMessage StateSnapshot(GameSnapshot snapshot) => new()
    {
        Type = ServerMessageType.StateSnapshot,
        Payload = JsonSerializer.Serialize(snapshot),
    };
    
    public static ServerMessage TileChange(int x, int y, int tileType) => new()
    {
        Type = ServerMessageType.TileChange,
        Payload = JsonSerializer.Serialize(new { x, y, tileType }),
    };
    
    public static ServerMessage MachineChange(string machineAction) => new()
    {
        Type = ServerMessageType.MachineChange,
        Payload = machineAction,
    };
    
    public static ServerMessage Chat(string from, string text) => new()
    {
        Type = ServerMessageType.Chat,
        Payload = JsonSerializer.Serialize(new { from, text }),
    };
    
    public static ServerMessage PlayerLeft(int playerIndex) => new()
    {
        Type = ServerMessageType.PlayerLeft,
        Payload = playerIndex.ToString(),
    };
    
    public static ServerMessage Victory(int winnerIndex) => new()
    {
        Type = ServerMessageType.Victory,
        Payload = winnerIndex.ToString(),
    };
}

// ========== DATA TRANSFER OBJECTS ==========

/// <summary>
/// Lobby state broadcast to all clients.
/// </summary>
public class LobbyState
{
    public List<LobbyPlayer> Players { get; set; } = new();
}

public class LobbyPlayer
{
    public int Index { get; set; }
    public string Nickname { get; set; } = "Player";
    public PlayerColor Color { get; set; }
    public bool Ready { get; set; }
}

/// <summary>
/// Full game state snapshot sent each tick.
/// Contains everything a client needs to render.
/// </summary>
public class GameSnapshot
{
    /// <summary>Server tick count.</summary>
    public int Tick { get; set; }
    
    /// <summary>All player states.</summary>
    public List<RemotePlayerState> Players { get; set; } = new();
    
    /// <summary>Current player's inventory (only sent to that player).</summary>
    public string? InventoryData { get; set; }
    
    /// <summary>All enemies visible to the player.</summary>
    public List<RemoteEnemyState> Enemies { get; set; } = new();
    
    /// <summary>All projectiles visible to the player.</summary>
    public List<RemoteProjectileState> Projectiles { get; set; } = new();
    
    /// <summary>Machines visible to the player.</summary>
    public List<RemoteMachineState> Machines { get; set; } = new();
    
    /// <summary>Tile changes since last snapshot (sparse).</summary>
    public List<TileChange> TileChanges { get; set; } = new();
    
    /// <summary>Machine changes since last snapshot.</summary>
    public List<MachineAction> MachineChanges { get; set; } = new();
    
    /// <summary>Player health values.</summary>
    public List<int> PlayerHealths { get; set; } = new();
    
    /// <summary>Machine count (for threat level).</summary>
    public int MachineCount { get; set; }
}

public class RemotePlayerState
{
    public int Index { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int FacingX { get; set; }
    public int FacingY { get; set; }
    public string Nickname { get; set; } = "";
    public PlayerColor Color { get; set; }
    public int Health { get; set; }
    public bool IsMining { get; set; }
    public int MiningProgress { get; set; }
    public int MiningTargetX { get; set; }
    public int MiningTargetY { get; set; }
    public int MiningAnimFrame { get; set; }
}

public class RemoteEnemyState
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Type { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int GrowthStage { get; set; }
}

public class RemoteProjectileState
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class RemoteMachineState
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Type { get; set; }
    public int Direction { get; set; }
    public bool IsProcessing { get; set; }
}

public class TileChange
{
    public int X { get; set; }
    public int Y { get; set; }
    public int TileType { get; set; }
}

public class MachineAction
{
    public const string Place = "place";
    public const string Remove = "remove";
    
    public string Action { get; set; } = Place;
    public int X { get; set; }
    public int Y { get; set; }
    public int Type { get; set; }
    public int Direction { get; set; }
    public int TileType { get; set; } // for miners
}

/// <summary>
/// Serialization helpers for the network protocol.
/// Uses length-prefixed JSON messages over TCP.
/// </summary>
public static class NetProtocol
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
    
    public static byte[] Serialize<T>(T msg) where T : class
    {
        string json = JsonSerializer.Serialize(msg, JsonOpts);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
        
        // Length prefix: 4 bytes (little-endian int32) + payload
        byte[] packet = new byte[4 + data.Length];
        packet[0] = (byte)(data.Length & 0xFF);
        packet[1] = (byte)((data.Length >> 8) & 0xFF);
        packet[2] = (byte)((data.Length >> 16) & 0xFF);
        packet[3] = (byte)((data.Length >> 24) & 0xFF);
        Array.Copy(data, 0, packet, 4, data.Length);
        
        return packet;
    }
    
    public static T? Deserialize<T>(byte[] data, int offset, int count) where T : class
    {
        string json = System.Text.Encoding.UTF8.GetString(data, offset, count);
        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }
    
    public static string ToJson<T>(T obj) where T : class => JsonSerializer.Serialize(obj, JsonOpts);
    public static T? FromJson<T>(string json) where T : class => JsonSerializer.Deserialize<T>(json, JsonOpts);
}