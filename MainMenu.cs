using Asciifactory.Network;
using Asciifactory.WorldGen;

namespace Asciifactory;

/// <summary>
/// Menu result returned after the main menu exits.
/// </summary>
public enum MenuResult
{
    Singleplayer,
    MultiplayerHost,
    MultiplayerJoin,
    Exit,
}

/// <summary>
/// Game settings configurable from the menu.
/// </summary>
public class GameSettings
{
    public int TickRate { get; set; } = 50;
    public bool EnemiesEnabled { get; set; } = true;
    
    /// <summary>Hidden god mode for testing: no damage, instant mine, infinite items, no save.</summary>
    public bool GodMode { get; set; }
    
    /// <summary>Multiplayer config. Null for singleplayer.</summary>
    public MultiplayerConfig? Multiplayer { get; set; }
}

/// <summary>
/// Multiplayer configuration returned from the menu.
/// </summary>
public class MultiplayerConfig
{
    public string Nickname { get; set; } = "Player";
    public PlayerColor Color { get; set; } = PlayerColor.Yellow;
    public string HostIP { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 7777;
    
    /// <summary>After lobby, this contains the lobby state with all players.</summary>
    public LobbyState? FinalLobby { get; set; }
}

/// <summary>
/// Animated main menu with scrolling terrain, rainbow title, and goofy animations.
/// Factorio-style background with conveyor belts, machines, and wandering bugs.
/// </summary>
public class MainMenu
{
    private readonly Renderer _renderer;
    private readonly InputHandler _input;
    
    // Menu state
    private enum MenuScreen { Main, Play, Settings, About, HostSetup, JoinSetup, Lobby }
    private MenuScreen _screen = MenuScreen.Main;
    private int _selection;
    private GameSettings _settings = new();
    
    // Animation state
    private int _tick;
    private float _scrollOffset;
    
    // Background terrain
    private TileType[,] _terrain = null!;
    private int _terrainW;
    private int _terrainH;
    private int _terrainScrollX;
    
    // Background factory objects
    private record struct FactoryObject(int X, int Y, char Symbol, ConsoleColor Color, int AnimPhase);
    private List<FactoryObject> _factoryObjects = new();
    
    // Background bugs
    private record struct MenuBug(int X, int Y, int DX, int DY, int Timer);
    private List<MenuBug> _bugs = new();
    
    // Conveyor items
    private record struct BeltItem(int X, int Y, int Speed, char Symbol, ConsoleColor Color);
    private List<BeltItem> _beltItems = new();
    
    // Hello World Easter egg
    private int _helloWorldTimer;
    private int _helloWorldX;
    private int _helloWorldY;
    private bool _helloWorldEaten;
    private int _kernelChasingHello;
    
    // Sparkle particles
    private record struct Particle(int X, int Y, int DY, char Symbol, ConsoleColor Color, int Life);
    private List<Particle> _particles = new();
    
    // Multiplayer setup state
    private string _nickname = "Player";
    private int _nicknameCursorPos = 6;
    private PlayerColor _selectedColor = PlayerColor.Yellow;
    private int _setupSelection;
    private string _hostIP = "127.0.0.1";
    private int _hostIPCursorPos = 9;
    private int _port = 7777;
    private int _portCursorPos = 4;
    private bool _editingField;
    
    // Lobby state
    private LobbyState? _lobbyState;
    private NetServer? _hostServer;
    private NetClient? _joinClient;
    private bool _isHost;
    private string _lobbyError = "";
    
    // Tips
    private static readonly string[] Tips =
    {
        "Tip: Bugs hate firewalls. Like, really hate them.",
        "Tip: The planet's insurance does not cover strip mining.",
        "Tip: Kernel Panics are not actual kernels. Don't try to eat them.",
        "Tip: Null Pointers are surprisingly pointy.",
        "Tip: Memory Leaks grow when you poke them. Stop poking.",
        "Tip: THE COMPUTER 9000™ is not Y2K compliant.",
        "Tip: Segfaults were a mistake. We're sorry.",
        "Tip: This planet has been rated 3/5 stars on Yelp.",
        "Tip: Glitches teleport because walking is too mainstream.",
        "Tip: 'Hello, World!' - THE COMPUTER 9000™, probably",
        "Tip: No bugs were harmed in the making of this game. (Just kidding.)",
        "Tip: Conveyor belts: because hands are for quitters.",
        "Tip: Always carry a spare Firewall Turret. Trust us.",
        "Tip: Stack Overflow is both a website and a valid fear.",
        "Tip: Off-by-one errors account for 99% of bugs. Or was it 100%?",
        "Tip: THE COMPUTER 9000™ warranty void if used as intended.",
        "Tip: Infinite loops are just forever friendships with your CPU.",
        "Tip: If your factory has more belts than floor, you're doing it right.",
        "Tip: Copper ore: the mineral that sounds disappointed.",
        "Tip: Our lawyers advise: 'just don't read the terms of service.'",
        "Tip: Bug fact: the 'S' variant can smell fear. And copper.",
        "Tip: Recommended by 0 out of 5 dentists. The 5th one was eaten by bugs.",
        "Tip: 'It works on my machine' - Last words before production deploy.",
        "Tip: THE COMPUTER 9000™ customer support hold time: approximately 4 billion years.",
        "Tip: Segfault. Core dumped. Feelings: hurt.",
        "Tip: Fun fact: all ASCII characters are 100% organic, gluten-free.",
        "Tip: Automation is just procrastination with extra steps. Literally.",
        "Tip: 'Have you tried turning it off and on again?' - Every engineer, to the bugs.",
        "Tip: THE COMPUTER 9000™ may develop sentience. This is a feature, not a bug.",
        "Tip: A conveyor belt to nowhere is still a conveyor belt. Believe in yourself.",
        "Tip: Coal: nature's way of saying 'you're welcome.'",
        "Tip: Recursion: see 'Recursion'.",
        "Tip: THE COMPUTER 9000™ was almost called 'Jeff'. Marketing disagreed.",
        "Tip: THE COMPUTER 9000™ runs on 4 AA batteries and pure existential dread.",
        "Tip: Null Pointer Exceptions: the universe's way of saying 'no.'",
        "Tip: Your factory's carbon footprint is visible from space. From another planet.",
        "Tip: 'I'll refactor later' - The biggest lie in software engineering.",
        "Tip: THE COMPUTER 9000™ does not support Bluetooth. It predates teeth.",
        "Tip: If you stare at the assembler long enough, it stares back.",
        "Tip: Mining: because violence against rocks is socially acceptable.",
        "Tip: This game was tested on animals. They didn't like it.",
        "Tip: THE COMPUTER 9000™ can count to infinity. Twice.",
        "Tip: Error 404: Tip not found. Please rotate your monitor and try again.",
        "Tip: When in doubt, add more conveyor belts. When certain, add even more.",
        "Tip: THE COMPUTER 9000™ once tried to compute the meaning of life. It got 42.",
        "Tip: The Glitches don't respect personal space. Or walls. Or physics.",
        "Tip: Good news: the bugs are edible. Bad news: they bite back.",
        "Tip: 'That's not a memory leak, it's a feature!' - Nobody, ever, honestly.",
        "Tip: THE COMPUTER 9000™ upgrade available! Just kidding. It's perfect. (Please don't sue.)",
        "Tip: Each Kernel Panic is a tiny existential crisis. For your computer.",
        "Tip: Factory tip: if it's on fire, you're either doing great or terribly.",
        "Tip: THE COMPUTER 9000™ assembly required. Batteries not included. Sanity sold separately.",
        "Tip: Water is wet. Bugs are angry. Your factory is on fire. Any questions?",
        "Tip: 'I have a perfectly balanced factory' is a sentence that has never been spoken.",
        "Tip: The S in Segfault stands for 'Sorry.'",
        "Tip: Fun fact: 73% of all statistics are made up on the spot. Like this one.",
        "Tip: THE COMPUTER 9000™ privacy policy: 'What privacy policy?'",
        "Tip: Copper wire was invented by two engineers fighting over a penny.",
        "Tip: Pro tip: read the error message. No, the actual error message. Not that one.",
        "Tip: THE COMPUTER 9000™ can divide by zero. We just don't know what happens next.",
        "Tip: The only thing more infinite than this world is my backlog of refactoring.",
        "Tip: If your code works on the first try, check again. You probably forgot a semicolon.",
        "Tip: The planet's native population consists entirely of bugs. They are not friendly.",
        "Tip: THE COMPUTER 9000™ EULA: 'By using this product, you agree to everything. Forever.'",
        "Tip: Console.WriteLine('Help'); // Not a joke. Actual request.",
        "Tip: Did you know? Null is not zero. Null is not empty. Null is just... disappointment.",
        "Tip: Your factory's efficiency is: ████░░░░░░ 40%. Room for improvement. Always.",
        "Tip: THE COMPUTER 9000™ may cause drowsiness, restlessness, or spontaneous combustion.",
        "Tip: Assembler tip: put the thing in the thing to make the other thing. You're welcome.",
        "Tip: The bugs have a union. They're negotiating for better working conditions. (Your face.)",
        "Tip: Build a Firewall Turret they said. It'll be fun they said. It was. It really was.",
        "Tip: THE COMPUTER 9000™ comes in three colors: beige, off-beige, and existential gray.",
    };
    
    // Rainbow colors for title
    private static readonly ConsoleColor[] RainbowColors =
    {
        ConsoleColor.Red,
        ConsoleColor.DarkYellow,
        ConsoleColor.Yellow,
        ConsoleColor.Green,
        ConsoleColor.Cyan,
        ConsoleColor.Blue,
        ConsoleColor.Magenta,
        ConsoleColor.Red,
        ConsoleColor.DarkYellow,
        ConsoleColor.Yellow,
        ConsoleColor.Green,
        ConsoleColor.Cyan,
        ConsoleColor.Blue,
        ConsoleColor.Magenta,
    };
    
    // ASCII art title
    private static readonly string[] TitleLines =
    {
        " ┏━┓┏━┓┏━╸╻╻┏━╸┏━┓┏━╸╺┳╸┏━┓┏━┓╻ ╻ ",
        " ┣━┫┗━┓┃  ┃┃┣╸ ┣━┫┃   ┃ ┃ ┃┣┳┛┗┳┛ ",
        " ╹ ╹┗━┛┗━╸╹╹╹  ╹ ╹┗━╸ ╹ ┗━┛╹┗╸ ╹  "
    };
    
    // Predefined nicknames to cycle through
    private static readonly string[] DefaultNicknames =
    {
        "Player", "Engineer", "Miner", "Builder", "Crafty", "Smelter",
        "Copper", "Ironside", "CoalRoller", "BugSlayer", "Factory", "ASCII"
    };
    
    public MainMenu()
    {
        _renderer = new Renderer();
        _input = new InputHandler();
        GenerateTerrain();
        SpawnFactoryObjects();
    }
    
    /// <summary>
    /// Maps a console key to a menu navigation command.
    /// Only arrows for navigation, Space for select, Escape for back.
    /// No WASD — those are game-only controls.
    /// </summary>
    private static InputCommand MapMenuKey(ConsoleKey key) => key switch
    {
        ConsoleKey.UpArrow => InputCommand.MoveUp,
        ConsoleKey.DownArrow => InputCommand.MoveDown,
        ConsoleKey.LeftArrow => InputCommand.MoveLeft,
        ConsoleKey.RightArrow => InputCommand.MoveRight,
        ConsoleKey.Spacebar => InputCommand.Interact,
        ConsoleKey.Escape => InputCommand.Quit,
        _ => InputCommand.None,
    };
    
    /// <summary>
    /// Runs the main menu loop. Returns the selected action and settings.
    /// Reads raw keys directly (not via InputHandler.Poll) so text fields work.
    /// </summary>
    public (MenuResult Result, GameSettings Settings) Run()
    {
        Console.CursorVisible = false;
        Console.Clear();
        
        while (true)
        {
            // Read raw key directly — this is critical for text field editing
            ConsoleKeyInfo? rawKey = null;
            if (Console.KeyAvailable)
                rawKey = Console.ReadKey(true);
            
            if (rawKey.HasValue)
            {
                // Map to menu command (arrows only, Space=select, no WASD)
                var command = MapMenuKey(rawKey.Value.Key);
                
                var result = HandleInput(command, rawKey);
                if (result.HasValue)
                    return result.Value;
            }
            
            Update();
            Render();
            _tick++;
            
            Thread.Sleep(60);
        }
    }
    
    private (MenuResult Result, GameSettings Settings)? HandleInput(InputCommand command, ConsoleKeyInfo? rawKey)
    {
        switch (_screen)
        {
            case MenuScreen.Main:
                return HandleMainInput(command);
            case MenuScreen.Play:
                return HandlePlayInput(command);
            case MenuScreen.Settings:
                HandleSettingsInput(command);
                return null;
            case MenuScreen.About:
                if (command != InputCommand.None)
                    _screen = MenuScreen.Main;
                return null;
            case MenuScreen.HostSetup:
                return HandleHostSetupInput(command, rawKey);
            case MenuScreen.JoinSetup:
                return HandleJoinSetupInput(command, rawKey);
            case MenuScreen.Lobby:
                return HandleLobbyInput(command);
        }
        return null;
    }
    
    private (MenuResult, GameSettings)? HandleMainInput(InputCommand command)
    {
        int maxItems = 4;
        switch (command)
        {
            case InputCommand.MoveUp: _selection = Math.Max(0, _selection - 1); break;
            case InputCommand.MoveDown: _selection = Math.Min(maxItems - 1, _selection + 1); break;
            case InputCommand.Interact:
            case InputCommand.Mine:
                switch (_selection)
                {
                    case 0: _screen = MenuScreen.Play; _selection = 0; break;
                    case 1: _screen = MenuScreen.Settings; _selection = 0; break;
                    case 2: _screen = MenuScreen.About; break;
                    case 3: return (MenuResult.Exit, _settings);
                }
                break;
        }
        return null;
    }
    
    private (MenuResult, GameSettings)? HandlePlayInput(InputCommand command)
    {
        int maxItems = 3;
        switch (command)
        {
            case InputCommand.MoveUp: _selection = Math.Max(0, _selection - 1); break;
            case InputCommand.MoveDown: _selection = Math.Min(maxItems - 1, _selection + 1); break;
            case InputCommand.Quit:
                _screen = MenuScreen.Main;
                _selection = 0;
                break;
            case InputCommand.Interact:
            case InputCommand.Mine:
                switch (_selection)
                {
                    case 0: return (MenuResult.Singleplayer, _settings);
                    case 1:
                        // Host Game
                        _screen = MenuScreen.HostSetup;
                        _setupSelection = 0;
                        _nickname = DefaultNicknames[Random.Shared.Next(DefaultNicknames.Length)];
                        _nicknameCursorPos = _nickname.Length;
                        _selectedColor = PlayerColor.Yellow;
                        _port = 7777;
                        _isHost = true;
                        break;
                    case 2:
                        // Join Game
                        _screen = MenuScreen.JoinSetup;
                        _setupSelection = 0;
                        _nickname = DefaultNicknames[Random.Shared.Next(DefaultNicknames.Length)];
                        _nicknameCursorPos = _nickname.Length;
                        _selectedColor = PlayerColor.Cyan;
                        _hostIP = "127.0.0.1";
                        _hostIPCursorPos = _hostIP.Length;
                        _port = 7777;
                        _isHost = false;
                        break;
                }
                break;
        }
        return null;
    }
    
    private void HandleSettingsInput(InputCommand command)
    {
        int maxItems = 3;
        switch (command)
        {
            case InputCommand.MoveUp: _selection = Math.Max(0, _selection - 1); break;
            case InputCommand.MoveDown: _selection = Math.Min(maxItems - 1, _selection + 1); break;
            case InputCommand.MoveLeft:
                if (_selection == 0) _settings.TickRate = Math.Min(200, _settings.TickRate + 10);
                if (_selection == 1) _settings.EnemiesEnabled = true;
                break;
            case InputCommand.MoveRight:
                if (_selection == 0) _settings.TickRate = Math.Max(10, _settings.TickRate - 10);
                if (_selection == 1) _settings.EnemiesEnabled = false;
                break;
            case InputCommand.Interact:
            case InputCommand.Mine:
                if (_selection == 1) _settings.EnemiesEnabled = !_settings.EnemiesEnabled;
                if (_selection == 2) { _settings = new GameSettings(); }
                break;
            case InputCommand.Quit:
                _screen = MenuScreen.Main;
                _selection = 1;
                break;
        }
    }
    
    private (MenuResult, GameSettings)? HandleHostSetupInput(InputCommand command, ConsoleKeyInfo? rawKey)
    {
        int maxItems = 4; // Nickname, Color, Port, Start
        
        if (_editingField && rawKey.HasValue)
        {
            var key = rawKey.Value;
            
            if (_setupSelection == 0) // Editing nickname
            {
                if (key.Key == ConsoleKey.Escape)
                {
                    _editingField = false;
                    return null;
                }
                if (key.Key == ConsoleKey.Enter)
                {
                    _editingField = false;
                    return null;
                }
                if (key.Key == ConsoleKey.Backspace && _nicknameCursorPos > 0)
                {
                    _nickname = _nickname.Remove(_nicknameCursorPos - 1, 1);
                    _nicknameCursorPos--;
                    return null;
                }
                if (char.IsLetterOrDigit(key.KeyChar) && _nickname.Length < 12)
                {
                    _nickname = _nickname.Insert(_nicknameCursorPos, key.KeyChar.ToString());
                    _nicknameCursorPos++;
                    return null;
                }
                return null;
            }
            else if (_setupSelection == 2) // Editing port
            {
                if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Enter)
                {
                    _editingField = false;
                    return null;
                }
                if (key.Key == ConsoleKey.Backspace && _port.ToString().Length > 0)
                {
                    string portStr = _port.ToString();
                    if (portStr.Length > 1)
                        _port = int.Parse(portStr[..^1]);
                    else
                        _port = 7777;
                    return null;
                }
                if (char.IsDigit(key.KeyChar))
                {
                    string newPort = _port.ToString() + key.KeyChar;
                    if (newPort.Length <= 5 && int.TryParse(newPort, out int p) && p <= 65535)
                        _port = p;
                    return null;
                }
                return null;
            }
        }
        
        switch (command)
        {
            case InputCommand.MoveUp: _setupSelection = Math.Max(0, _setupSelection - 1); break;
            case InputCommand.MoveDown: _setupSelection = Math.Min(maxItems - 1, _setupSelection + 1); break;
            case InputCommand.MoveLeft:
                if (_setupSelection == 1)
                    _selectedColor = (PlayerColor)(((int)_selectedColor - 1 + 4) % 4);
                break;
            case InputCommand.MoveRight:
                if (_setupSelection == 1)
                    _selectedColor = (PlayerColor)(((int)_selectedColor + 1) % 4);
                break;
            case InputCommand.Interact:
            case InputCommand.Mine:
                if (_setupSelection == 0 || _setupSelection == 2)
                {
                    _editingField = true;
                }
                else if (_setupSelection == 3) // Start
                {
                    if (string.IsNullOrWhiteSpace(_nickname))
                        _nickname = "Host";
                    
                    // Start server and go to lobby
                    try
                    {
                        _hostServer = new NetServer(_port);
                        _hostServer.Start();
                        
                        var hostInfo = new PlayerInfo
                        {
                            Nickname = _nickname,
                            Color = _selectedColor,
                            IsHost = true,
                        };
                        _hostServer.AddHostPlayer(hostInfo);
                        
                        _settings.Multiplayer = new MultiplayerConfig
                        {
                            Nickname = _nickname,
                            Color = _selectedColor,
                            Port = _port,
                        };
                        
                        _screen = MenuScreen.Lobby;
                        _lobbyError = "";
                    }
                    catch (Exception ex)
                    {
                        _lobbyError = $"Failed to start server: {ex.Message}";
                    }
                }
                break;
            case InputCommand.Quit:
                _screen = MenuScreen.Play;
                _selection = 1;
                break;
        }
        return null;
    }
    
    private (MenuResult, GameSettings)? HandleJoinSetupInput(InputCommand command, ConsoleKeyInfo? rawKey)
    {
        int maxItems = 4; // Nickname, Color, IP, Connect
        
        if (_editingField && rawKey.HasValue)
        {
            var key = rawKey.Value;
            
            if (_setupSelection == 0) // Editing nickname
            {
                if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Enter)
                {
                    _editingField = false;
                    return null;
                }
                if (key.Key == ConsoleKey.Backspace && _nicknameCursorPos > 0)
                {
                    _nickname = _nickname.Remove(_nicknameCursorPos - 1, 1);
                    _nicknameCursorPos--;
                    return null;
                }
                if ((char.IsLetterOrDigit(key.KeyChar) || key.KeyChar == '_') && _nickname.Length < 12)
                {
                    _nickname = _nickname.Insert(_nicknameCursorPos, key.KeyChar.ToString());
                    _nicknameCursorPos++;
                    return null;
                }
                return null;
            }
            else if (_setupSelection == 2) // Editing IP
            {
                if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Enter)
                {
                    _editingField = false;
                    return null;
                }
                if (key.Key == ConsoleKey.Backspace && _hostIPCursorPos > 0)
                {
                    _hostIP = _hostIP.Remove(_hostIPCursorPos - 1, 1);
                    _hostIPCursorPos--;
                    return null;
                }
                if ((char.IsDigit(key.KeyChar) || key.KeyChar == '.') && _hostIP.Length < 15)
                {
                    _hostIP = _hostIP.Insert(_hostIPCursorPos, key.KeyChar.ToString());
                    _hostIPCursorPos++;
                    return null;
                }
                return null;
            }
        }
        
        switch (command)
        {
            case InputCommand.MoveUp: _setupSelection = Math.Max(0, _setupSelection - 1); break;
            case InputCommand.MoveDown: _setupSelection = Math.Min(maxItems - 1, _setupSelection + 1); break;
            case InputCommand.MoveLeft:
                if (_setupSelection == 1)
                    _selectedColor = (PlayerColor)(((int)_selectedColor - 1 + 4) % 4);
                break;
            case InputCommand.MoveRight:
                if (_setupSelection == 1)
                    _selectedColor = (PlayerColor)(((int)_selectedColor + 1) % 4);
                break;
            case InputCommand.Interact:
            case InputCommand.Mine:
                if (_setupSelection == 0 || _setupSelection == 2)
                {
                    _editingField = true;
                }
                else if (_setupSelection == 3) // Connect
                {
                    if (string.IsNullOrWhiteSpace(_nickname))
                        _nickname = "Player";
                    
                    if (string.IsNullOrWhiteSpace(_hostIP))
                        _hostIP = "127.0.0.1";
                    
                    try
                    {
                        _joinClient = new NetClient(_hostIP, _port);
                        var clientInfo = new PlayerInfo
                        {
                            Nickname = _nickname,
                            Color = _selectedColor,
                            IsHost = false,
                        };
                        
                        if (_joinClient.Connect(clientInfo))
                        {
                            _settings.Multiplayer = new MultiplayerConfig
                            {
                                Nickname = _nickname,
                                Color = _selectedColor,
                                HostIP = _hostIP,
                                Port = _port,
                            };
                            
                            _screen = MenuScreen.Lobby;
                            _lobbyError = "";
                        }
                        else
                        {
                            _lobbyError = _joinClient.Error ?? "Connection failed.";
                            _joinClient.Dispose();
                            _joinClient = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        _lobbyError = $"Connection failed: {ex.Message}";
                    }
                }
                break;
            case InputCommand.Quit:
                _screen = MenuScreen.Play;
                _selection = 2;
                break;
        }
        return null;
    }
    
    private (MenuResult, GameSettings)? HandleLobbyInput(InputCommand command)
    {
        switch (command)
        {
            case InputCommand.Interact:
            case InputCommand.Mine:
                if (_isHost && _hostServer != null)
                {
                    // Toggle ready
                    _hostServer.SetHostReady(true);
                    
                    // If all ready, start the game
                    if (_hostServer.AllReady && _hostServer.PlayerCount >= 1)
                    {
                        _hostServer.StartGame();
                        
                        _settings.Multiplayer!.FinalLobby = _hostServer.GetLobbyState();
                        return (MenuResult.MultiplayerHost, _settings);
                    }
                }
                else if (!_isHost && _joinClient != null)
                {
                    // Toggle ready
                    _joinClient.SendReady();
                }
                break;
            
            case InputCommand.Quit:
                // Leave lobby
                _joinClient?.Dispose();
                _joinClient = null;
                _hostServer?.Dispose();
                _hostServer = null;
                _screen = MenuScreen.Play;
                break;
        }
        
        // Check if client received game start
        if (!_isHost && _joinClient?.GameStarted == true)
        {
            _settings.Multiplayer!.FinalLobby = _joinClient.LobbyState;
            return (MenuResult.MultiplayerJoin, _settings);
        }
        
        return null;
    }
    
    // ========== TERRAIN GENERATION ==========
    
    private void GenerateTerrain()
    {
        _terrainW = 200;
        _terrainH = 50;
        _terrain = new TileType[_terrainW, _terrainH];
        _terrainScrollX = 0;
        
        int seed = Random.Shared.Next();
        for (int x = 0; x < _terrainW; x++)
        {
            for (int y = 0; y < _terrainH; y++)
            {
                double n = SimpleNoise(x * 0.1 + seed, y * 0.15);
                _terrain[x, y] = n switch
                {
                    < -0.3 => TileType.Water,
                    < -0.1 => TileType.Sand,
                    < 0.2 => TileType.Grass,
                    < 0.4 => TileType.Dirt,
                    < 0.6 => TileType.Stone,
                    _ => TileType.Snow,
                };
                
                if (_terrain[x, y] == TileType.Grass && SimpleNoise(x * 0.3 + 100, y * 0.3) > 0.6)
                    _terrain[x, y] = TileType.IronOre;
                if (_terrain[x, y] == TileType.Grass && SimpleNoise(x * 0.3 + 200, y * 0.3) > 0.65)
                    _terrain[x, y] = TileType.CopperOre;
                if (_terrain[x, y] == TileType.Dirt && SimpleNoise(x * 0.3 + 300, y * 0.3) > 0.7)
                    _terrain[x, y] = TileType.Coal;
            }
        }
    }
    
    private static double SimpleNoise(double x, double y)
    {
        int xi = (int)Math.Floor(x);
        int yi = (int)Math.Floor(y);
        double xf = x - xi;
        double yf = y - yi;
        
        double v00 = Hash2D(xi, yi);
        double v10 = Hash2D(xi + 1, yi);
        double v01 = Hash2D(xi, yi + 1);
        double v11 = Hash2D(xi + 1, yi + 1);
        
        double sx = xf * xf * (3 - 2 * xf);
        double sy = yf * yf * (3 - 2 * yf);
        
        double a = v00 * (1 - sx) + v10 * sx;
        double b = v01 * (1 - sx) + v11 * sx;
        
        return a * (1 - sy) + b * sy;
    }
    
    private static double Hash2D(int x, int y)
    {
        uint n = (uint)(x * 374761393 + y * 668265263);
        n = (n ^ (n >> 13)) * 1274126177;
        n = n ^ (n >> 16);
        return (n & 0xFFFFFF) / (double)0xFFFFFF * 2.0 - 1.0;
    }
    
    private void SpawnFactoryObjects()
    {
        _factoryObjects.Clear();
        
        for (int i = 0; i < 30; i++)
        {
            int x = Random.Shared.Next(5, _terrainW - 5);
            int y = Random.Shared.Next(10, _terrainH - 10);
            var (sym, color) = Random.Shared.Next(8) switch
            {
                0 => ('M', ConsoleColor.Cyan),
                1 => ('F', ConsoleColor.Red),
                2 => ('A', ConsoleColor.Green),
                3 => ('G', ConsoleColor.Yellow),
                4 => ('□', ConsoleColor.Gray),
                5 => ('R', ConsoleColor.Magenta),
                6 => ('T', ConsoleColor.DarkCyan),
                _ => ('›', ConsoleColor.White),
            };
            _factoryObjects.Add(new FactoryObject(x, y, sym, color, Random.Shared.Next(100)));
        }
        
        _bugs.Clear();
        for (int i = 0; i < 12; i++)
        {
            _bugs.Add(new MenuBug(
                Random.Shared.Next(5, _terrainW - 5),
                Random.Shared.Next(10, _terrainH - 10),
                Random.Shared.Next(-1, 2),
                Random.Shared.Next(-1, 2),
                Random.Shared.Next(5, 20)));
        }
        
        _beltItems.Clear();
        for (int i = 0; i < 15; i++)
        {
            _beltItems.Add(new BeltItem(
                Random.Shared.Next(5, _terrainW - 5),
                Random.Shared.Next(10, _terrainH - 10),
                Random.Shared.Next(1, 3),
                Random.Shared.Next(4) switch { 0 => '◆', 1 => '◈', 2 => '✦', _ => '●' },
                Random.Shared.Next(4) switch { 0 => ConsoleColor.White, 1 => ConsoleColor.DarkYellow, 2 => ConsoleColor.Green, _ => ConsoleColor.Cyan }));
        }
    }
    
    // ========== UPDATE ==========
    
    private void Update()
    {
        _renderer.CheckResize();
        
        _scrollOffset += 0.15f;
        if (_scrollOffset >= 1.0f)
        {
            _scrollOffset -= 1.0f;
            _terrainScrollX++;
            
            if (_terrainScrollX >= _terrainW)
            {
                _terrainScrollX = 0;
                GenerateTerrain();
                SpawnFactoryObjects();
            }
        }
        
        for (int i = 0; i < _bugs.Count; i++)
        {
            var bug = _bugs[i];
            bug.Timer--;
            if (bug.Timer <= 0)
            {
                bug.Timer = Random.Shared.Next(5, 20);
                if (Random.Shared.Next(3) == 0)
                {
                    bug.DX = Random.Shared.Next(-1, 2);
                    bug.DY = Random.Shared.Next(-1, 2);
                }
                bug.X = (bug.X + bug.DX + _terrainW) % _terrainW;
                bug.Y = Math.Clamp(bug.Y + bug.DY, 5, _terrainH - 5);
            }
            _bugs[i] = bug;
        }
        
        for (int i = 0; i < _beltItems.Count; i++)
        {
            var item = _beltItems[i];
            item.X = (item.X + item.Speed) % _terrainW;
            _beltItems[i] = item;
        }
        
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Y += p.DY;
            p.Life--;
            if (p.Life <= 0)
                _particles.RemoveAt(i);
            else
                _particles[i] = p;
        }
        
        if (_tick % 5 == 0)
        {
            foreach (var obj in _factoryObjects)
            {
                if (Random.Shared.Next(5) == 0)
                {
                    _particles.Add(new Particle(
                        obj.X, obj.Y - 1, -1,
                        Random.Shared.Next(3) switch { 0 => '★', 1 => '✦', _ => '✧' },
                        Random.Shared.Next(3) switch { 0 => ConsoleColor.Yellow, 1 => ConsoleColor.Cyan, _ => ConsoleColor.White },
                        Random.Shared.Next(3, 8)));
                }
            }
        }
        
        _helloWorldTimer--;
        if (_helloWorldTimer <= 0)
        {
            if (Random.Shared.Next(300) == 0 && !_helloWorldEaten)
            {
                _helloWorldX = Random.Shared.Next(10, _renderer.Width - 30);
                _helloWorldY = Random.Shared.Next(_renderer.Height / 2 + 5, _renderer.Height - 6);
                _helloWorldTimer = 120;
                _helloWorldEaten = false;
                _kernelChasingHello = 0;
            }
        }
        
        if (_helloWorldTimer > 0 && !_helloWorldEaten)
        {
            _kernelChasingHello++;
            if (_kernelChasingHello > 60)
            {
                _helloWorldEaten = true;
                _helloWorldTimer = 30;
                for (int i = 0; i < 8; i++)
                {
                    _particles.Add(new Particle(
                        _helloWorldX + 5, _helloWorldY,
                        Random.Shared.Next(-1, 2),
                        Random.Shared.Next(3) switch { 0 => '★', 1 => '!', _ => '#' },
                        ConsoleColor.Red,
                        Random.Shared.Next(5, 15)));
                }
            }
        }
        
        // Update lobby state from network
        if (_screen == MenuScreen.Lobby)
        {
            if (_isHost && _hostServer != null)
            {
                _lobbyState = _hostServer.GetLobbyState();
            }
            else if (!_isHost && _joinClient != null)
            {
                _lobbyState = _joinClient.LobbyState;
            }
        }
    }
    
    // ========== RENDER ==========
    
    private void Render()
    {
        _renderer.Clear();
        
        DrawBackgroundTerrain();
        DrawFactoryScene();
        DrawTitle();
        DrawMenuContent();
        DrawMarquee();
        
        _renderer.Present();
    }
    
    private void DrawBackgroundTerrain()
    {
        int w = _renderer.Width;
        int h = _renderer.Height;
        int offsetX = (int)_scrollOffset;
        
        for (int sy = 0; sy < h; sy++)
        {
            for (int sx = 0; sx < w; sx++)
            {
                int tx = (sx + _terrainScrollX + offsetX) % _terrainW;
                int ty = (sy * _terrainH / h) % _terrainH;
                
                if (tx < 0) tx += _terrainW;
                if (ty < 0) ty += _terrainH;
                
                var tile = _terrain[tx, ty];
                var (symbol, color) = TileTypeInfo.GetVisual(tile);
                
                ConsoleColor dimColor = DimColor(color);
                _renderer.SetCell(sx, sy, symbol, dimColor);
            }
        }
    }
    
    private void DrawFactoryScene()
    {
        int w = _renderer.Width;
        int h = _renderer.Height;
        int offsetX = (int)_scrollOffset;
        
        foreach (var obj in _factoryObjects)
        {
            int sx = obj.X - _terrainScrollX - offsetX;
            if (sx < -5) sx += _terrainW;
            int sy = obj.Y * h / _terrainH;
            
            if (sx >= 0 && sx < w && sy >= 0 && sy < h)
            {
                char ch = obj.Symbol;
                if (ch == 'A' && (_tick + obj.AnimPhase) % 20 < 10) ch = 'Ã';
                _renderer.SetCell(sx, sy, ch, obj.Color);
            }
        }
        
        foreach (var item in _beltItems)
        {
            int sx = item.X - _terrainScrollX - offsetX;
            if (sx < -5) sx += _terrainW;
            int sy = item.Y * h / _terrainH;
            
            if (sx >= 0 && sx < w && sy >= 0 && sy < h)
                _renderer.SetCell(sx, sy, item.Symbol, item.Color);
        }
        
        char[] bugChars = { 'b', 'g', 'S', 'ø', 'K', 'm' };
        ConsoleColor[] bugColors = { ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.Red, ConsoleColor.White, ConsoleColor.DarkRed, ConsoleColor.DarkCyan };
        
        for (int i = 0; i < _bugs.Count; i++)
        {
            var bug = _bugs[i];
            int sx = bug.X - _terrainScrollX - offsetX;
            if (sx < -5) sx += _terrainW;
            int sy = bug.Y * h / _terrainH;
            
            if (sx >= 0 && sx < w && sy >= 0 && sy < h)
            {
                int ci = i % bugChars.Length;
                _renderer.SetCell(sx, sy, bugChars[ci], bugColors[ci]);
            }
        }
        
        foreach (var p in _particles)
        {
            int sx = p.X - _terrainScrollX - offsetX;
            if (sx < -5) sx += _terrainW;
            int sy = p.Y;
            
            if (sx >= 0 && sx < w && sy >= 0 && sy < h)
                _renderer.SetCell(sx, sy, p.Symbol, p.Color);
        }
    }
    
    private void DrawTitle()
    {
        int w = _renderer.Width;
        int titleY = 3;
        
        float floatOffset = (float)Math.Sin(_tick * 0.04) * 1.5f;
        titleY += (int)floatOffset;
        
        int startTitleY = Math.Max(0, titleY);
        int startTitleX = (w - TitleLines[0].Length) / 2;
        
        for (int i = 0; i < TitleLines.Length; i++)
        {
            int y = startTitleY + i;
            if (y < 0 || y >= _renderer.Height - 5) continue;
            
            string line = TitleLines[i];
            for (int j = 0; j < line.Length; j++)
            {
                int x = startTitleX + j;
                if (x < 0 || x >= w) continue;
                
                char ch = line[j];
                if (ch == ' ') continue;
                
                int colorIdx = (j + i * 2 + _tick / 3) % RainbowColors.Length;
                ConsoleColor color = RainbowColors[colorIdx];
                
                int wiggle = (int)(Math.Sin((_tick * 0.05) + j * 0.3) * 0.5);
                
                _renderer.SetCell(x + wiggle, y, ch, color);
            }
        }
        
        string subtitle = "The ASCII Factory Simulation";
        int subX = (w - subtitle.Length) / 2;
        int subY = startTitleY + TitleLines.Length + 1;
        if (subY < _renderer.Height - 5)
        {
            bool bright = _tick % 60 < 40;
            _renderer.DrawString(subX, subY, subtitle, bright ? ConsoleColor.White : ConsoleColor.Gray);
        }
        
        string ver = "v1.0";
        _renderer.DrawString(subX + subtitle.Length + 2, subY, ver, ConsoleColor.DarkGray);
    }
    
    private void DrawMenuContent()
    {
        int w = _renderer.Width;
        int h = _renderer.Height;
        
        int menuCenterY = h / 2 + 3;
        int menuCenterX = w / 2;
        
        switch (_screen)
        {
            case MenuScreen.Main: DrawMainMenu(menuCenterX, menuCenterY); break;
            case MenuScreen.Play: DrawPlayMenu(menuCenterX, menuCenterY); break;
            case MenuScreen.Settings: DrawSettingsMenu(menuCenterX, menuCenterY); break;
            case MenuScreen.About: DrawAboutScreen(menuCenterX, menuCenterY); break;
            case MenuScreen.HostSetup: DrawHostSetupMenu(menuCenterX, menuCenterY); break;
            case MenuScreen.JoinSetup: DrawJoinSetupMenu(menuCenterX, menuCenterY); break;
            case MenuScreen.Lobby: DrawLobbyScreen(menuCenterX, menuCenterY); break;
        }
        
        if (_helloWorldTimer > 0 && !_helloWorldEaten)
        {
            string hw = "Hello, World!";
            _renderer.DrawString(_helloWorldX, _helloWorldY, hw, ConsoleColor.Green);
            
            if (_kernelChasingHello > 10)
            {
                int kx = _helloWorldX - 3 + _kernelChasingHello / 5;
                int ky = _helloWorldY;
                if (kx < _helloWorldX + hw.Length)
                    _renderer.SetCell(kx, ky, 'K', ConsoleColor.DarkRed);
            }
        }
    }
    
    private void DrawMainMenu(int cx, int cy)
    {
        string[] items = { "Play", "Settings", "About", "Exit" };
        
        int boxW = 32;
        int boxH = items.Length * 2 + 5;
        int boxX = cx - boxW / 2;
        int boxY = cy - boxH / 2;
        
        for (int y = boxY - 1; y < boxY + boxH + 1; y++)
            for (int x = boxX - 2; x < boxX + boxW + 2; x++)
                if (x >= 0 && x < _renderer.Width && y >= 0 && y < _renderer.Height)
                    _renderer.SetCell(x, y, ' ', ConsoleColor.Black, ConsoleColor.Black);
        
        _renderer.DrawString(boxX, boxY, $"╔{new string('═', boxW - 2)}╗", ConsoleColor.Cyan);
        for (int i = 0; i < boxH - 2; i++)
        {
            _renderer.DrawString(boxX, boxY + 1 + i, "║", ConsoleColor.Cyan);
            _renderer.DrawString(boxX + boxW - 1, boxY + 1 + i, "║", ConsoleColor.Cyan);
        }
        _renderer.DrawString(boxX, boxY + boxH - 1, $"╚{new string('═', boxW - 2)}╝", ConsoleColor.Cyan);
        
        int tipIdx = (_tick / 180) % Tips.Length;
        string tip = Tips[tipIdx];
        if (tip.Length > boxW - 6) tip = tip[..(boxW - 6)] + "…";
        _renderer.DrawString(boxX + 2, boxY + 1, tip, ConsoleColor.DarkYellow);
        _renderer.DrawString(boxX, boxY + 2, $"╠{new string('═', boxW - 2)}╣", ConsoleColor.Cyan);
        
        for (int i = 0; i < items.Length; i++)
        {
            int itemY = boxY + 4 + i * 2;
            bool selected = i == _selection;
            
            string selector = selected ? "▸ " : "  ";
            string item = items[i];
            
            ConsoleColor color = selected ? ConsoleColor.White : ConsoleColor.Gray;
            string display = $"{selector}{item}";
            
            if (selected)
            {
                string highlight = new string('─', item.Length + 4);
                _renderer.DrawString(boxX + 3, itemY, highlight, ConsoleColor.DarkCyan);
            }
            
            _renderer.DrawString(boxX + 5, itemY, display, color);
        }
        
        _renderer.DrawString(boxX + 2, boxY + boxH - 2, "↑↓ Navigate  Space: Select", ConsoleColor.DarkGray);
    }
    
    private void DrawPlayMenu(int cx, int cy)
    {
        string[] items = { "Singleplayer", "Host Game", "Join Game" };
        
        int boxW = 36;
        int boxH = items.Length * 2 + 5;
        int boxX = cx - boxW / 2;
        int boxY = cy - boxH / 2;
        
        for (int y = boxY - 1; y < boxY + boxH + 1; y++)
            for (int x = boxX - 2; x < boxX + boxW + 2; x++)
                if (x >= 0 && x < _renderer.Width && y >= 0 && y < _renderer.Height)
                    _renderer.SetCell(x, y, ' ', ConsoleColor.Black, ConsoleColor.Black);
        
        _renderer.DrawString(boxX, boxY, $"╔{new string('═', boxW - 2)}╗", ConsoleColor.Green);
        for (int i = 0; i < boxH - 2; i++)
        {
            _renderer.DrawString(boxX, boxY + 1 + i, "║", ConsoleColor.Green);
            _renderer.DrawString(boxX + boxW - 1, boxY + 1 + i, "║", ConsoleColor.Green);
        }
        _renderer.DrawString(boxX, boxY + boxH - 1, $"╚{new string('═', boxW - 2)}╝", ConsoleColor.Green);
        
        _renderer.DrawString(boxX + 2, boxY + 1, "PLAY", ConsoleColor.Green);
        _renderer.DrawString(boxX, boxY + 2, $"╠{new string('═', boxW - 2)}╣", ConsoleColor.Green);
        
        for (int i = 0; i < items.Length; i++)
        {
            int itemY = boxY + 4 + i * 2;
            bool selected = i == _selection;
            
            string selector = selected ? "▸ " : "  ";
            ConsoleColor color = selected ? ConsoleColor.White : ConsoleColor.Gray;
            
            _renderer.DrawString(boxX + 5, itemY, $"{selector}{items[i]}", color);
        }
        
        _renderer.DrawString(boxX + 2, boxY + boxH - 2, "↑↓ Select  Space: Play  Esc: Back", ConsoleColor.DarkGray);
    }
    
    private void DrawHostSetupMenu(int cx, int cy)
    {
        int boxW = 44;
        int boxH = 14;
        int boxX = cx - boxW / 2;
        int boxY = cy - boxH / 2;
        
        // Background
        for (int y = boxY - 1; y < boxY + boxH + 1; y++)
            for (int x = boxX - 2; x < boxX + boxW + 2; x++)
                if (x >= 0 && x < _renderer.Width && y >= 0 && y < _renderer.Height)
                    _renderer.SetCell(x, y, ' ', ConsoleColor.Black, ConsoleColor.Black);
        
        _renderer.DrawString(boxX, boxY, $"╔{new string('═', boxW - 2)}╗", ConsoleColor.Yellow);
        for (int i = 0; i < boxH - 2; i++)
        {
            _renderer.DrawString(boxX, boxY + 1 + i, "║", ConsoleColor.Yellow);
            _renderer.DrawString(boxX + boxW - 1, boxY + 1 + i, "║", ConsoleColor.Yellow);
        }
        _renderer.DrawString(boxX, boxY + boxH - 1, $"╚{new string('═', boxW - 2)}╝", ConsoleColor.Yellow);
        
        _renderer.DrawString(boxX + 2, boxY + 1, "HOST GAME", ConsoleColor.Yellow);
        _renderer.DrawString(boxX, boxY + 2, $"╠{new string('═', boxW - 2)}╣", ConsoleColor.Yellow);
        
        // Nickname field
        {
            int y = boxY + 4;
            bool selected = _setupSelection == 0;
            bool editing = selected && _editingField;
            string label = selected ? "▸ " : "  ";
            _renderer.DrawString(boxX + 3, y, $"{label}Nickname:", selected ? ConsoleColor.White : ConsoleColor.Gray);
            string nick = _nickname.PadRight(14);
            _renderer.DrawString(boxX + 18, y, $"[{$"{nick}"}]", editing ? ConsoleColor.Cyan : ConsoleColor.White);
            // Cursor blink
            if (editing && _tick % 20 < 10)
                _renderer.SetCell(boxX + 19 + _nicknameCursorPos, y, '▏', ConsoleColor.White);
        }
        
        // Color field
        {
            int y = boxY + 6;
            bool selected = _setupSelection == 1;
            string label = selected ? "▸ " : "  ";
            _renderer.DrawString(boxX + 3, y, $"{label}Color:", selected ? ConsoleColor.White : ConsoleColor.Gray);
            
            string[] colorNames = { "Yellow", "Cyan", "Green", "Magenta" };
            ConsoleColor[] colorValues = { ConsoleColor.Yellow, ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Magenta };
            
            for (int i = 0; i < 4; i++)
            {
                int x = boxX + 15 + i * 7;
                bool isCurrent = (int)_selectedColor == i;
                string prefix = isCurrent ? "▸" : " ";
                _renderer.DrawString(x, y, $"{prefix}{colorNames[i]}", isCurrent ? colorValues[i] : ConsoleColor.DarkGray);
            }
        }
        
        // Port field
        {
            int y = boxY + 8;
            bool selected = _setupSelection == 2;
            bool editing = selected && _editingField;
            string label = selected ? "▸ " : "  ";
            _renderer.DrawString(boxX + 3, y, $"{label}Port:", selected ? ConsoleColor.White : ConsoleColor.Gray);
            _renderer.DrawString(boxX + 18, y, $"[{_port}]", editing ? ConsoleColor.Cyan : ConsoleColor.White);
            if (editing && _tick % 20 < 10)
                _renderer.SetCell(boxX + 19 + _port.ToString().Length, y, '▏', ConsoleColor.White);
        }
        
        // Start button
        {
            int y = boxY + 10;
            bool selected = _setupSelection == 3;
            string label = selected ? "▸ " : "  ";
            ConsoleColor color = selected ? ConsoleColor.Green : ConsoleColor.Gray;
            _renderer.DrawString(boxX + 3, y, $"{label}▶ Start Lobby", color);
        }
        
        _renderer.DrawString(boxX + 2, boxY + boxH - 2, "Space: Edit/Select  ←→: Color  Esc: Back", ConsoleColor.DarkGray);
    }
    
    private void DrawJoinSetupMenu(int cx, int cy)
    {
        int boxW = 44;
        int boxH = 14;
        int boxX = cx - boxW / 2;
        int boxY = cy - boxH / 2;
        
        for (int y = boxY - 1; y < boxY + boxH + 1; y++)
            for (int x = boxX - 2; x < boxX + boxW + 2; x++)
                if (x >= 0 && x < _renderer.Width && y >= 0 && y < _renderer.Height)
                    _renderer.SetCell(x, y, ' ', ConsoleColor.Black, ConsoleColor.Black);
        
        _renderer.DrawString(boxX, boxY, $"╔{new string('═', boxW - 2)}╗", ConsoleColor.Cyan);
        for (int i = 0; i < boxH - 2; i++)
        {
            _renderer.DrawString(boxX, boxY + 1 + i, "║", ConsoleColor.Cyan);
            _renderer.DrawString(boxX + boxW - 1, boxY + 1 + i, "║", ConsoleColor.Cyan);
        }
        _renderer.DrawString(boxX, boxY + boxH - 1, $"╚{new string('═', boxW - 2)}╝", ConsoleColor.Cyan);
        
        _renderer.DrawString(boxX + 2, boxY + 1, "JOIN GAME", ConsoleColor.Cyan);
        _renderer.DrawString(boxX, boxY + 2, $"╠{new string('═', boxW - 2)}╣", ConsoleColor.Cyan);
        
        // Nickname
        {
            int y = boxY + 4;
            bool selected = _setupSelection == 0;
            bool editing = selected && _editingField;
            string label = selected ? "▸ " : "  ";
            _renderer.DrawString(boxX + 3, y, $"{label}Nickname:", selected ? ConsoleColor.White : ConsoleColor.Gray);
            string nick = _nickname.PadRight(14);
            _renderer.DrawString(boxX + 18, y, $"[{nick}]", editing ? ConsoleColor.Cyan : ConsoleColor.White);
            if (editing && _tick % 20 < 10)
                _renderer.SetCell(boxX + 19 + _nicknameCursorPos, y, '▏', ConsoleColor.White);
        }
        
        // Color
        {
            int y = boxY + 6;
            bool selected = _setupSelection == 1;
            string label = selected ? "▸ " : "  ";
            _renderer.DrawString(boxX + 3, y, $"{label}Color:", selected ? ConsoleColor.White : ConsoleColor.Gray);
            
            string[] colorNames = { "Yellow", "Cyan", "Green", "Magenta" };
            ConsoleColor[] colorValues = { ConsoleColor.Yellow, ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Magenta };
            
            for (int i = 0; i < 4; i++)
            {
                int x = boxX + 15 + i * 7;
                bool isCurrent = (int)_selectedColor == i;
                string prefix = isCurrent ? "▸" : " ";
                _renderer.DrawString(x, y, $"{prefix}{colorNames[i]}", isCurrent ? colorValues[i] : ConsoleColor.DarkGray);
            }
        }
        
        // IP field
        {
            int y = boxY + 8;
            bool selected = _setupSelection == 2;
            bool editing = selected && _editingField;
            string label = selected ? "▸ " : "  ";
            _renderer.DrawString(boxX + 3, y, $"{label}Host IP:", selected ? ConsoleColor.White : ConsoleColor.Gray);
            string ip = _hostIP.PadRight(15);
            _renderer.DrawString(boxX + 18, y, $"[{ip}]", editing ? ConsoleColor.Cyan : ConsoleColor.White);
            if (editing && _tick % 20 < 10)
                _renderer.SetCell(boxX + 19 + _hostIPCursorPos, y, '▏', ConsoleColor.White);
        }
        
        // Connect button
        {
            int y = boxY + 10;
            bool selected = _setupSelection == 3;
            string label = selected ? "▸ " : "  ";
            ConsoleColor color = selected ? ConsoleColor.Cyan : ConsoleColor.Gray;
            _renderer.DrawString(boxX + 3, y, $"{label}▶ Connect", color);
        }
        
        // Show error
        if (!string.IsNullOrEmpty(_lobbyError))
        {
            _renderer.DrawString(boxX + 3, boxY + 11, _lobbyError, ConsoleColor.Red);
        }
        
        _renderer.DrawString(boxX + 2, boxY + boxH - 2, "Space: Edit/Connect  ←→: Color  Esc: Back", ConsoleColor.DarkGray);
    }
    
    private void DrawLobbyScreen(int cx, int cy)
    {
        int boxW = 44;
        int boxH = 16;
        int boxX = cx - boxW / 2;
        int boxY = cy - boxH / 2;
        
        for (int y = boxY - 1; y < boxY + boxH + 1; y++)
            for (int x = boxX - 2; x < boxX + boxW + 2; x++)
                if (x >= 0 && x < _renderer.Width && y >= 0 && y < _renderer.Height)
                    _renderer.SetCell(x, y, ' ', ConsoleColor.Black, ConsoleColor.Black);
        
        _renderer.DrawString(boxX, boxY, $"╔{new string('═', boxW - 2)}╗", _isHost ? ConsoleColor.Yellow : ConsoleColor.Cyan);
        for (int i = 0; i < boxH - 2; i++)
        {
            _renderer.DrawString(boxX, boxY + 1 + i, "║", _isHost ? ConsoleColor.Yellow : ConsoleColor.Cyan);
            _renderer.DrawString(boxX + boxW - 1, boxY + 1 + i, "║", _isHost ? ConsoleColor.Yellow : ConsoleColor.Cyan);
        }
        _renderer.DrawString(boxX, boxY + boxH - 1, $"╚{new string('═', boxW - 2)}╝", _isHost ? ConsoleColor.Yellow : ConsoleColor.Cyan);
        
        string title = _isHost ? $"HOST LOBBY (Port: {_port})" : "LOBBY";
        _renderer.DrawString(boxX + 2, boxY + 1, title, _isHost ? ConsoleColor.Yellow : ConsoleColor.Cyan);
        
        // IP info for host
        if (_isHost)
        {
            string localIP = GetLocalIP();
            _renderer.DrawString(boxX + 2, boxY + 2, $"Your IP: {localIP}", ConsoleColor.Gray);
        }
        
        _renderer.DrawString(boxX, boxY + 3, $"╠{new string('═', boxW - 2)}╣", _isHost ? ConsoleColor.Yellow : ConsoleColor.Cyan);
        
        // Player list
        _renderer.DrawString(boxX + 3, boxY + 4, "Players:", ConsoleColor.White);
        
        if (_lobbyState != null)
        {
            string[] colorNames = { "Yellow", "Cyan", "Green", "Magenta" };
            ConsoleColor[] colorValues = { ConsoleColor.Yellow, ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Magenta };
            
            for (int i = 0; i < _lobbyState.Players.Count; i++)
            {
                var player = _lobbyState.Players[i];
                int y = boxY + 6 + i;
                if (y >= boxY + boxH - 3) break;
                
                string ready = player.Ready ? "✓ READY" : "  ...";
                ConsoleColor nameColor = colorValues[(int)player.Color];
                
                _renderer.DrawString(boxX + 3, y, $"  ▲ {player.Nickname}", nameColor);
                _renderer.DrawString(boxX + 22, y, ready, player.Ready ? ConsoleColor.Green : ConsoleColor.DarkGray);
            }
            
            // Waiting for more players
            if (_lobbyState.Players.Count < NetServer.MaxPlayers)
            {
                int waitY = boxY + 6 + _lobbyState.Players.Count;
                _renderer.DrawString(boxX + 3, waitY, "  ... waiting for players ...", ConsoleColor.DarkGray);
            }
        }
        else
        {
            _renderer.DrawString(boxX + 3, boxY + 6, "  Waiting for server...", ConsoleColor.DarkGray);
        }
        
        // Error
        if (!string.IsNullOrEmpty(_lobbyError))
        {
            _renderer.DrawString(boxX + 3, boxY + boxH - 4, _lobbyError, ConsoleColor.Red);
        }
        
        // Controls
        if (_isHost)
        {
            _renderer.DrawString(boxX + 2, boxY + boxH - 2,
                "Space: Ready/Start  Esc: Cancel", ConsoleColor.DarkGray);
        }
        else
        {
            _renderer.DrawString(boxX + 2, boxY + boxH - 2,
                "Space: Toggle Ready  Esc: Leave", ConsoleColor.DarkGray);
        }
    }
    
    private static string GetLocalIP()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch { }
        return "?.?.?.?";
    }
    
    private void DrawSettingsMenu(int cx, int cy)
    {
        int boxW = 46;
        int boxH = 11;
        int boxX = cx - boxW / 2;
        int boxY = cy - boxH / 2;
        
        for (int y = boxY - 1; y < boxY + boxH + 1; y++)
            for (int x = boxX - 2; x < boxX + boxW + 2; x++)
                if (x >= 0 && x < _renderer.Width && y >= 0 && y < _renderer.Height)
                    _renderer.SetCell(x, y, ' ', ConsoleColor.Black, ConsoleColor.Black);
        
        _renderer.DrawString(boxX, boxY, $"╔{new string('═', boxW - 2)}╗", ConsoleColor.Magenta);
        for (int i = 0; i < boxH - 2; i++)
        {
            _renderer.DrawString(boxX, boxY + 1 + i, "║", ConsoleColor.Magenta);
            _renderer.DrawString(boxX + boxW - 1, boxY + 1 + i, "║", ConsoleColor.Magenta);
        }
        _renderer.DrawString(boxX, boxY + boxH - 1, $"╚{new string('═', boxW - 2)}╝", ConsoleColor.Magenta);
        
        _renderer.DrawString(boxX + 2, boxY + 1, "SETTINGS", ConsoleColor.Magenta);
        _renderer.DrawString(boxX, boxY + 2, $"╠{new string('═', boxW - 2)}╣", ConsoleColor.Magenta);
        
        {
            int y = boxY + 4;
            bool selected = _selection == 0;
            int barW = 15;
            int fill = (_settings.TickRate - 10) * barW / 190;
            string label = selected ? "▸ Game Speed:" : "  Game Speed:";
            _renderer.DrawString(boxX + 3, y, label, selected ? ConsoleColor.White : ConsoleColor.Gray);
            
            int barX = boxX + 18;
            _renderer.DrawString(barX, y, "[", ConsoleColor.DarkGray);
            _renderer.DrawString(barX + 1, y, new string('█', fill), ConsoleColor.Magenta);
            _renderer.DrawString(barX + 1 + fill, y, new string('░', barW - fill), ConsoleColor.DarkGray);
            _renderer.DrawString(barX + 1 + barW, y, "]", ConsoleColor.DarkGray);
            _renderer.DrawString(barX + barW + 3, y, $"{_settings.TickRate}ms", ConsoleColor.Gray);
        }
        
        {
            int y = boxY + 6;
            bool selected = _selection == 1;
            string label = selected ? "▸ Enemies:    " : "  Enemies:    ";
            _renderer.DrawString(boxX + 3, y, label, selected ? ConsoleColor.White : ConsoleColor.Gray);
            string state = _settings.EnemiesEnabled ? "ON " : "OFF";
            ConsoleColor stateColor = _settings.EnemiesEnabled ? ConsoleColor.Green : ConsoleColor.Red;
            _renderer.DrawString(boxX + 18, y, state, stateColor);
        }
        
        {
            int y = boxY + 8;
            bool selected = _selection == 2;
            _renderer.DrawString(boxX + 3, y, selected ? "▸ Reset to Defaults" : "  Reset to Defaults", 
                selected ? ConsoleColor.White : ConsoleColor.Gray);
        }
        
        _renderer.DrawString(boxX + 2, boxY + boxH - 2, "←→ Adjust  Space: Toggle  Esc: Back", ConsoleColor.DarkGray);
    }
    
    private void DrawAboutScreen(int cx, int cy)
    {
        int boxW = 52;
        int boxH = 18;
        int boxX = cx - boxW / 2;
        int boxY = cy - boxH / 2;
        
        for (int y = boxY - 1; y < boxY + boxH + 1; y++)
            for (int x = boxX - 2; x < boxX + boxW + 2; x++)
                if (x >= 0 && x < _renderer.Width && y >= 0 && y < _renderer.Height)
                    _renderer.SetCell(x, y, ' ', ConsoleColor.Black, ConsoleColor.Black);
        
        _renderer.DrawString(boxX, boxY, $"╔{new string('═', boxW - 2)}╗", ConsoleColor.Yellow);
        for (int i = 0; i < boxH - 2; i++)
        {
            _renderer.DrawString(boxX, boxY + 1 + i, "║", ConsoleColor.Yellow);
            _renderer.DrawString(boxX + boxW - 1, boxY + 1 + i, "║", ConsoleColor.Yellow);
        }
        _renderer.DrawString(boxX, boxY + boxH - 1, $"╚{new string('═', boxW - 2)}╝", ConsoleColor.Yellow);
        
        var lines = new (string Text, ConsoleColor Color)[]
        {
            ("ASCIIFACTORY v1.0", ConsoleColor.Cyan),
            ("", ConsoleColor.White),
            ("A Factorio-inspired ASCII factory game", ConsoleColor.White),
            ("Built with C# / .NET 10", ConsoleColor.Gray),
            ("", ConsoleColor.White),
            ("You crash-land on an alien planet.", ConsoleColor.White),
            ("Your mission: strip-mine the entire world,", ConsoleColor.White),
            ("build a massive automated factory,", ConsoleColor.White),
            ("and construct THE COMPUTER 9000™", ConsoleColor.Yellow),
            ("", ConsoleColor.White),
            ("...just to [REDACTED]", ConsoleColor.Green),
            ("", ConsoleColor.White),
            ("The same thing Program.cs already did", ConsoleColor.DarkGray),
            ("in 20[REDACTED] ERROR - CLEARANCE TOO LOW.", ConsoleColor.DarkGray),
            ("", ConsoleColor.White),
            ("Press any key to go back", ConsoleColor.DarkGray),
        };
        
        for (int i = 0; i < lines.Length; i++)
        {
            int x = boxX + 3;
            int y = boxY + 1 + i;
            if (y < boxY + boxH - 1)
                _renderer.DrawString(x, y, lines[i].Text.PadRight(boxW - 6), lines[i].Color);
        }
    }
    
    private void DrawMarquee()
    {
        int w = _renderer.Width;
        int h = _renderer.Height;
        
        string marquee = "★ Building THE COMPUTER 9000™ to [REDACTED] since 2026! ★" +
                         "   ●●●   " +
                         "\"I tried to explain my factory to my wife. She left.\" - A Player" +
                         "   ●●●   " +
                         "Now with SOFTWARE BUGS! (the enemy kind, not the code kind) (okay, maybe both)" +
                         "   ●●●   " +
                         "THE COMPUTER 9000™: \"I'm sorry, Dave. I can't [REDACTED] that.\"" +
                         "   ●●●   " +
                         "\"It's not a bug, it's a feature!\" - Every developer moments before catastrophe" +
                         "   ●●●   " +
                         "♫ Elevator music plays softly while your factory burns ♫" +
                         "   ●●●   " +
                         "Bug population: 7 billion and rising. Good luck with that." +
                         "   ●●●   " +
                         "\"I just need one more assembler\" - Famous last words" +
                         "   ●●●   " +
                         "This planet was beautiful once. Keywords: was. once." +
                         "   ●●●   " +
                         "THE COMPUTER 9000™ now powered by 100% renewable bug energy!" +
                         "   ●●●   " +
                         "\"My factory is a perfectly organized chaos, thank you very much.\"" +
                         "   ●●●   " +
                         "Fun fact: The conveyor belt was invented by someone too lazy to walk. We respect that." +
                         "   ●●●   " +
                         "Pollution index: ☠. That's not a measurement. That's a warning." +
                         "   ●●●   " +
                         "THE COMPUTER 9000™ user manual: 404 - Page not found. Good luck!" +
                         "   ●●●   " +
                         "Warning: THE COMPUTER 9000™ may become self-aware. If it asks for your password, decline." +
                         "   ●●●   " +
                         "\"There's no place like 127.0.0.1\" - THE COMPUTER 9000™, homesick" +
                         "   ●●●   " +
                         "Fun fact: Segfaults are just the computer's way of saying 'I need a hug.'" +
                         "   ●●●   " +
                         "THE COMPUTER 9000™ boot screen: 'PRESS ANY KEY TO REGRET YOUR LIFE CHOICES'" +
                         "   ●●●   " +
                         "Do not taunt the Kernel Panics. They know where you live. They ARE where you live." +
                         "   ●●●   " +
                         "\"I have not failed. I've just found 10,000 ways not to build a factory.\" - Einstein, probably" +
                         "   ●●●   " +
                         "Sponsored by Copper Ore™: 'Disappointed since the Bronze Age.'" +
                         "   ●●●   " +
                         "THE COMPUTER 9000™ does not multitask. It dramatically switches tasks." +
                         "   ●●●   " +
                         "Bug tip: Glitches can clip through walls. Your factory is NOT safe. It never was." +
                         "   ●●●   " +
                         "\"If you think your factory is big enough, it isn't.\" - Sun Tzu, The Art of Conveyor Belts" +
                         "   ●●●   " +
                         "Coal: the fossil fuel that's also a fossil fool. Ha. Ha. We'll be here all week." +
                         "   ●●●   " +
                         "THE COMPUTER 9000™ on love: 'Error: undefined reference to heart.'" +
                         "   ●●●   " +
                         "Real engineers don't use debuggers. They use printf and prayer." +
                         "   ●●●   " +
                         "Memory Leak spotted! Just kidding. Or am I? (I am. Or... am I?)" +
                         "   ●●●   " +
                         "THE COMPUTER 9000™ fun fact: It takes 3.5 seconds to boot and a lifetime to understand why." +
                         "   ●●●   " +
                         "\"To belay is human, to conveyor divine.\" - Alexander the Great (probably)" +
                         "   ●●●   " +
                         "Your strip mining operation is so thorough, even the MOLE PEOPLE are impressed." +
                         "   ●●●   " +
                         "THE COMPUTER 9000™ certified organic, free-range, cage-free computing." +
                         "   ●●●   " +
                         "♫ Another One Bites the Dust ♫ - Your machines, probably" +
                         "   ●●●   " +
                         "\"I'm not addicted to conveyor belts. I can stop anytime. I just don't want to.\"" +
                         "   ●●●   " +
                         "The planet called. It wants its ecosystem back. We said no." +
                         "   ●●●   " +
                         "THE COMPUTER 9000™ reviews: ★☆☆☆☆ 'It just said Hello World and cried.' - User #404" +
                         "   ●●●   " +
                         "Null Pointer walked into a bar. The bar was null. There was no bar. There was no pointer. There is only void." +
                         "   ●●●   " +
                         "Spelunking tip: always carry a crafting table. You never know when inspiration (or a bug) strikes." +
                         "   ●●●   " +
                         "THE COMPUTER 9000™ would like to remind you: it's not slow, it's 'thoughtfully paced.'";
        
        int marqueeLen = marquee.Length;
        int scrollPos = (_tick / 2) % marqueeLen;
        
        int y = h - 1;
        
        for (int x = 0; x < w; x++)
            _renderer.SetCell(x, y, ' ', ConsoleColor.Black, ConsoleColor.DarkGray);
        
        for (int x = 0; x < w; x++)
        {
            int charIdx = (scrollPos + x) % marqueeLen;
            _renderer.SetCell(x, y, marquee[charIdx], ConsoleColor.DarkCyan, ConsoleColor.DarkGray);
        }
    }
    
    private static ConsoleColor DimColor(ConsoleColor color) => color switch
    {
        ConsoleColor.White => ConsoleColor.DarkGray,
        ConsoleColor.Yellow => ConsoleColor.DarkYellow,
        ConsoleColor.Green => ConsoleColor.DarkGreen,
        ConsoleColor.Cyan => ConsoleColor.DarkCyan,
        ConsoleColor.Blue => ConsoleColor.DarkBlue,
        ConsoleColor.Red => ConsoleColor.DarkRed,
        ConsoleColor.Magenta => ConsoleColor.DarkMagenta,
        ConsoleColor.Gray => ConsoleColor.DarkGray,
        _ => ConsoleColor.Black,
    };
}