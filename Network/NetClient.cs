using System.Net.Sockets;
using Asciifactory.Entities;
using Asciifactory.Items;
using Asciifactory.Machines;
using Asciifactory.WorldGen;

namespace Asciifactory.Network;

/// <summary>
/// TCP game client. Connects to a host server.
/// Sends local player input, receives state snapshots, and provides
/// data for the renderer to draw.
/// </summary>
public class NetClient : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly object _lock = new();
    
    /// <summary>Server IP address.</summary>
    public string Host { get; }
    
    /// <summary>Server port.</summary>
    public int Port { get; }
    
    /// <summary>Whether connected to the server.</summary>
    public bool IsConnected => _tcpClient?.Connected ?? false;
    
    /// <summary>Assigned player index (set after Welcome).</summary>
    public int PlayerIndex { get; private set; } = -1;
    
    /// <summary>World seed (set after game starts).</summary>
    public int WorldSeed { get; private set; }
    
    /// <summary>Latest received game snapshot.</summary>
    public GameSnapshot? LatestSnapshot { get; private set; }
    
    /// <summary>Latest lobby state.</summary>
    public LobbyState? LobbyState { get; private set; }
    
    /// <summary>Whether the game has started (received GameStart).</summary>
    public bool GameStarted { get; private set; }
    
    /// <summary>Whether a victory was received.</summary>
    public bool VictoryReceived { get; private set; }
    public int WinnerIndex { get; private set; } = -1;
    
    /// <summary>Chat messages received.</summary>
    private readonly List<(string From, string Text)> _chatMessages = new();
    public IReadOnlyList<(string From, string Text)> ChatMessages => _chatMessages;
    
    /// <summary>Connection error message (if any).</summary>
    public string? Error { get; private set; }
    
    public NetClient(string host, int port = 7777)
    {
        Host = host;
        Port = port;
    }
    
    /// <summary>
    /// Connects to the server and sends Hello. Returns true on success.
    /// </summary>
    public bool Connect(PlayerInfo info)
    {
        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(Host, Port);
            _stream = _tcpClient.GetStream();
            
            // Send Hello
            SendMessage(ClientMessage.Hello(info));
            
            // Read Welcome response
            var welcome = ReadMessage();
            if (welcome == null || welcome.Type != ServerMessageType.Welcome)
            {
                Error = "Server did not send welcome message.";
                return false;
            }
            
            var welcomeData = NetProtocol.FromJson<WelcomeData>(welcome.Payload ?? "");
            if (welcomeData == null)
            {
                Error = "Invalid welcome data.";
                return false;
            }
            
            PlayerIndex = welcomeData.playerIndex;
            
            // Start background read loop
            Task.Run(ReadLoop);
            
            return true;
        }
        catch (SocketException ex)
        {
            Error = $"Connection failed: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            Error = $"Error: {ex.Message}";
            return false;
        }
    }
    
    private class WelcomeData
    {
        public int playerIndex { get; set; }
        public int worldSeed { get; set; }
    }
    
    /// <summary>
    /// Sends a ready toggle to the server.
    /// </summary>
    public void SendReady()
    {
        SendMessage(ClientMessage.Ready());
    }
    
    /// <summary>
    /// Sends a player input command to the server.
    /// </summary>
    public void SendInput(InputCommand command)
    {
        SendMessage(ClientMessage.Input((int)command));
    }
    
    /// <summary>
    /// Sends a disconnect message.
    /// </summary>
    public void SendDisconnect()
    {
        try
        {
            SendMessage(ClientMessage.Disconnect());
        }
        catch { }
    }
    
    /// <summary>
    /// Drains and returns all chat messages since last call.
    /// </summary>
    public List<(string From, string Text)> DrainChatMessages()
    {
        lock (_lock)
        {
            var msgs = new List<(string From, string Text)>(_chatMessages);
            _chatMessages.Clear();
            return msgs;
        }
    }
    
    // ========== BACKGROUND READ LOOP ==========
    
    private async Task ReadLoop()
    {
        try
        {
            while (_tcpClient?.Connected == true && _stream != null)
            {
                var msg = await ReadMessageAsync(_stream);
                if (msg == null) break;
                
                ProcessServerMessage(msg);
            }
        }
        catch
        {
            // Disconnected
        }
    }
    
    private void ProcessServerMessage(ServerMessage msg)
    {
        switch (msg.Type)
        {
            case ServerMessageType.LobbyUpdate:
                var lobby = NetProtocol.FromJson<LobbyState>(msg.Payload ?? "");
                if (lobby != null)
                    LobbyState = lobby;
                break;
            
            case ServerMessageType.GameStart:
                GameStarted = true;
                if (int.TryParse(msg.Payload, out int seed))
                    WorldSeed = seed;
                break;
            
            case ServerMessageType.StateSnapshot:
                var snapshot = NetProtocol.FromJson<GameSnapshot>(msg.Payload ?? "");
                if (snapshot != null)
                {
                    // Apply tile changes to local world state tracking
                    LatestSnapshot = snapshot;
                }
                break;
            
            case ServerMessageType.Chat:
                var chatData = NetProtocol.FromJson<ChatData>(msg.Payload ?? "");
                if (chatData != null)
                {
                    lock (_lock)
                    {
                        _chatMessages.Add((chatData.from, chatData.text));
                    }
                }
                break;
            
            case ServerMessageType.PlayerLeft:
                // A player disconnected
                break;
            
            case ServerMessageType.Victory:
                VictoryReceived = true;
                if (int.TryParse(msg.Payload, out int winner))
                    WinnerIndex = winner;
                break;
        }
    }
    
    private class ChatData
    {
        public string from { get; set; } = "";
        public string text { get; set; } = "";
    }
    
    // ========== LOW-LEVEL IO ==========
    
    private void SendMessage(ClientMessage msg)
    {
        if (_stream == null) return;
        var data = NetProtocol.Serialize(msg);
        _stream.Write(data, 0, data.Length);
        _stream.Flush();
    }
    
    private ServerMessage? ReadMessage()
    {
        if (_stream == null) return null;
        
        // Read 4-byte length prefix
        var lenBuf = new byte[4];
        ReadExact(_stream, lenBuf, 4);
        
        int len = lenBuf[0] | (lenBuf[1] << 8) | (lenBuf[2] << 16) | (lenBuf[3] << 24);
        if (len <= 0 || len > 10_000_000) return null;
        
        var data = new byte[len];
        ReadExact(_stream, data, len);
        
        return NetProtocol.Deserialize<ServerMessage>(data, 0, len);
    }
    
    private static async Task<ServerMessage?> ReadMessageAsync(NetworkStream stream)
    {
        // Read 4-byte length prefix
        var lenBuf = new byte[4];
        int read = await ReadExactAsync(stream, lenBuf, 4);
        if (read < 4) return null;
        
        int len = lenBuf[0] | (lenBuf[1] << 8) | (lenBuf[2] << 16) | (lenBuf[3] << 24);
        if (len <= 0 || len > 10_000_000) return null;
        
        var data = new byte[len];
        read = await ReadExactAsync(stream, data, len);
        if (read < len) return null;
        
        return NetProtocol.Deserialize<ServerMessage>(data, 0, len);
    }
    
    private static void ReadExact(NetworkStream stream, byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, totalRead, count - totalRead);
            if (read == 0) throw new IOException("Connection closed");
            totalRead += read;
        }
    }
    
    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
            if (read == 0) return totalRead;
            totalRead += read;
        }
        return totalRead;
    }
    
    public void Dispose()
    {
        try { SendDisconnect(); } catch { }
        
        _stream?.Close();
        _tcpClient?.Close();
    }
}