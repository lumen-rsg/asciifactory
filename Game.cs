using System.Text.Json;
using System.Text.Json.Serialization;
using Asciifactory.Entities;
using Asciifactory.Items;
using Asciifactory.Machines;
using Asciifactory.Network;
using Asciifactory.WorldGen;

namespace Asciifactory;

/// <summary>
/// Game state for overlay screens.
/// </summary>
public enum GameState
{
    Playing,
    Help,
    Inventory,
    Crafting,
    Building,
    Scanning,
    MachineInteract,
    Victory,
    PauseMenu,
}

/// <summary>
/// Main game class. Manages the game loop, state, and coordinates all systems.
/// </summary>
public class Game
{
    private readonly Renderer _renderer;
    private readonly InputHandler _input;
    private World _world = null!;
    private Player _player = null!;
    private MachineGrid _machineGrid = null!;
    private EnemyManager _enemyManager = null!;
    private Camera _camera;
    
    private bool _running = true;
    private GameState _state = GameState.Playing;
    private int _tickRate;
    private bool _enemiesEnabled;
    private int _craftSelection;
    private int _buildSelection;
    private Direction _buildDirection = Direction.Right;
    private int _pauseSelection;
    private bool _returnToMainMenu;
    private int _buildCursorX;
    private int _buildCursorY;
    
    private int _moveCooldown;
    private const int MoveDelay = 1;
    
    private int _tickCount;
    private bool _hasWon;
    private readonly TerrainScanner _scanner = new();
    
    // Machine interaction state
    private MachineBase? _interactMachine;
    private int _fuseResetAnimTimer;
    
    private static readonly MachineType[] BuildableTypes = new[]
    {
        MachineType.Miner,
        MachineType.ConveyorBelt,
        MachineType.Furnace,
        MachineType.Assembler,
        MachineType.StorageChest,
        MachineType.BiomassBurner,
        MachineType.CoalGenerator,
        MachineType.NuclearReactor,
        MachineType.Wire,
        MachineType.Lab,
        MachineType.Refinery,
        MachineType.FirewallTurret,
    };
    
    private const string SaveFile = "save.json";
    
    // Multiplayer
    private NetServer? _server;
    private NetClient? _client;
    private bool _isMultiplayer;
    private bool _isHost;
    private List<Player> _remotePlayers = new();
    
    // Multiplayer change tracking (host only — cleared each tick after broadcast)
    private readonly List<TileChange> _tickTileChanges = new();
    private readonly List<MachineAction> _tickMachineChanges = new();
    
    // Persistent tile change tracking for save/load
    private readonly Dictionary<(int x, int y), TileType> _tileChanges = new();
    
    public Game(GameSettings? settings = null)
    {
        _tickRate = settings?.TickRate ?? 50;
        _enemiesEnabled = settings?.EnemiesEnabled ?? true;
        
        _renderer = new Renderer();
        _input = new InputHandler();
        
        // Try to load save, otherwise create new
        if (!TryLoadGame())
        {
            int seed = Random.Shared.Next();
            _world = new World(seed);
            _player = new Player(0, 0);
            _machineGrid = new MachineGrid();
            _enemyManager = new EnemyManager();
        }
        
        // God mode from settings (CLI --god flag)
        if (settings?.GodMode == true)
        {
            _player.GodMode = true;
        }
        
        _camera = new Camera(_renderer.Width, _renderer.Height);
    }
    
    /// <summary>
    /// Sets up a multiplayer host game with the given server and config.
    /// </summary>
    public void SetupMultiplayerHost(NetServer server, MultiplayerConfig config)
    {
        _server = server;
        _isMultiplayer = true;
        _isHost = true;
        
        _player.Nickname = config.Nickname;
        _player.PlayerColor = config.Color;
        _player.PlayerIndex = 0;
        
        // Use the server's world seed
        _world = new World(server.WorldSeed);
        _player = new Player(0, 0)
        {
            Nickname = config.Nickname,
            PlayerColor = config.Color,
            PlayerIndex = 0,
        };
        _machineGrid = new MachineGrid();
        _enemyManager = new EnemyManager();
        _camera = new Camera(_renderer.Width, _renderer.Height);
        
        // Create remote player slots from lobby
        if (config.FinalLobby != null)
        {
            foreach (var lp in config.FinalLobby.Players.Skip(1))
            {
                _remotePlayers.Add(new Player(0, 0)
                {
                    PlayerIndex = lp.Index,
                    Nickname = lp.Nickname,
                    PlayerColor = lp.Color,
                });
            }
        }
    }
    
    /// <summary>
    /// Sets up a multiplayer client game with the given client and config.
    /// </summary>
    public void SetupMultiplayerClient(NetClient client, MultiplayerConfig config)
    {
        _client = client;
        _isMultiplayer = true;
        _isHost = false;
        
        _player = new Player(0, 0)
        {
            Nickname = config.Nickname,
            PlayerColor = config.Color,
            PlayerIndex = client.PlayerIndex,
        };
        
        // Client generates the same world as the host using the server's seed
        _world = new World(client.WorldSeed);
        _machineGrid = new MachineGrid();
        _enemyManager = new EnemyManager();
        _camera = new Camera(_renderer.Width, _renderer.Height);
        
        // Pre-generate chunks around spawn
        _ = _world.GetVisibleChunks(0, 0).ToList();
    }
    
    /// <summary>
    /// If true, the game should return to the main menu instead of exiting.
    /// </summary>
    public bool ReturnToMainMenu => _returnToMainMenu;

    public void Run()
    {
        SetupConsole();
        
        // Pre-generate chunks around spawn
        _ = _world.GetVisibleChunks(_player.X, _player.Y).ToList();
        
        if (_isMultiplayer && !_isHost && _client != null)
        {
            RunClient();
            return;
        }
        
        while (_running)
        {
            var command = _input.Poll();
            
            if (command != InputCommand.None || _state == GameState.MachineInteract)
            {
                HandleInput(command);
            }
            
            if (_moveCooldown > 0) _moveCooldown--;
            
            Update();
            
            // Host: process remote player inputs and broadcast state
            if (_isMultiplayer && _isHost && _server != null)
            {
                ProcessPendingJoins();
                ProcessRemotePlayerInputs();
                BroadcastState();
            }
            
            Render();
            
            _tickCount++;
            
            // Singleplayer auto-save (multiplayer host saves less frequently)
            if (!_isMultiplayer && _tickCount % 400 == 0 && _state == GameState.Playing)
            {
                TrySaveGame();
            }
            
            Thread.Sleep(_tickRate);
        }
        
        if (!_isMultiplayer) TrySaveGame();
        ShutdownConsole();
    }
    
    private void HandleInput(InputCommand command)
    {
        switch (_state)
        {
            case GameState.Playing: HandlePlayingInput(command); break;
            case GameState.Help: HandleHelpInput(command); break;
            case GameState.Inventory: HandleInventoryInput(command); break;
            case GameState.Crafting: HandleCraftingInput(command); break;
            case GameState.Building: HandleBuildingInput(command); break;
            case GameState.Scanning: HandleScanningInput(command); break;
            case GameState.MachineInteract: HandleMachineInteractInput(command); break;
            case GameState.Victory: HandleVictoryInput(command); break;
            case GameState.PauseMenu: HandlePauseMenuInput(command); break;
        }
    }
    
    private void HandlePlayingInput(InputCommand command)
    {
        switch (command)
        {
            case InputCommand.MoveUp: TryMovePlayer(0, -1); break;
            case InputCommand.MoveDown: TryMovePlayer(0, 1); break;
            case InputCommand.MoveLeft: TryMovePlayer(-1, 0); break;
            case InputCommand.MoveRight: TryMovePlayer(1, 0); break;
            case InputCommand.Mine: DoMiningTick(); break;
            case InputCommand.Help: _state = GameState.Help; break;
            case InputCommand.Inventory: _state = GameState.Inventory; break;
            case InputCommand.Craft:
                _state = GameState.Crafting;
                _craftSelection = 0;
                break;
            case InputCommand.Build:
                _state = GameState.Building;
                _buildSelection = 0;
                _buildDirection = Direction.Right;
                // Start cursor at player's facing tile
                (_buildCursorX, _buildCursorY) = _player.GetFacingTile();
                break;
            case InputCommand.Scan:
                _scanner.StartScan(_player.X, _player.Y);
                _state = GameState.Scanning;
                break;
            case InputCommand.Interact:
                TryInteractMachine();
                break;
            case InputCommand.GodMode:
                _player.GodMode = !_player.GodMode;
                break;
            case InputCommand.Quit:
                _state = GameState.PauseMenu; _pauseSelection = 0; break;
        }
    }
    
    /// <summary>
    /// Opens the machine interaction menu if the player is facing a machine.
    /// </summary>
    private void TryInteractMachine()
    {
        var (fx, fy) = _player.GetFacingTile();
        var machine = _machineGrid.GetMachineAt(fx, fy);
        if (machine == null) return;
        
        _interactMachine = machine;
        _state = GameState.MachineInteract;
    }
    
    private void HandleMachineInteractInput(InputCommand command)
    {
        if (_interactMachine == null) { _state = GameState.Playing; return; }
        
        // Check for digit key 1-9 to deposit player item into machine
        char ch = _input.LastKeyChar;
        if (ch >= '1' && ch <= '9')
        {
            int slotNum = ch - '1'; // 0-based
            var playerSlots = _player.Inventory.GetFilledSlots().ToList();
            if (slotNum < playerSlots.Count)
            {
                var (_, stack) = playerSlots[slotNum];
                int toDeposit = Math.Min(stack.Count, 1);
                int overflow = _interactMachine.InternalInventory.AddItem(stack.Id, toDeposit);
                if (overflow < toDeposit)
                {
                    int deposited = toDeposit - overflow;
                    _player.Inventory.RemoveItem(stack.Id, deposited);
                }
            }
            return;
        }
        
        switch (command)
        {
            case InputCommand.Interact:
            case InputCommand.Quit:
            case InputCommand.Help:
                _state = GameState.Playing;
                _interactMachine = null;
                break;
            
            case InputCommand.Mine:
                // If facing a tripped generator, reset fuse
                if (MachineTypeInfo.IsGenerator(_interactMachine.Type) && _interactMachine.FuseTripped)
                {
                    _machineGrid.ResetFuse(_interactMachine);
                    _fuseResetAnimTimer = 15;
                }
                break;
            
            case InputCommand.TakeItem:
                // Take first available item from machine inventory
                var slots = _interactMachine.InternalInventory.GetFilledSlots().ToList();
                if (slots.Count > 0)
                {
                    var (_, stack) = slots[0];
                    int canTake = Math.Min(stack.Count, 1);
                    if (canTake > 0 && _player.Inventory.AddItem(stack.Id, canTake) == 0)
                    {
                        _interactMachine.InternalInventory.RemoveItem(stack.Id, canTake);
                    }
                }
                break;
        }
    }
    
    private void HandleScanningInput(InputCommand command)
    {
        if (command == InputCommand.Scan || command == InputCommand.Quit || command == InputCommand.Interact)
        {
            if (_scanner.HasResults)
                _scanner.Close();
            _state = GameState.Playing;
        }
    }
    
    private void HandleHelpInput(InputCommand command)
    {
        if (command == InputCommand.Help || command == InputCommand.Quit)
            _state = GameState.Playing;
    }
    
    private void HandleInventoryInput(InputCommand command)
    {
        if (command == InputCommand.Inventory || command == InputCommand.Quit)
            _state = GameState.Playing;
    }
    
    private void HandleCraftingInput(InputCommand command)
    {
        var recipes = GetAvailableCraftRecipes();
        if (recipes.Count == 0) { _state = GameState.Playing; return; }
        
        switch (command)
        {
            case InputCommand.MoveUp: _craftSelection = Math.Max(0, _craftSelection - 1); break;
            case InputCommand.MoveDown: _craftSelection = Math.Min(recipes.Count - 1, _craftSelection + 1); break;
            case InputCommand.Interact:
            case InputCommand.Mine:
                TryCraft(recipes[_craftSelection]);
                break;
            case InputCommand.Craft:
            case InputCommand.Quit:
                _state = GameState.Playing;
                break;
        }
    }
    
    private void HandleBuildingInput(InputCommand command)
    {
        switch (command)
        {
            // Move cursor freely
            case InputCommand.MoveUp:    _buildCursorY--; break;
            case InputCommand.MoveDown:  _buildCursorY++; break;
            case InputCommand.MoveLeft:  _buildCursorX--; break;
            case InputCommand.MoveRight: _buildCursorX++; break;
            // Cycle machine type
            case InputCommand.NextType:
                _buildSelection = (_buildSelection + 1) % BuildableTypes.Length;
                break;
            case InputCommand.PrevType:
                _buildSelection = (_buildSelection - 1 + BuildableTypes.Length) % BuildableTypes.Length;
                break;
            // Rotate
            case InputCommand.Rotate: _buildDirection = _buildDirection.RotateCW(); break;
            // Place machine at cursor
            case InputCommand.Interact:
            case InputCommand.Mine:
                TryBuildAt(BuildableTypes[_buildSelection], _buildCursorX, _buildCursorY);
                break;
            // Exit build mode
            case InputCommand.Build:
            case InputCommand.Quit:
                _state = GameState.Playing;
                break;
        }
    }
    
    private void HandleVictoryInput(InputCommand command)
    {
        if (command != InputCommand.None)
            _running = false;
    }
    

    private void HandlePauseMenuInput(InputCommand command)
    {
        const int OptionCount = 5;
        switch (command)
        {
            case InputCommand.MoveUp:
                _pauseSelection = (_pauseSelection - 1 + OptionCount) % OptionCount;
                break;
            case InputCommand.MoveDown:
                _pauseSelection = (_pauseSelection + 1) % OptionCount;
                break;
            case InputCommand.Enter:
            case InputCommand.Interact:
                ExecutePauseAction(_pauseSelection);
                break;
            case InputCommand.Quit:
                _state = GameState.Playing;
                break;
        }
    }

    private void ExecutePauseAction(int selection)
    {
        switch (selection)
        {
            case 0: _state = GameState.Playing; break;
            case 1: _enemiesEnabled = !_enemiesEnabled; break;
            case 2: TrySaveGame(); break;
            case 3: TrySaveGame(); _returnToMainMenu = true; _running = false; break;
            case 4: _returnToMainMenu = true; _running = false; break;
        }
    }
    /// <summary>
    /// Gets all available craft recipes: Manual + CraftingTable (if player has table).
    /// </summary>
    private List<Recipe> GetAvailableCraftRecipes()
    {
        var recipes = RecipeRegistry.GetByCategory(RecipeCategory.Manual);
        
        // Add CraftingTable recipes if player has a Crafting Table
        if (_player.Inventory.HasItem(ItemId.CraftingTable, 1))
        {
            recipes.AddRange(RecipeRegistry.GetByCategory(RecipeCategory.CraftingTable));
        }
        
        return recipes;
    }
    
    private void TryCraft(Recipe recipe)
    {
        if (!_player.Inventory.HasIngredients(recipe.Inputs)) return;
        int overflow = _player.Inventory.AddItem(recipe.Output, recipe.OutputCount);
        if (overflow > 0) return;
        _player.Inventory.RemoveIngredients(recipe.Inputs);
        
        CheckWinCondition();
    }
    
    private void TryBuild(MachineType type)
    {
        // Legacy: build at player's facing tile
        var (fx, fy) = _player.GetFacingTile();
        TryBuildAt(type, fx, fy);
    }
    
    /// <summary>
    /// Attempts to build a machine at the specified world coordinates.
    /// </summary>
    private void TryBuildAt(MachineType type, int bx, int by)
    {
        var cost = MachineGrid.GetBuildCost(type);
        if (!_player.GodMode && !cost.All(c => _player.Inventory.HasItem(c.Id, c.Count))) return;
        
        if (_machineGrid.GetMachineAt(bx, by) != null) return;
        
        var tile = _world.GetTile(bx, by);
        if (tile == TileType.Water) return;
        
            MachineBase machine = type switch
            {
                MachineType.Miner => new Miner(bx, by, _buildDirection, tile),
                MachineType.ConveyorBelt => new ConveyorBelt(bx, by, _buildDirection),
                MachineType.Furnace => new Furnace(bx, by, _buildDirection),
                MachineType.Assembler => new Assembler(bx, by, _buildDirection),
                MachineType.StorageChest => new StorageChest(bx, by, _buildDirection),
                MachineType.PowerGenerator => new PowerGenerator(bx, by, _buildDirection),
                MachineType.Lab => new Lab(bx, by, _buildDirection),
                MachineType.Refinery => new Refinery(bx, by, _buildDirection),
                MachineType.FirewallTurret => new FirewallTurret(bx, by, _buildDirection),
                MachineType.Wire => new Wire(bx, by, _buildDirection),
                MachineType.BiomassBurner => new BiomassBurner(bx, by, _buildDirection),
                MachineType.CoalGenerator => new CoalGenerator(bx, by, _buildDirection, (wx, wy) => _world.GetTile(wx, wy)),
                MachineType.NuclearReactor => new NuclearReactor(bx, by, _buildDirection),
                _ => throw new ArgumentOutOfRangeException()
            };
        
        if (!_machineGrid.PlaceMachine(machine, (wx, wy) => _world.GetTile(wx, wy))) return;
        
        if (!_player.GodMode)
        {
            foreach (var (id, count) in cost)
                _player.Inventory.RemoveItem(id, count);
        }
        
        // Record machine placement for multiplayer broadcast
        if (_isMultiplayer && _isHost)
        {
            _tickMachineChanges.Add(new MachineAction
            {
                Action = MachineAction.Place,
                X = bx, Y = by,
                Type = (int)type,
                Direction = (int)_buildDirection,
                TileType = (int)tile,
            });
        }
    }
    
    private void TryMovePlayer(int dx, int dy)
    {
        if (_moveCooldown > 0) return;
        
        var (turned, moved) = _player.TryMove(dx, dy, (wx, wy) =>
        {
            if (_machineGrid.GetMachineAt(wx, wy) != null) return false;
            return TileTypeInfo.IsWalkable(_world.GetTile(wx, wy));
        });
        
        if (moved) _moveCooldown = MoveDelay;
        if (turned) _moveCooldown = 0; // Turning is instant, no cooldown
    }
    
    private void DoMiningTick()
    {
        DoMiningTickFor(_player);
    }
    
    /// <summary>
    /// Performs a mining tick for the given player (host or remote).
    /// Priority: (1) shoot enemies, (2) disassemble machines, (3) mine terrain.
    /// </summary>
    private void DoMiningTickFor(Player player)
    {
        player.MiningAnimFrame++;
        
        // Check if facing an enemy — laser damages enemies
        var (fx, fy) = player.GetFacingTile();
        var godDmg = player.GodMode ? 9999 : 20;
        var hitEnemy = _enemyManager.LaserHitEnemy(fx, fy, godDmg);
        
        if (hitEnemy != null)
        {
            player.IsMining = true;
            player.MiningProgress = Math.Min(100, player.MiningProgress + (player.GodMode ? 100 : 20));
            if (player.MiningProgress >= 100) player.MiningProgress = 0;
            
            if (hitEnemy.IsDead)
            {
                player.Inventory.AddItem(hitEnemy.Loot.Id, hitEnemy.Loot.Count);
                player.LastMinedItem = hitEnemy.Loot.Id;
                player.MiningSplashTimer = 8;
            }
            return;
        }
        
        // Check if facing a machine — disassemble it, returning build cost items
        var targetMachine = _machineGrid.GetMachineAt(fx, fy);
        if (targetMachine != null)
        {
            var cost = MachineGrid.GetBuildCost(targetMachine.Type);
            _machineGrid.RemoveMachine(targetMachine.X, targetMachine.Y);
            
            // Return build cost items to player inventory
            foreach (var (id, count) in cost)
            {
                player.Inventory.AddItem(id, count);
            }
            
            // Transfer any items in machine's internal inventory back to player
            foreach (var (_, stack) in targetMachine.InternalInventory.GetFilledSlots())
            {
                player.Inventory.AddItem(stack.Id, stack.Count);
            }
            
            player.IsMining = true;
            player.MiningProgress = 0;
            player.LastMinedItem = ItemId.IronPlate; // Generic splash
            player.MiningSplashTimer = 8;
            player.MiningTargetX = fx;
            player.MiningTargetY = fy;
            return;
        }
        
        var result = player.IsMining
            ? player.TickMining(
                (wx, wy) => _world.GetTile(wx, wy),
                (wx, wy, tile) => SetTileAndRecord(wx, wy, tile))
            : StartMiningFor(player);
        
        if (result != null)
        {
            player.LastMinedItem = result;
            player.MiningSplashTimer = 8;
        }
    }
    
    private ItemId? StartMining()
    {
        return StartMiningFor(_player);
    }
    
    private ItemId? StartMiningFor(Player player)
    {
        player.MiningProgress = 0;
        player.IsMining = true;
        return player.TickMining(
            (wx, wy) => _world.GetTile(wx, wy),
            (wx, wy, tile) => SetTileAndRecord(wx, wy, tile));
    }
    
    /// <summary>
    /// Sets a tile and records the change for both multiplayer broadcasting and save persistence.
    /// </summary>
    private void SetTileAndRecord(int x, int y, TileType tile)
    {
        _world.SetTile(x, y, tile);
        _tileChanges[(x, y)] = tile;
        if (_isMultiplayer && _isHost)
        {
            _tickTileChanges.Add(new TileChange { X = x, Y = y, TileType = (int)tile });
        }
    }
    
    private void CheckWinCondition()
    {
        if (_hasWon) return;
        
        // Check all players (anyone can trigger victory)
        bool won = _player.Inventory.HasItem(ItemId.Computer9000, 1);
        if (!won && _isMultiplayer)
        {
            foreach (var rp in _remotePlayers)
            {
                if (rp.Inventory.HasItem(ItemId.Computer9000, 1))
                { won = true; break; }
            }
        }
        
        if (won)
        {
            _hasWon = true;
            _state = GameState.Victory;
            
            // Broadcast victory to all clients
            if (_isMultiplayer && _isHost && _server != null)
            {
                _server.BroadcastVictory(_player.PlayerIndex);
            }
        }
    }
    
    private void Update()
    {
        // Clear per-tick change tracking
        _tickTileChanges.Clear();
        _tickMachineChanges.Clear();
        
        // In build mode, center camera on cursor
        if (_state == GameState.Building)
            _camera.CenterOn(_buildCursorX, _buildCursorY, _renderer.HudHeight);
        else
            _camera.CenterOn(_player.X, _player.Y, _renderer.HudHeight);
        
        // Tick scanner
        if (_scanner.IsScanning)
            _scanner.TickScan(_world, 8);
        
        // Tick machines
        _machineGrid.TickAll();
        
        // Tick enemies
        int machineCount = _machineGrid.MachineCount;
        var killed = _enemyManager.Tick(_player, _machineGrid, _world);
        
        // Drop loot from killed enemies
        foreach (var enemy in killed)
        {
            _player.Inventory.AddItem(enemy.Loot.Id, enemy.Loot.Count);
        }
        
        // Remove destroyed machines
        foreach (var machine in _machineGrid.GetAllMachines().Where(m => m.IsDestroyed).ToList())
        {
            _machineGrid.RemoveMachine(machine.X, machine.Y);
        }
        
        // Tick machine visuals
        foreach (var machine in _machineGrid.GetAllMachines())
            machine.TickVisuals();
        
        // Tick turret targeting and firing
        foreach (var machine in _machineGrid.GetAllMachines())
        {
            if (machine is FirewallTurret turret && turret.IsPowered)
            {
                turret.AcquireTarget(_enemyManager.Enemies);
                if (turret.Tick(_machineGrid.GetMachineAt))
                {
                    var shot = turret.Fire();
                    if (shot.HasValue)
                    {
                        var (dx, dy, dmg) = shot.Value;
                        _enemyManager.FireProjectile(turret.X, turret.Y, dx, dy, dmg);
                    }
                }
            }
        }
        
        // Spawn enemies (scaled by factory size)
        if (_enemiesEnabled)
            _enemyManager.TickSpawning(_player.X, _player.Y, machineCount, _world);
        
        // Player health regen (slow)
        if (_player.Health < _player.MaxHealth && _tickCount % 100 == 0)
            _player.Health = Math.Min(_player.MaxHealth, _player.Health + 1);
        
        // Player damage flash
        if (_player.DamageFlash > 0) _player.DamageFlash--;
        
        // Check if any assembler produced a Computer 9000
        if (!_hasWon) CheckWinCondition();
        
        // Remote players: regen, damage flash, visual ticks
        foreach (var rp in _remotePlayers)
        {
            if (rp.Health < rp.MaxHealth && _tickCount % 100 == 0)
                rp.Health = Math.Min(rp.MaxHealth, rp.Health + 1);
            if (rp.DamageFlash > 0) rp.DamageFlash--;
        }
        
        // Unload distant chunks
        if (_tickCount % 200 == 0)
            _world.UnloadDistantChunks(_player.X, _player.Y);
        
        if (_renderer.CheckResize())
            _camera = new Camera(_renderer.Width, _renderer.Height);
    }
    
    private void Render()
    {
        _renderer.Clear();
        _renderer.DrawWorld(_world, _camera);
        _renderer.DrawMachines(_machineGrid, _camera);
        _renderer.DrawEnemies(_enemyManager, _camera);
        _renderer.DrawProjectiles(_enemyManager, _camera);
        _renderer.DrawPlayer(_player.X, _player.Y, _player.FacingX, _player.FacingY, _camera);
        
        // Draw remote players (host mode)
        foreach (var rp in _remotePlayers)
        {
            _renderer.DrawPlayerColored(rp.X, rp.Y, rp.FacingX, rp.FacingY,
                rp.PlayerColor, rp.Nickname, _camera, false);
            
            if (rp.IsMining)
            {
                _renderer.DrawLaserBeam(
                    rp.X, rp.Y, rp.FacingX, rp.FacingY,
                    rp.MiningProgress, rp.MiningAnimFrame, _camera);
            }
        }
        
        // Laser mining animation
        if (_player.IsMining)
        {
            _renderer.DrawLaserBeam(
                _player.X, _player.Y, _player.FacingX, _player.FacingY,
                _player.MiningProgress, _player.MiningAnimFrame, _camera);
        }
        
        // Mining splash effect
        if (_player.MiningSplashTimer > 0)
        {
            _renderer.DrawMiningSplash(
                _player.MiningTargetX, _player.MiningTargetY,
                _player.MiningSplashTimer, _player.LastMinedItem, _camera);
            _player.MiningSplashTimer--;
        }
        
        // HUD
        var biome = _world.GetBiome(_player.X, _player.Y);
        var (biomeName, biomeColor) = BiomeInfo.GetInfo(biome);
        var (fx, fy) = _player.GetFacingTile();
        var facingTile = _world.GetTile(fx, fy);
        _renderer.DrawHUD(biomeName, biomeColor, _player.X, _player.Y, _tickRate, 
            _state == GameState.Playing, facingTile,
            _player.Health, _player.MaxHealth, _player.DamageFlash > 0,
            _enemyManager.EnemyCount, _machineGrid.MachineCount,
            _player.GodMode);
        _renderer.DrawInventoryBar(_player.Inventory, _renderer.HudHeight);
        
        // Overlays
        switch (_state)
        {
            case GameState.Help: DrawHelpOverlay(); break;
            case GameState.Inventory: _renderer.DrawInventoryScreen(_player.Inventory); break;
            case GameState.Crafting: _renderer.DrawCraftingMenu(_player.Inventory, _craftSelection); break;
            case GameState.Building: DrawBuildCursor(); break;
            case GameState.Scanning: _renderer.DrawScannerOverlay(_scanner); break;
            case GameState.MachineInteract: DrawMachineInteract(); break;
            case GameState.Victory: DrawVictoryScreen(); break;
            case GameState.PauseMenu: DrawPauseMenu(); break;
        }
        
        _renderer.Present();
    }
    
    private void DrawPauseMenu()
    {
        string[] options = {
            "  Resume Game",
            $"  Enemies: {(_enemiesEnabled ? "ON" : "OFF")}",
            "  Save Game",
            "  Save & Quit to Menu",
            "  Quit to Menu (no save)"
        };
        
        var lines = new List<string>
        {
            "",
            "  ╔══════════════════════════════════════╗",
            "  ║         PAUSE MENU                  ║",
            "  ╠══════════════════════════════════════╣",
        };
        
        for (int i = 0; i < options.Length; i++)
        {
            string marker = i == _pauseSelection ? " ►" : "  ";
            ConsoleColor color = i == _pauseSelection ? ConsoleColor.Yellow : ConsoleColor.White;
            lines.Add($"  ║{marker}{options[i].PadRight(36)}║");
        }
        
        lines.Add("  ╠══════════════════════════════════════╣");
        lines.Add("  ║  ↑↓ Navigate  Enter Select  Esc Back ║");
        lines.Add("  ╚══════════════════════════════════════╝");
        
        _renderer.DrawOverlay(lines.ToArray(), ConsoleColor.Cyan, ConsoleColor.White);
    }

    private void DrawVictoryScreen()
    {
        _renderer.DrawOverlay(new[]
        {
            "",
            "",
            "  ╔══════════════════════════════════════════════════════╗",
            "  ║                                                      ║",
            "  ║     ★ ★ ★  C O N G R A T U L A T I O N S  ★ ★ ★    ║",
            "  ║                                                      ║",
            "  ║       You built THE COMPUTER 9000™!                  ║",
            "  ║                                                      ║",
            "  ║   After strip-mining an entire planet,               ║",
            "  ║   building thousands of machines,                    ║",
            "  ║   and polluting the ecosystem beyond repair...       ║",
            "  ║                                                      ║",
            "  ║   THE COMPUTER 9000™ boots up...                     ║",
            "  ║                                                      ║",
            "  ║   ╔═══════════════════════════════════════╗           ║",
            "  ║   ║                                       ║           ║",
            "  ║   ║   > Hello, World!                     ║           ║",
            "  ║   ║                                       ║           ║",
            "  ║   ║   > That's it. That's all it does.    ║           ║",
            "  ║   ║                                       ║           ║",
            "  ║   ╚═══════════════════════════════════════╝           ║",
            "  ║                                                      ║",
            "  ║   ...the same thing Program.cs already did.          ║",
            "  ║                                                      ║",
            "  ║          Press any key to quit.                       ║",
            "  ║                                                      ║",
            "  ╚══════════════════════════════════════════════════════╝",
            "",
        }, ConsoleColor.Yellow, ConsoleColor.White);
    }
    
    private void DrawMachineInteract()
    {
        if (_interactMachine == null) return;
        
        var m = _interactMachine;
        var (supply, demand) = _machineGrid.GetPowerInfoFor(m.X, m.Y);
        
        // Fuse reset animation
        if (_fuseResetAnimTimer > 0)
        {
            _fuseResetAnimTimer--;
            var animLines = _fuseResetAnimTimer switch
            {
                > 12 => new[] { "  *click*", "  ...", "" },
                > 8 => new[] { "  *click*...", "  buzz...", "" },
                > 4 => new[] { "  *click*... buzz...", "  hummm...", "" },
                _ => new[] { "  *click*... buzz... hummm...", "  Power restored!", "" },
            };
            _renderer.DrawOverlay(animLines, ConsoleColor.Green, ConsoleColor.White);
            return;
        }
        
        var typeName = MachineTypeInfo.GetName(m.Type);
        var dirName = m.Direction.ToString();
        
        // Power bar
        int barW = 20;
        int supplyFill = Math.Min(barW, supply > 0 ? barW * supply / Math.Max(supply, demand) : 0);
        string powerBar = new string('█', supplyFill) + new string('░', barW - supplyFill);
        ConsoleColor powerColor = supply >= demand ? ConsoleColor.Green : ConsoleColor.Red;
        
        // Progress bar
        int progFill = (int)(barW * m.Progress);
        string progBar = m.IsProcessing 
            ? new string('█', progFill) + new string('░', barW - progFill)
            : new string('·', barW);
        
        var lines = new List<string>
        {
            $"  {typeName} ({m.X},{m.Y}) facing {dirName}",
            "",
            $"  Status: {(m.FuseTripped ? "⚠ FUSE TRIPPED" : m.IsProcessing ? "⟳ Processing" : "○ Idle")}",
            $"  [{progBar}]",
            "",
            $"  ⚡ Power: {supply}MW / {demand}MW",
            $"  [{powerBar}]",
        };
        
        // Fuse tripped warning
        if (m.FuseTripped && MachineTypeInfo.IsGenerator(m.Type))
        {
            lines.Add("");
            lines.Add("  ⚠ FUSE TRIPPED — Press Space to reset");
        }
        
        // Machine internal inventory
        var slots = m.InternalInventory.GetFilledSlots().ToList();
        lines.Add("");
        lines.Add("  Machine Inventory:");
        if (slots.Count > 0)
        {
            for (int i = 0; i < Math.Min(slots.Count, 4); i++)
            {
                var (_, stack) = slots[i];
                char sym = ItemRegistry.GetSymbol(stack.Id);
                lines.Add($"    {sym} {ItemRegistry.GetName(stack.Id)} x{stack.Count}");
            }
        }
        else
        {
            lines.Add("    (empty)");
        }
        
        // Player inventory (for deposit)
        var playerSlots = _player.Inventory.GetFilledSlots().ToList();
        lines.Add("");
        lines.Add("  Your Inventory (press 1-9 to deposit):");
        if (playerSlots.Count > 0)
        {
            for (int i = 0; i < Math.Min(playerSlots.Count, 9); i++)
            {
                var (_, stack) = playerSlots[i];
                char sym = ItemRegistry.GetSymbol(stack.Id);
                lines.Add($"    [{i + 1}] {sym} {ItemRegistry.GetName(stack.Id)} x{stack.Count}");
            }
        }
        else
        {
            lines.Add("    (empty)");
        }
        
        lines.Add("");
        lines.Add("  T = Take first item  E/Esc = Close");
        
        _renderer.DrawOverlay(lines.ToArray(), 
            m.FuseTripped ? ConsoleColor.Red : ConsoleColor.Cyan, 
            ConsoleColor.White);
    }
    
    /// <summary>
    /// Draws the cursor build mode: ghost preview of the selected machine at cursor,
    /// plus a compact info bar at the top of the screen.
    /// </summary>
    private void DrawBuildCursor()
    {
        var type = BuildableTypes[_buildSelection];
        var typeName = MachineTypeInfo.GetName(type);
        var typeSymbol = MachineTypeInfo.GetSymbol(type);
        int w = MachineTypeInfo.GetWidth(type);
        int h = MachineTypeInfo.GetHeight(type);
        
        // Check if placement is valid
        bool blocked = _machineGrid.GetMachineAt(_buildCursorX, _buildCursorY) != null
            || _world.GetTile(_buildCursorX, _buildCursorY) == TileType.Water;
        var cost = MachineGrid.GetBuildCost(type);
        bool canAfford = _player.GodMode || cost.All(c => _player.Inventory.HasItem(c.Id, c.Count));
        bool valid = !blocked && canAfford;
        
        // Draw ghost preview (machine outline at cursor)
        _renderer.DrawBuildGhost(_buildCursorX, _buildCursorY, w, h, typeSymbol, valid, _camera);
        
        // Draw direction indicator arrow at cursor
        var arrowPos = _camera.WorldToScreen(_buildCursorX, _buildCursorY);
        if (arrowPos.HasValue)
        {
            // Draw a small direction arrow next to the cursor
            char dirChar = _buildDirection switch
            {
                Direction.Up => '↑',
                Direction.Down => '↓',
                Direction.Left => '←',
                Direction.Right => '→',
                _ => '·'
            };
            int arrowX = arrowPos.Value.ScreenX + w;
            _renderer.SetCell(arrowX, arrowPos.Value.ScreenY, dirChar, ConsoleColor.Cyan);
        }
        
        // Compact info bar at top of screen
        string costStr = string.Join("+", cost.Select(c => $"{ItemRegistry.GetName(c.Id)}x{c.Count}"));
        string affordStr = canAfford ? "✓" : "✗";
        ConsoleColor affordColor = canAfford ? ConsoleColor.Green : ConsoleColor.Red;
        string validStr = valid ? "OK" : (blocked ? "BLOCKED" : "NO RESOURCES");
        ConsoleColor validColor = valid ? ConsoleColor.Green : ConsoleColor.Red;
        
        int viewH = _renderer.Height - _renderer.HudHeight;
        
        // Top bar background
        _renderer.DrawString(0, 0, $" BUILD: ", ConsoleColor.DarkCyan);
        _renderer.DrawString(8, 0, $"{typeSymbol} {typeName}", ConsoleColor.Cyan);
        _renderer.DrawString(8 + typeName.Length + 3, 0, $" ({w}×{h})", ConsoleColor.DarkGray);
        
        int col = 8 + typeName.Length + 8;
        _renderer.DrawString(col, 0, $"│ Dir: {_buildDirection}", ConsoleColor.DarkGray);
        col += 10;
        _renderer.DrawString(col, 0, $"│ [{costStr}] {affordStr}", affordColor);
        col += costStr.Length + 6;
        _renderer.DrawString(col, 0, $"│ {validStr}", validColor);
        
        // Controls hint at top-right
        string controls = "[/] type  R:rotate  Space:place  B/Esc:exit";
        int ctrlX = _renderer.Width - controls.Length - 2;
        if (ctrlX > col + 5)
            _renderer.DrawString(ctrlX, 0, controls, ConsoleColor.DarkGray);
    }
    
    private void DrawHelpOverlay()
    {
        _renderer.DrawOverlay(new[]
        {
            "╔════════════════════════════════════════════════╗",
            "║            ASCIIFACTORY - CONTROLS             ║",
            "╠════════════════════════════════════════════════╣",
            "║                                                ║",
            "║  WASD / Arrows    Move player                  ║",
            "║  Space (hold)     Mine resource                ║",
            "║  B                Build menu                   ║",
            "║  I                Inventory                    ║",
            "║  C                Craft menu                   ║",
            "║  R                Rotate building              ║",
            "║  E                Interact with machine        ║",
            "║  F                Terrain Scanner              ║",
            "║  H                Toggle this help             ║",
            "║  Q / Esc          Pause menu                  ║",
            "║                                                ║",
            "╠════════════════════════════════════════════════╣",
            "║   MACHINES:                                    ║",
            "║                                                ║",
            "║   ┌─┐  Miner — mines deposit below   ⚡2MW    ║",
            "║   │M│► output ►                       ~       ║",
            "║   └─┘                                          ║",
            "║                                                ║",
            "║   › = Conveyor  F = Furnace  A = Assembler    ║",
            "║   □ = Chest     L = Lab       R = Refinery    ║",
            "║   T = Firewall Turret               ⚡3MW     ║",
            "║                                                ║",
            "║   POWER:                                       ║",
            "║   + = Wire (connects grid)                     ║",
            "║   b = Biomass Burner    ⚡30MW  burns Biomass  ║",
            "║   C = Coal Generator    ⚡75MW  needs Water    ║",
            "║   ☢ = Nuclear Reactor  ⚡600MW  burns Uranium ║",
            "║                                                ║",
            "╠════════════════════════════════════════════════╣",
            "║   RESOURCES:                                   ║",
            "║   ▒ = Grass/Dirt → Biomass                     ║",
            "║   ◆ = Iron   ◈ = Copper  ✦ = Coal             ║",
            "║   ◉ = Stone  ◇ = Quartz  ☢ = Uranium          ║",
            "║   ¤ = Oil    ≋ = Water                         ║",
            "║                                                ║",
            "║   GOAL: Build THE COMPUTER 9000™!              ║",
            "║                                                ║",
            "╠════════════════════════════════════════════════╣",
            "║           Press H to close this menu           ║",
            "╚════════════════════════════════════════════════╝",
        }, ConsoleColor.Cyan, ConsoleColor.White);
    }
    
    // ========== SAVE / LOAD ==========
    
    private bool TrySaveGame()
    {
        // Block saving in god mode
        if (_player.GodMode) return false;
        
        try
        {
            var saveData = new SaveData
            {
                WorldSeed = _world.Seed,
                PlayerX = _player.X,
                PlayerY = _player.Y,
                TickCount = _tickCount,
                HasWon = _hasWon,
                TickRate = _tickRate,
                EnemiesEnabled = _enemiesEnabled,
                InventoryItems = _player.Inventory.GetFilledSlots()
                    .Select(s => (int)s.Stack.Id + ":" + s.Stack.Count)
                    .ToList(),
                Machines = _machineGrid.GetAllMachines()
                    .Select(m => new MachineSave
                    {
                        Type = (int)m.Type,
                        X = m.X,
                        Y = m.Y,
                        Direction = (int)m.Direction,
                        Items = m.InternalInventory.GetFilledSlots()
                            .Select(s => (int)s.Stack.Id + ":" + s.Stack.Count)
                            .ToList(),
                    }).ToList(),
                TileChanges = _tileChanges
                    .Select(kvp => kvp.Key.x + ":" + kvp.Key.y + ":" + (int)kvp.Value)
                    .ToList(),
            };
            
            string json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SaveFile, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private bool TryLoadGame()
    {
        try
        {
            if (!File.Exists(SaveFile)) return false;
            
            string json = File.ReadAllText(SaveFile);
            var saveData = JsonSerializer.Deserialize<SaveData>(json);
            if (saveData == null) return false;
            
            _world = new World(saveData.WorldSeed);
            _player = new Player(saveData.PlayerX, saveData.PlayerY);
            _machineGrid = new MachineGrid();
            _enemyManager = new EnemyManager();
            _tickCount = saveData.TickCount;
            _hasWon = saveData.HasWon;
            
            // Restore settings from save
            if (saveData.TickRate > 0)
                _tickRate = saveData.TickRate;
            _enemiesEnabled = saveData.EnemiesEnabled;
            
            // Restore player inventory
            foreach (var itemStr in saveData.InventoryItems)
            {
                var parts = itemStr.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int id) && int.TryParse(parts[1], out int count))
                {
                    _player.Inventory.AddItem((ItemId)id, count);
                }
            }
            
            // Restore machines
            foreach (var ms in saveData.Machines)
            {
                var type = (MachineType)ms.Type;
                var dir = (Direction)ms.Direction;
                var tile = _world.GetTile(ms.X, ms.Y);
                
                MachineBase machine = type switch
                {
                    MachineType.Miner => new Miner(ms.X, ms.Y, dir, tile),
                    MachineType.ConveyorBelt => new ConveyorBelt(ms.X, ms.Y, dir),
                    MachineType.Furnace => new Furnace(ms.X, ms.Y, dir),
                    MachineType.Assembler => new Assembler(ms.X, ms.Y, dir),
                    MachineType.StorageChest => new StorageChest(ms.X, ms.Y, dir),
                    MachineType.PowerGenerator => new PowerGenerator(ms.X, ms.Y, dir),
                    MachineType.Lab => new Lab(ms.X, ms.Y, dir),
                    MachineType.Refinery => new Refinery(ms.X, ms.Y, dir),
                    MachineType.FirewallTurret => new FirewallTurret(ms.X, ms.Y, dir),
                    MachineType.Wire => new Wire(ms.X, ms.Y, dir),
                    MachineType.BiomassBurner => new BiomassBurner(ms.X, ms.Y, dir),
                    MachineType.CoalGenerator => new CoalGenerator(ms.X, ms.Y, dir, (wx, wy) => _world.GetTile(wx, wy)),
                    MachineType.NuclearReactor => new NuclearReactor(ms.X, ms.Y, dir),
                    _ => null!
                };
                
                if (machine != null)
                {
                    foreach (var itemStr in ms.Items)
                    {
                        var parts = itemStr.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int id) && int.TryParse(parts[1], out int count))
                        {
                            machine.InternalInventory.AddItem((ItemId)id, count);
                        }
                    }
                    
                    _machineGrid.PlaceMachine(machine, (wx, wy) => _world.GetTile(wx, wy));
                }
            }
            
            // Restore tile changes (replay onto regenerated world)
            foreach (var changeStr in saveData.TileChanges)
            {
                var parts = changeStr.Split(':');
                if (parts.Length == 3 
                    && int.TryParse(parts[0], out int tx) 
                    && int.TryParse(parts[1], out int ty)
                    && int.TryParse(parts[2], out int tileType))
                {
                    _world.SetTile(tx, ty, (TileType)tileType);
                    _tileChanges[(tx, ty)] = (TileType)tileType;
                }
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    // ========== MULTIPLAYER HOST METHODS ==========
    
    /// <summary>
    /// Processes players that joined during an active game (late joins).
    /// Called each tick by the host.
    /// </summary>
    private void ProcessPendingJoins()
    {
        if (_server == null) return;
        
        foreach (var join in _server.DrainPendingJoins())
        {
            _remotePlayers.Add(new Player(0, 0)
            {
                PlayerIndex = join.PlayerIndex,
                Nickname = join.Nickname,
                PlayerColor = join.Color,
            });
        }
    }
    
    /// <summary>
    /// Processes queued inputs from all remote players.
    /// Called each tick by the host.
    /// </summary>
    private void ProcessRemotePlayerInputs()
    {
        if (_server == null) return;
        
        foreach (var rp in _remotePlayers)
        {
            var inputs = _server.DrainInputs(rp.PlayerIndex);
            foreach (var inputCmd in inputs)
            {
                var cmd = (InputCommand)inputCmd;
                switch (cmd)
                {
                    case InputCommand.MoveUp: TryMoveRemotePlayer(rp, 0, -1); break;
                    case InputCommand.MoveDown: TryMoveRemotePlayer(rp, 0, 1); break;
                    case InputCommand.MoveLeft: TryMoveRemotePlayer(rp, -1, 0); break;
                    case InputCommand.MoveRight: TryMoveRemotePlayer(rp, 1, 0); break;
                    case InputCommand.Mine: DoMiningTickFor(rp); break;
                }
            }
        }
    }
    
    private void TryMoveRemotePlayer(Player player, int dx, int dy)
    {
        player.TryMove(dx, dy, (wx, wy) =>
        {
            if (_machineGrid.GetMachineAt(wx, wy) != null) return false;
            return TileTypeInfo.IsWalkable(_world.GetTile(wx, wy));
        });
    }
    
    /// <summary>
    /// Broadcasts the full game state to all connected clients.
    /// Called each tick by the host after Update().
    /// </summary>
    private void BroadcastState()
    {
        if (_server == null) return;
        
        // Build all-players list: host + remotes
        var allPlayers = new List<Player> { _player };
        allPlayers.AddRange(_remotePlayers);
        
        _server.BroadcastSnapshot(
            allPlayers,
            _enemyManager,
            _machineGrid,
            _world,
            _tickTileChanges,
            _tickMachineChanges,
            _tickCount);
    }
    
    private void SetupConsole()
    {
        Console.CursorVisible = false;
        Console.Clear();
    }
    
    /// <summary>
    /// Client game loop: sends inputs, receives snapshots, renders.
    /// </summary>
    private void RunClient()
    {
        while (_running)
        {
            var command = _input.Poll();
            
            if (command != InputCommand.None)
            {
                // Send input to server
                _client!.SendInput(command);
                
                // Handle local UI state
                if (command == InputCommand.Quit)
                    _running = false;
            }
            
            // Process latest snapshot from server
            var snapshot = _client!.LatestSnapshot;
            if (snapshot != null)
            {
                RenderFromSnapshot(snapshot);
            }
            
            // Check for victory
            if (_client.VictoryReceived)
            {
                _state = GameState.Victory;
            }
            
            _tickCount++;
            Thread.Sleep(_tickRate);
        }
        
        _client.Dispose();
        ShutdownConsole();
    }
    
    /// <summary>
    /// Renders the game from a server snapshot (client mode).
    /// </summary>
    private void RenderFromSnapshot(GameSnapshot snapshot)
    {
        _renderer.Clear();
        _renderer.DrawWorld(_world, _camera);
        
        // Update camera from snapshot (use local player position)
        var localPlayer = snapshot.Players.FirstOrDefault(p => p.Index == _player.PlayerIndex);
        if (localPlayer != null)
        {
            _player.X = localPlayer.X;
            _player.Y = localPlayer.Y;
            _player.FacingX = localPlayer.FacingX;
            _player.FacingY = localPlayer.FacingY;
            _player.Health = localPlayer.Health;
            _camera.CenterOn(_player.X, _player.Y, _renderer.HudHeight);
        }
        
        // Apply tile changes
        foreach (var tc in snapshot.TileChanges)
        {
            _world.SetTile(tc.X, tc.Y, (TileType)tc.TileType);
        }
        
        // Draw machines from snapshot
        foreach (var m in snapshot.Machines)
        {
            var screenPos = _camera.WorldToScreen(m.X, m.Y);
            if (screenPos.HasValue)
            {
                var (sx, sy) = screenPos.Value;
                char ch = MachineTypeInfo.GetSymbol((MachineType)m.Type);
                ConsoleColor color = MachineTypeInfo.GetColor((MachineType)m.Type);
                _renderer.SetCell(sx, sy, ch, color);
            }
        }
        
        // Draw enemies from snapshot
        foreach (var e in snapshot.Enemies)
        {
            var screenPos = _camera.WorldToScreen(e.X, e.Y);
            if (screenPos.HasValue)
            {
                var (sx, sy) = screenPos.Value;
                char ch = (EnemyType)e.Type switch
                {
                    EnemyType.Bug => 'b',
                    EnemyType.Glitch => 'g',
                    EnemyType.KernelPanic => 'K',
                    EnemyType.Segfault => 'S',
                    EnemyType.MemoryLeak => 'm',
                    EnemyType.NullPointer => 'N',
                    _ => '?'
                };
                ConsoleColor color = (EnemyType)e.Type switch
                {
                    EnemyType.Bug => ConsoleColor.Green,
                    EnemyType.Glitch => ConsoleColor.Magenta,
                    EnemyType.KernelPanic => ConsoleColor.DarkRed,
                    EnemyType.Segfault => ConsoleColor.White,
                    EnemyType.MemoryLeak => ConsoleColor.DarkCyan,
                    EnemyType.NullPointer => ConsoleColor.Blue,
                    _ => ConsoleColor.Red
                };
                _renderer.SetCell(sx, sy, ch, color);
            }
        }
        
        // Draw projectiles from snapshot
        foreach (var p in snapshot.Projectiles)
        {
            var screenPos = _camera.WorldToScreen(p.X, p.Y);
            if (screenPos.HasValue)
            {
                var (sx, sy) = screenPos.Value;
                _renderer.SetCell(sx, sy, '•', ConsoleColor.Yellow);
            }
        }
        
        // Draw local player
        _renderer.DrawPlayerColored(_player.X, _player.Y, _player.FacingX, _player.FacingY,
            _player.PlayerColor, _player.Nickname, _camera, true);
        
        // Draw remote players
        _renderer.DrawRemotePlayers(snapshot.Players, _player.PlayerIndex, _camera);
        
        // HUD
        var biome = _world.GetBiome(_player.X, _player.Y);
        var (biomeName, biomeColor) = BiomeInfo.GetInfo(biome);
        var (fx, fy) = _player.GetFacingTile();
        var facingTile = _world.GetTile(fx, fy);
        int enemyCount = snapshot.Enemies.Count;
        int machineCount = snapshot.Machines.Count;
        _renderer.DrawHUD(biomeName, biomeColor, _player.X, _player.Y, _tickRate, true, facingTile,
            _player.Health, _player.MaxHealth, false,
            enemyCount, machineCount, false);
        _renderer.DrawInventoryBar(_player.Inventory, _renderer.HudHeight);
        
        if (_state == GameState.Victory)
            DrawVictoryScreen();
        
        _renderer.Present();
    }
    
    private void ShutdownConsole()
    {
        _server?.Dispose();
        _client?.Dispose();
        
        Console.Clear();
        Console.CursorVisible = true;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  Thanks for playing ASCIIFACTORY!");
        if (_isMultiplayer)
            Console.WriteLine("  Multiplayer game ended.");
        else
            Console.WriteLine("  Game saved automatically.");
        Console.ResetColor();
    }
}

// Save data structures
public class SaveData
{
    public int WorldSeed { get; set; }
    public int PlayerX { get; set; }
    public int PlayerY { get; set; }
    public int TickCount { get; set; }
    public bool HasWon { get; set; }
    public int TickRate { get; set; }
    public bool EnemiesEnabled { get; set; } = true;
    public List<string> InventoryItems { get; set; } = new();
    public List<MachineSave> Machines { get; set; } = new();
    public List<string> TileChanges { get; set; } = new();
}

public class MachineSave
{
    public int Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Direction { get; set; }
    public List<string> Items { get; set; } = new();
}