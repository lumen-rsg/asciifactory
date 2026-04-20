using Asciifactory.Items;

namespace Asciifactory.Machines;

/// <summary>
/// Visual info for a single tile of a multi-tile machine.
/// </summary>
public readonly record struct TileVisual(char Char, ConsoleColor ForeColor, ConsoleColor BackColor);

/// <summary>
/// Abstract base class for all machines in the world.
/// Handles position, direction, internal inventory, power state, and processing.
/// Supports multi-tile machines with multiple I/O ports.
/// </summary>
public abstract class MachineBase
{
    public int X { get; }
    public int Y { get; }
    public Direction Direction { get; set; }
    public MachineType Type { get; }
    public bool IsPowered { get; set; } = true;
    
    /// <summary>Machine width in tiles (base, unrotated).</summary>
    public int Width => MachineTypeInfo.GetWidth(Type);
    
    /// <summary>Machine height in tiles (base, unrotated).</summary>
    public int Height => MachineTypeInfo.GetHeight(Type);
    
    /// <summary>Effective width after rotation.</summary>
    public int RotatedWidth => Direction is Direction.Up or Direction.Down ? Height : Width;
    
    /// <summary>Effective height after rotation.</summary>
    public int RotatedHeight => Direction is Direction.Up or Direction.Down ? Width : Height;
    
    /// <summary>
    /// Power draw in MW. Override in derived classes for machine-specific draw.
    /// </summary>
    public virtual int PowerDraw => MachineTypeInfo.GetPowerDraw(Type);
    
    /// <summary>
    /// Power output in MW. Override in generator classes.
    /// </summary>
    public virtual int PowerOutput => MachineTypeInfo.GetPowerOutput(Type);
    
    /// <summary>
    /// Whether this machine's fuse has tripped due to power grid overload.
    /// Player must walk to generator and press E to reset.
    /// </summary>
    public bool FuseTripped { get; set; }
    
    /// <summary>
    /// Internal buffer inventory (input + output). Machines have small internal storage.
    /// </summary>
    public Inventory InternalInventory { get; }
    
    /// <summary>
    /// Processing progress (0 to 1).
    /// </summary>
    public float Progress { get; protected set; }
    
    /// <summary>
    /// Whether this machine is currently processing a recipe.
    /// </summary>
    public bool IsProcessing => Progress > 0;
    
    /// <summary>
    /// Machine health. When reaches 0, machine is destroyed.
    /// </summary>
    public int Health { get; set; } = 20;
    public int MaxHealth { get; } = 20;
    public bool IsDestroyed => Health <= 0;
    
    /// <summary>
    /// Damage flash timer for visual feedback.
    /// </summary>
    public int DamageFlash { get; set; }
    
    // Cached rotated port data
    private (int wx, int wy, PortType type, int worldDir)[]? _rotatedPorts;
    
    protected MachineBase(MachineType type, int x, int y, Direction direction, int internalSlots = 4)
    {
        Type = type;
        X = x;
        Y = y;
        Direction = direction;
        InternalInventory = new Inventory(internalSlots);
        Progress = 0;
    }
    
    /// <summary>
    /// Returns all world tiles occupied by this machine.
    /// For multi-tile machines, includes the origin (X,Y) and all additional tiles.
    /// </summary>
    public IEnumerable<(int x, int y)> GetOccupiedCells()
    {
        int rotations = Direction.RotationCount();
        for (int ly = 0; ly < Height; ly++)
        {
            for (int lx = 0; lx < Width; lx++)
            {
                var (rdx, rdy) = DirectionExtensions.RotateOffset(lx, ly, rotations);
                yield return (X + rdx, Y + rdy);
            }
        }
    }
    
    /// <summary>
    /// Returns the visual character for each occupied tile.
    /// For multi-tile machines (W > 1 or H > 1), draws a box-drawing outline.
    /// Override in derived classes for custom multi-tile visuals.
    /// </summary>
    public virtual IEnumerable<((int x, int y) pos, TileVisual visual)> GetTileVisuals()
    {
        char defaultChar = GetDisplayChar();
        ConsoleColor defaultColor = GetDisplayColor();
        ConsoleColor bg = IsProcessing ? ConsoleColor.DarkGray : ConsoleColor.Black;
        
        int rw = RotatedWidth;
        int rh = RotatedHeight;
        
        // Single-tile machine: just show the character
        if (rw <= 1 && rh <= 1)
        {
            yield return ((X, Y), new TileVisual(defaultChar, defaultColor, bg));
            yield break;
        }
        
        // Multi-tile: draw box-drawing outline with interior label character
        int rotations = Direction.RotationCount();
        var cells = GetOccupiedCells().ToList();
        var cellSet = cells.ToHashSet();
        
        // Build local coordinate map: for each rotated offset, determine edge type
        foreach (var (wx, wy) in cells)
        {
            int localX = wx - X;
            int localY = wy - Y;
            
            bool isLeft = !cellSet.Contains((wx - 1, wy));
            bool isRight = !cellSet.Contains((wx + 1, wy));
            bool isTop = !cellSet.Contains((wx, wy - 1));
            bool isBottom = !cellSet.Contains((wx, wy + 1));
            
            char ch;
            if (isTop && isLeft) ch = '┌';
            else if (isTop && isRight) ch = '┐';
            else if (isBottom && isLeft) ch = '└';
            else if (isBottom && isRight) ch = '┘';
            else if (isTop || isBottom) ch = '─';
            else if (isLeft || isRight) ch = '│';
            else ch = defaultChar; // Interior tile
            
            yield return ((wx, wy), new TileVisual(ch, defaultColor, bg));
        }
    }
    
    /// <summary>
    /// Gets all rotated ports in world coordinates.
    /// Each port includes its world position and the facing direction of the port.
    /// </summary>
    public IEnumerable<(int wx, int wy, PortType type, int worldDir)> GetRotatedPorts()
    {
        if (_rotatedPorts != null) return _rotatedPorts;
        
        var basePorts = MachineTypeInfo.GetBasePorts(Type);
        int rotations = Direction.RotationCount();
        
        var result = new List<(int wx, int wy, PortType type, int worldDir)>();
        foreach (var port in basePorts)
        {
            var (rdx, rdy) = DirectionExtensions.RotateOffset(port.LocalX, port.LocalY, rotations);
            int worldDir = DirectionExtensions.RotateDirOffset(port.DirOffset, rotations);
            result.Add((X + rdx, Y + rdy, port.Type, worldDir));
        }
        
        _rotatedPorts = result.ToArray();
        return _rotatedPorts;
    }
    
    /// <summary>
    /// Returns item input ports in world coordinates with their facing direction.
    /// </summary>
    public IEnumerable<(int wx, int wy, int worldDir)> GetItemInputPorts()
    {
        int i = 0;
        foreach (var port in GetRotatedPorts())
        {
            // For machines with multiple ports, the last item port is typically the output
            // We need to determine which ports are inputs vs outputs
            if (port.type == PortType.Item && IsInputPort(i))
            {
                yield return (port.wx, port.wy, port.worldDir);
            }
            i++;
        }
    }
    
    /// <summary>
    /// Returns the item output port in world coordinates, if any.
    /// </summary>
    public (int wx, int wy, int worldDir)? GetItemOutputPort()
    {
        int i = 0;
        foreach (var port in GetRotatedPorts())
        {
            if (port.type == PortType.Item && !IsInputPort(i))
            {
                return (port.wx, port.wy, port.worldDir);
            }
            i++;
        }
        return null;
    }
    
    /// <summary>
    /// Determines if a port index is an input (true) or output (false).
    /// By convention: the last Item port is the output, all others are inputs.
    /// Override for custom behavior.
    /// </summary>
    protected virtual bool IsInputPort(int portIndex)
    {
        var ports = MachineTypeInfo.GetBasePorts(Type);
        int lastItemIndex = -1;
        for (int i = 0; i < ports.Length; i++)
        {
            if (ports[i].Type == PortType.Item) lastItemIndex = i;
        }
        return portIndex < lastItemIndex;
    }
    
    /// <summary>
    /// Gets the output position (where items are sent) based on the output port.
    /// Falls back to direction-based output for single-tile machines.
    /// </summary>
    public (int x, int y) GetOutputPos()
    {
        var outputPort = GetItemOutputPort();
        if (outputPort.HasValue)
        {
            return PortDirToOffset(outputPort.Value.wx, outputPort.Value.wy, outputPort.Value.worldDir);
        }
        // Fallback: use facing direction
        var (dx, dy) = Direction.ToDelta();
        return (X + dx, Y + dy);
    }
    
    /// <summary>
    /// Gets the input position (where items come from, opposite of output port).
    /// </summary>
    public (int x, int y) GetInputPos()
    {
        var (dx, dy) = Direction.ToDelta();
        return (X - dx, Y - dy);
    }
    
    /// <summary>
    /// Converts a port position + direction to the adjacent tile in that direction.
    /// </summary>
    private static (int x, int y) PortDirToOffset(int px, int py, int dir) => dir switch
    {
        0 => (px, py - 1), // up
        1 => (px + 1, py), // right
        2 => (px, py + 1), // down
        3 => (px - 1, py), // left
        _ => (px, py)
    };
    
    /// <summary>
    /// Checks if a given world position is an item input port for this machine.
    /// </summary>
    public bool IsItemInputAt(int wx, int wy)
    {
        foreach (var port in GetItemInputPorts())
        {
            if (port.wx == wx && port.wy == wy) return true;
        }
        return false;
    }
    
    /// <summary>
    /// Checks if a given world position is the item output port for this machine.
    /// </summary>
    public bool IsItemOutputAt(int wx, int wy)
    {
        var outputPort = GetItemOutputPort();
        return outputPort.HasValue && outputPort.Value.wx == wx && outputPort.Value.wy == wy;
    }
    
    /// <summary>
    /// Clears cached port data (call after rotation changes).
    /// </summary>
    public void InvalidateCache() => _rotatedPorts = null;
    
    /// <summary>
    /// Tick this machine. Returns true if it produced output this tick.
    /// </summary>
    public abstract bool Tick(Func<int, int, MachineBase?> getMachineAt);
    
    /// <summary>
    /// Tries to send items from this machine's output to the adjacent machine.
    /// Uses the output port position for multi-tile machines.
    /// </summary>
    protected bool TryOutputItem(ItemId itemId, int count, Func<int, int, MachineBase?> getMachineAt)
    {
        var (ox, oy) = GetOutputPos();
        var target = getMachineAt(ox, oy);
        
        if (target == null) return false;
        
        int remaining = target.AcceptItem(itemId, count);
        return remaining < count;
    }
    
    /// <summary>
    /// Accept items from another machine. Returns the number that couldn't be accepted.
    /// </summary>
    public virtual int AcceptItem(ItemId itemId, int count)
    {
        return InternalInventory.AddItem(itemId, count);
    }
    
    /// <summary>
    /// Returns the visual character for this machine (can vary based on state).
    /// </summary>
    public virtual char GetDisplayChar() => MachineTypeInfo.GetSymbol(Type);
    
    /// <summary>
    /// Returns the display color for this machine.
    /// </summary>
    public virtual ConsoleColor GetDisplayColor()
    {
        if (DamageFlash > 0) return ConsoleColor.White;
        if (!IsPowered) return ConsoleColor.DarkGray;
        return IsProcessing ? MachineTypeInfo.GetDirColor(Type) : MachineTypeInfo.GetColor(Type);
    }
    
    /// <summary>
    /// Take damage from an enemy. Returns true if destroyed.
    /// </summary>
    public bool TakeDamage(int amount)
    {
        Health -= amount;
        DamageFlash = 3;
        return IsDestroyed;
    }
    
    /// <summary>
    /// Ticks visual effects (damage flash). Call each frame.
    /// </summary>
    public void TickVisuals()
    {
        if (DamageFlash > 0) DamageFlash--;
    }
}