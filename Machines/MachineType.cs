namespace Asciifactory.Machines;

/// <summary>
/// All machine types that can be placed in the world.
/// </summary>
public enum MachineType
{
    Miner,
    ConveyorBelt,
    Furnace,
    Assembler,
    StorageChest,
    PowerGenerator,
    Lab,
    Refinery,
    FirewallTurret,
    Wire,
    BiomassBurner,
    CoalGenerator,
    NuclearReactor,
}

/// <summary>
/// Port type: Item or Power.
/// </summary>
public enum PortType
{
    Item,
    Power,
}

/// <summary>
/// A connection port on a machine tile. Defined in local coordinates relative to machine origin.
/// DirOffset is which face of the tile the port is on (0=up, 1=right, 2=down, 3=left).
/// </summary>
public readonly record struct PortDef(int LocalX, int LocalY, PortType Type, int DirOffset);

/// <summary>
/// Display info and size for machine types.
/// </summary>
public static class MachineTypeInfo
{
    private readonly record struct Info(char Symbol, ConsoleColor Color, string Name, ConsoleColor DirColor, int Width, int Height);
    
    private static readonly Dictionary<MachineType, Info> MachineInfos = new()
    {
        { MachineType.Miner,         new('M', ConsoleColor.DarkYellow, "Miner",           ConsoleColor.Yellow,   2, 2) },
        { MachineType.ConveyorBelt,  new('›', ConsoleColor.DarkGray,   "Conveyor Belt",   ConsoleColor.Gray,     1, 1) },
        { MachineType.Furnace,       new('F', ConsoleColor.DarkRed,    "Furnace",         ConsoleColor.Red,      3, 2) },
        { MachineType.Assembler,     new('A', ConsoleColor.DarkGreen,  "Assembler",       ConsoleColor.Green,    3, 3) },
        { MachineType.StorageChest,  new('□', ConsoleColor.DarkCyan,   "Storage Chest",   ConsoleColor.Cyan,     1, 1) },
        { MachineType.PowerGenerator,new('G', ConsoleColor.DarkYellow, "Power Generator", ConsoleColor.Yellow,   1, 1) },
        { MachineType.Lab,           new('L', ConsoleColor.DarkMagenta,"Lab",             ConsoleColor.Magenta,  3, 3) },
        { MachineType.Refinery,      new('R', ConsoleColor.DarkBlue,   "Refinery",        ConsoleColor.Blue,     3, 3) },
        { MachineType.FirewallTurret,new('T', ConsoleColor.DarkRed,    "Firewall Turret", ConsoleColor.Red,     1, 1) },
        { MachineType.Wire,          new('+', ConsoleColor.Cyan,       "Power Wire",      ConsoleColor.Cyan,     1, 1) },
        { MachineType.BiomassBurner, new('b', ConsoleColor.DarkGreen,  "Biomass Burner",  ConsoleColor.Green,    3, 3) },
        { MachineType.CoalGenerator, new('C', ConsoleColor.DarkGray,   "Coal Generator",  ConsoleColor.Gray,     2, 2) },
        { MachineType.NuclearReactor,new('☢', ConsoleColor.DarkGreen,  "Nuclear Reactor", ConsoleColor.Green,    3, 3) },
    };
    
    public static char GetSymbol(MachineType type) => MachineInfos[type].Symbol;
    public static ConsoleColor GetColor(MachineType type) => MachineInfos[type].Color;
    public static string GetName(MachineType type) => MachineInfos[type].Name;
    public static ConsoleColor GetDirColor(MachineType type) => MachineInfos[type].DirColor;
    public static int GetWidth(MachineType type) => MachineInfos[type].Width;
    public static int GetHeight(MachineType type) => MachineInfos[type].Height;
    
    /// <summary>
    /// Returns the base (unrotated) port definitions for a machine type.
    /// Ports are in local coordinates relative to the origin (top-left) tile.
    /// DirOffset: 0=up, 1=right, 2=down, 3=left — which face of the tile the port is on.
    /// </summary>
    public static PortDef[] GetBasePorts(MachineType type) => type switch
    {
        // Miner: 2x2, 1 item output port at bottom-right, right face
        //   ┌─┐
        //   │M│
        //   │M│► output
        //   └─┘
        MachineType.Miner => new[] { new PortDef(1, 1, PortType.Item, 1) },
        
        // Belt: 1 item in (back), 1 item out (front). DirOffset follows direction.
        MachineType.ConveyorBelt => new[] { new PortDef(0, 0, PortType.Item, 3), new PortDef(0, 0, PortType.Item, 1) },
        
        // Furnace 3x2: 2 item inputs on left side, 1 item output on right center
        //   [0,0][1,0][2,0]    inputs: (0,0) face-left, (0,1) face-left
        //   [0,1][1,1][2,1]    output: (2,1) face-right
        MachineType.Furnace => new[]
        {
            new PortDef(0, 0, PortType.Item, 3),  // top-left, left face = fuel in
            new PortDef(0, 1, PortType.Item, 3),  // bottom-left, left face = ore in
            new PortDef(2, 1, PortType.Item, 1),  // bottom-right, right face = output
        },
        
        // Assembler 3x3: 2 item inputs on left side, 1 item output on right center
        //   [0,0][1,0][2,0]    inputs: (0,0) face-left, (0,2) face-left
        //   [0,1][1,1][2,1]    output: (2,1) face-right
        //   [0,2][1,2][2,2]
        MachineType.Assembler => new[]
        {
            new PortDef(0, 0, PortType.Item, 3),  // top-left, left face
            new PortDef(0, 2, PortType.Item, 3),  // bottom-left, left face
            new PortDef(2, 1, PortType.Item, 1),  // mid-right, right face = output
        },
        
        // Lab 3x3: 2 item inputs on left side, 1 item output on right center
        MachineType.Lab => new[]
        {
            new PortDef(0, 0, PortType.Item, 3),  // top-left, left face
            new PortDef(0, 2, PortType.Item, 3),  // bottom-left, left face
            new PortDef(2, 1, PortType.Item, 1),  // mid-right, right face = output
        },
        
        // Refinery 3x3: 2 item inputs on left/top, 1 item output on right center
        MachineType.Refinery => new[]
        {
            new PortDef(0, 1, PortType.Item, 3),  // mid-left, left face
            new PortDef(1, 0, PortType.Item, 0),  // top-center, top face
            new PortDef(2, 1, PortType.Item, 1),  // mid-right, right face = output
        },
        
        // Storage Chest: item in from all sides, item out from front
        MachineType.StorageChest => new[] { new PortDef(0, 0, PortType.Item, 3), new PortDef(0, 0, PortType.Item, 1) },
        
        // Wire: power only, no item ports
        MachineType.Wire => Array.Empty<PortDef>(),
        
        // BiomassBurner 3x3: 1 item input (fuel) on mid-left, left face
        //   ┌─┐
        //   │b│
        // ◄ b│  fuel in
        //   │b│
        //   └─┘
        MachineType.BiomassBurner => new[]
        {
            new PortDef(0, 1, PortType.Item, 3),  // mid-left, left face = fuel in
        },
        
        // PowerGenerator = BiomassBurner equivalent
        MachineType.PowerGenerator => new[] { new PortDef(0, 0, PortType.Item, 3) },
        
        // CoalGenerator 2x2: 1 item input (coal) on left center
        //   [0,0][1,0]    input: (0,0) face-left
        //   [0,1][1,1]
        MachineType.CoalGenerator => new[]
        {
            new PortDef(0, 0, PortType.Item, 3),  // top-left, left face = coal in
        },
        
        // NuclearReactor 3x3: 1 item input (uranium) on top center
        //   [0,0][1,0][2,0]    input: (1,0) face-up
        //   [0,1][1,1][2,1]
        //   [0,2][1,2][2,2]
        MachineType.NuclearReactor => new[]
        {
            new PortDef(1, 0, PortType.Item, 0),  // top-center, top face = uranium in
        },
        
        // FirewallTurret: 1 item input (ammo)
        MachineType.FirewallTurret => new[] { new PortDef(0, 0, PortType.Item, 3) },
        
        _ => Array.Empty<PortDef>()
    };
    
    /// <summary>
    /// Checks if a machine type is multi-tile (occupies more than 1 tile).
    /// </summary>
    public static bool IsMultiTile(MachineType type) => GetWidth(type) > 1 || GetHeight(type) > 1;
    
    /// <summary>
    /// Gets the power draw (MW) for a machine type. Generators and wires draw 0.
    /// </summary>
    public static int GetPowerDraw(MachineType type) => type switch
    {
        MachineType.Miner => 2,
        MachineType.ConveyorBelt => 0,
        MachineType.Furnace => 4,
        MachineType.Assembler => 5,
        MachineType.StorageChest => 0,
        MachineType.Lab => 3,
        MachineType.Refinery => 6,
        MachineType.FirewallTurret => 3,
        MachineType.Wire => 0,
        MachineType.BiomassBurner => 0,
        MachineType.CoalGenerator => 0,
        MachineType.NuclearReactor => 0,
        MachineType.PowerGenerator => 0,
        _ => 0
    };
    
    /// <summary>
    /// Gets the power output (MW) for a generator type. Non-generators return 0.
    /// </summary>
    public static int GetPowerOutput(MachineType type) => type switch
    {
        MachineType.BiomassBurner => 30,
        MachineType.CoalGenerator => 75,
        MachineType.NuclearReactor => 600,
        MachineType.PowerGenerator => 5,
        _ => 0
    };
    
    /// <summary>
    /// Whether this machine type is a power generator (produces electricity).
    /// </summary>
    public static bool IsGenerator(MachineType type) => type switch
    {
        MachineType.PowerGenerator => true,
        MachineType.BiomassBurner => true,
        MachineType.CoalGenerator => true,
        MachineType.NuclearReactor => true,
        _ => false
    };
    
    /// <summary>
    /// Whether this machine type is a power wire (conducts electricity).
    /// </summary>
    public static bool IsWire(MachineType type) => type == MachineType.Wire;
    
    /// <summary>
    /// Gets the belt symbol based on direction.
    /// </summary>
    public static char GetBeltSymbol(int dx, int dy) => (dx, dy) switch
    {
        (1, 0)  => '›',  // right
        (-1, 0) => '‹',  // left
        (0, -1) => 'ˆ',  // up
        (0, 1)  => 'v',  // down
        _       => '·',
    };
}

/// <summary>
/// Cardinal directions for machine facing.
/// </summary>
public enum Direction
{
    Up,    // 0, -1
    Down,  // 0, 1
    Left,  // -1, 0
    Right, // 1, 0
}

public static class DirectionExtensions
{
    public static (int dx, int dy) ToDelta(this Direction dir) => dir switch
    {
        Direction.Up    => (0, -1),
        Direction.Down  => (0, 1),
        Direction.Left  => (-1, 0),
        Direction.Right => (1, 0),
        _ => (0, 1)
    };
    
    public static Direction RotateCW(this Direction dir) => dir switch
    {
        Direction.Up    => Direction.Right,
        Direction.Right => Direction.Down,
        Direction.Down  => Direction.Left,
        Direction.Left  => Direction.Up,
        _ => Direction.Up
    };
    
    /// <summary>
    /// Number of 90° clockwise rotations from the default (Right) facing.
    /// </summary>
    public static int RotationCount(this Direction dir) => dir switch
    {
        Direction.Right => 0,
        Direction.Down  => 1,
        Direction.Left  => 2,
        Direction.Up    => 3,
        _ => 0
    };
    
    /// <summary>
    /// Rotates a local offset (dx, dy) by the given number of 90° CW rotations.
    /// Used to transform port positions and tile offsets from base to world coordinates.
    /// </summary>
    public static (int dx, int dy) RotateOffset(int dx, int dy, int rotations)
    {
        for (int i = 0; i < rotations % 4; i++)
        {
            (dx, dy) = (-dy, dx);
        }
        return (dx, dy);
    }
    
    /// <summary>
    /// Rotates a facing direction offset (0=up, 1=right, 2=down, 3=left) by the given number of 90° CW rotations.
    /// </summary>
    public static int RotateDirOffset(int dirOffset, int rotations) => (dirOffset + rotations) % 4;
}