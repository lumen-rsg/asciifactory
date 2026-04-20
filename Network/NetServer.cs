using System.Net;
using System.Net.Sockets;
using Asciifactory.Entities;
using Asciifactory.Items;
using Asciifactory.Machines;
using Asciifactory.WorldGen;

namespace Asciifactory.Network;

/// <summary>
/// TCP game server. Runs on the host machine.
/// Accepts client connections, receives inputs, runs authoritative game state,
/// and broadcasts snapshots to all clients.
/// </summary>
public class NetServer : IDisposable
{
    private TcpListener? _listener;
    private readonly List<ConnectedClient> _clients = new();
    private readonly object _lock = new();
    
    /// <summary>Max players including host.</summary>
    public const int MaxPlayers = 4;
    
    /// <summary>Port the server listens on.</summary>
    public int Port { get; }
    
    /// <summary>Whether the server is running.</summary>
    public bool IsRunning { get; private set; }
    
    /// <summary>Lobby state (player list).</summary>
    private readonly LobbyState _lobby = new();
    
    /// <summary>Per-player input queues (thread-safe).</summary>
    private readonly List<List<int>> _playerInputs = new();
    
    /// <summary>World seed set when game starts.</summary>
    public int WorldSeed { get; private set; }
    
    /// <summary>Whether the game has started.</summary>
    public bool GameStarted { get; private set; }
    
    public NetServer(int port = 7777)
    {
        Port = port;
    }
    
    /// <summary>
    /// Connected client data.
    /// </summary>
    private class ConnectedClient
    {
        public TcpClient TcpClient { get; }
        public NetworkStream Stream { get; }
        public int PlayerIndex { get; }
        public PlayerInfo Info { get; }
        public bool Ready { get; set; }
        public bool Connected { get; set; } = true;
        
        public ConnectedClient(TcpClient tcpClient, int playerIndex, PlayerInfo info)
        {
            TcpClient = tcpClient;
            Stream = tcpClient.GetStream();
            PlayerIndex = playerIndex;
            Info = info;
        }
    }
    
    // ========== LOBBY PHASE ==========
    
    /// <summary>
    /// Starts listening for connections. Non-blocking.
    /// </summary>
    public void Start()
    {
        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        IsRunning = true;
        
        // Accept connections asynchronously
        Task.Run(AcceptLoop);
    }
    
    /// <summary>
    /// Adds the host player to the lobby.
    /// </summary>
    public void AddHostPlayer(PlayerInfo hostInfo)
    {
        _lobby.Players.Add(new LobbyPlayer
        {
            Index = 0,
            Nickname = hostInfo.Nickname,
            Color = hostInfo.Color,
            Ready = false,
        });
        
        // Initialize input queue for host (player 0)
        _playerInputs.Add(new List<int>());
    }
    
    /// <summary>
    /// Sets the host ready state.
    /// </summary>
    public void SetHostReady(bool ready)
    {
        if (_lobby.Players.Count > 0)
            _lobby.Players[0].Ready = ready;
    }
    
    /// <summary>
    /// Gets current lobby state.
    /// </summary>
    public LobbyState GetLobbyState() => new()
    {
        Players = _lobby.Players.Select(p => new LobbyPlayer
        {
            Index = p.Index,
            Nickname = p.Nickname,
            Color = p.Color,
            Ready = p.Ready,
        }).ToList(),
    };
    
    /// <summary>
    /// Starts the game. Generates world seed and notifies all clients.
    /// </summary>
    public void StartGame()
    {
        if (WorldSeed == 0)
            WorldSeed = Random.Shared.Next();
        GameStarted = true;
        
        // Send GameStart to all clients with the world seed
        Broadcast(ServerMessage.GameStart(WorldSeed));
    }
    
    /// <summary>
    /// Whether all connected players are ready.
    /// </summary>
    public bool AllReady => _lobby.Players.All(p => p.Ready);
    
    public int PlayerCount => _lobby.Players.Count;
    
    // ========== ACCEPT LOOP ==========
    
    private async Task AcceptLoop()
    {
        while (IsRunning)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClient(tcpClient));
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                // Accept failed, continue
            }
        }
    }
    
    private async Task HandleClient(TcpClient tcpClient)
    {
        int playerIndex = -1;
        NetworkStream? stream = null;
        
        try
        {
            stream = tcpClient.GetStream();
            
            // Read first message - must be Hello
            var msg = await ReadMessageAsync(stream);
            if (msg == null || msg.Type != ClientMessageType.Hello)
            {
                tcpClient.Close();
                return;
            }
            
            var info = NetProtocol.FromJson<PlayerInfo>(msg.Payload ?? "");
            if (info == null)
            {
                tcpClient.Close();
                return;
            }
            
            lock (_lock)
            {
                if (_lobby.Players.Count >= MaxPlayers || GameStarted)
                {
                    tcpClient.Close();
                    return;
                }
                
                playerIndex = _lobby.Players.Count;
                
                var client = new ConnectedClient(tcpClient, playerIndex, info);
                _clients.Add(client);
                
                _lobby.Players.Add(new LobbyPlayer
                {
                    Index = playerIndex,
                    Nickname = info.Nickname,
                    Color = info.Color,
                    Ready = false,
                });
                
                _playerInputs.Add(new List<int>());
            }
            
            // Send Welcome
            SendMessage(stream, ServerMessage.Welcome(playerIndex, 0)); // Seed sent at game start
            
            // Broadcast lobby update
            Broadcast(ServerMessage.LobbyUpdate(GetLobbyState()));
            
            // Read loop
            while (tcpClient.Connected && IsRunning)
            {
                var clientMsg = await ReadMessageAsync(stream);
                if (clientMsg == null) break;
                
                ProcessClientMessage(playerIndex, clientMsg);
            }
        }
        catch (Exception)
        {
            // Client disconnected or error
        }
        finally
        {
            if (playerIndex >= 0)
            {
                lock (_lock)
                {
                    var client = _clients.FirstOrDefault(c => c.PlayerIndex == playerIndex);
                    if (client != null)
                    {
                        client.Connected = false;
                        _clients.Remove(client);
                    }
                    
                    var lp = _lobby.Players.FirstOrDefault(p => p.Index == playerIndex);
                    if (lp != null)
                        _lobby.Players.Remove(lp);
                }
                
                Broadcast(ServerMessage.PlayerLeft(playerIndex));
            }
            
            stream?.Close();
            tcpClient.Close();
        }
    }
    
    private void ProcessClientMessage(int playerIndex, ClientMessage msg)
    {
        switch (msg.Type)
        {
            case ClientMessageType.Input:
                if (int.TryParse(msg.Payload, out int inputCmd))
                {
                    lock (_lock)
                    {
                        if (playerIndex < _playerInputs.Count)
                            _playerInputs[playerIndex].Add(inputCmd);
                    }
                }
                break;
            
            case ClientMessageType.Ready:
                lock (_lock)
                {
                    var lp = _lobby.Players.FirstOrDefault(p => p.Index == playerIndex);
                    if (lp != null) lp.Ready = !lp.Ready;
                    Broadcast(ServerMessage.LobbyUpdate(GetLobbyState()));
                }
                break;
            
            case ClientMessageType.Chat:
                var lp2 = _lobby.Players.FirstOrDefault(p => p.Index == playerIndex);
                string name = lp2?.Nickname ?? "???";
                Broadcast(ServerMessage.Chat(name, msg.Payload ?? ""));
                break;
            
            case ClientMessageType.Disconnect:
                // Will be handled by the read loop ending
                break;
        }
    }
    
    // ========== GAME PHASE - INPUT COLLECTION ==========
    
    /// <summary>
    /// Collects and drains all queued inputs for a player. Thread-safe.
    /// </summary>
    public List<int> DrainInputs(int playerIndex)
    {
        lock (_lock)
        {
            if (playerIndex >= _playerInputs.Count) return new();
            var inputs = new List<int>(_playerInputs[playerIndex]);
            _playerInputs[playerIndex].Clear();
            return inputs;
        }
    }
    
    // ========== GAME PHASE - STATE BROADCAST ==========
    
    /// <summary>
    /// Broadcasts a game snapshot to all connected clients.
    /// Each client gets a personalized snapshot with their own inventory.
    /// </summary>
    public void BroadcastSnapshot(
        List<Player> players,
        EnemyManager enemyManager,
        MachineGrid machineGrid,
        World world,
        List<TileChange> tileChanges,
        List<MachineAction> machineChanges,
        int tickCount)
    {
        // Build common state
        var playerStates = players.Select(p => new RemotePlayerState
        {
            Index = p.PlayerIndex,
            X = p.X,
            Y = p.Y,
            FacingX = p.FacingX,
            FacingY = p.FacingY,
            Nickname = p.Nickname,
            Color = p.PlayerColor,
            Health = p.Health,
            IsMining = p.IsMining,
            MiningProgress = p.MiningProgress,
            MiningTargetX = p.MiningTargetX,
            MiningTargetY = p.MiningTargetY,
            MiningAnimFrame = p.MiningAnimFrame,
        }).ToList();
        
        var enemyStates = enemyManager.Enemies.Select(e => new RemoteEnemyState
        {
            X = e.X,
            Y = e.Y,
            Type = (int)e.Type,
            Health = e.Health,
            MaxHealth = e.MaxHealth,
            GrowthStage = e.GrowthStage,
        }).ToList();
        
        var projStates = enemyManager.Projectiles.Select(p => new RemoteProjectileState
        {
            X = p.X,
            Y = p.Y,
        }).ToList();
        
        var machineStates = machineGrid.GetAllMachines().Select(m => new RemoteMachineState
        {
            X = m.X,
            Y = m.Y,
            Type = (int)m.Type,
            Direction = (int)m.Direction,
            IsProcessing = m.IsProcessing,
        }).ToList();
        
        // Send personalized snapshot to each client
        foreach (var client in _clients.Where(c => c.Connected).ToList())
        {
            var snapshot = new GameSnapshot
            {
                Tick = tickCount,
                Players = playerStates,
                InventoryData = client.PlayerIndex < players.Count
                    ? SerializeInventory(players[client.PlayerIndex].Inventory)
                    : null,
                Enemies = enemyStates,
                Projectiles = projStates,
                Machines = machineStates,
                TileChanges = tileChanges,
                MachineChanges = machineChanges,
                PlayerHealths = players.Select(p => p.Health).ToList(),
                MachineCount = machineGrid.MachineCount,
            };
            
            try
            {
                SendMessage(client.Stream, ServerMessage.StateSnapshot(snapshot));
            }
            catch
            {
                client.Connected = false;
            }
        }
    }
    
    /// <summary>
    /// Broadcasts a victory message to all clients.
    /// </summary>
    public void BroadcastVictory(int winnerIndex)
    {
        Broadcast(ServerMessage.Victory(winnerIndex));
    }
    
    private static string SerializeInventory(Inventory inventory)
    {
        var slots = inventory.GetFilledSlots()
            .Select(s => $"{(int)s.Stack.Id}:{s.Stack.Count}")
            .ToList();
        return string.Join(";", slots);
    }
    
    // ========== LOW-LEVEL IO ==========
    
    private void SendMessage(NetworkStream stream, ServerMessage msg)
    {
        var data = NetProtocol.Serialize(msg);
        stream.Write(data, 0, data.Length);
        stream.Flush();
    }
    
    private void Broadcast(ServerMessage msg)
    {
        var data = NetProtocol.Serialize(msg);
        foreach (var client in _clients.Where(c => c.Connected).ToList())
        {
            try
            {
                client.Stream.Write(data, 0, data.Length);
                client.Stream.Flush();
            }
            catch
            {
                client.Connected = false;
            }
        }
    }
    
    private async Task<ClientMessage?> ReadMessageAsync(NetworkStream stream)
    {
        // Read 4-byte length prefix
        var lenBuf = new byte[4];
        int read = await ReadExactAsync(stream, lenBuf, 4);
        if (read < 4) return null;
        
        int len = lenBuf[0] | (lenBuf[1] << 8) | (lenBuf[2] << 16) | (lenBuf[3] << 24);
        if (len <= 0 || len > 10_000_000) return null; // Sanity check: max 10MB
        
        var data = new byte[len];
        read = await ReadExactAsync(stream, data, len);
        if (read < len) return null;
        
        return NetProtocol.Deserialize<ClientMessage>(data, 0, len);
    }
    
    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
            if (read == 0) return totalRead; // Connection closed
            totalRead += read;
        }
        return totalRead;
    }
    
    public void Dispose()
    {
        IsRunning = false;
        _listener?.Stop();
        
        foreach (var client in _clients)
        {
            try
            {
                client.Stream.Close();
                client.TcpClient.Close();
            }
            catch { }
        }
        
        _clients.Clear();
    }
}