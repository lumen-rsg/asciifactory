using Asciifactory.Items;
using Asciifactory.WorldGen;

namespace Asciifactory.Machines;

/// <summary>
/// Miner: placed on a resource deposit. Automatically extracts resources over time.
/// Outputs items via its output port.
/// </summary>
public class Miner : MachineBase
{
    private const float MineTime = 2.0f;
    private const float ProgressPerTick = 1.0f / (MineTime * 20);
    
    private readonly ItemId? _resourceId;
    private readonly bool _validDeposit;
    
    public Miner(int x, int y, Direction direction, TileType depositTile) 
        : base(MachineType.Miner, x, y, direction, 2)
    {
        _resourceId = ItemRegistry.GetItemFromTile(depositTile);
        _validDeposit = _resourceId.HasValue;
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (!IsPowered || !_validDeposit || _resourceId == null) return false;
        if (InternalInventory.GetCount(_resourceId.Value) >= 5) return false;
        
        Progress += ProgressPerTick;
        
        if (Progress >= 1.0f)
        {
            Progress = 0;
            int overflow = InternalInventory.AddItem(_resourceId.Value, 1);
            
            if (overflow == 0)
                return TryOutputItem(_resourceId.Value, 1, getMachineAt);
        }
        
        return false;
    }
    
    public override char GetDisplayChar() => _validDeposit ? 'M' : '?';
}

/// <summary>
/// Conveyor Belt: transports items from input side to output side.
/// Direction-aware visual.
/// </summary>
public class ConveyorBelt : MachineBase
{
    private const float MoveTime = 0.5f;
    private const float ProgressPerTick = 1.0f / (MoveTime * 20);
    
    private ItemId? _carriedItem;
    private int _carriedCount;
    
    public ConveyorBelt(int x, int y, Direction direction) 
        : base(MachineType.ConveyorBelt, x, y, direction, 1)
    {
    }
    
    public override int AcceptItem(ItemId itemId, int count)
    {
        if (_carriedItem.HasValue && _carriedItem != itemId) return count;
        if (_carriedCount >= 5) return count;
        
        int canAccept = Math.Min(count, 5 - _carriedCount);
        _carriedItem = itemId;
        _carriedCount += canAccept;
        Progress = 0;
        return count - canAccept;
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (_carriedItem == null || _carriedCount <= 0) return false;
        
        Progress += ProgressPerTick;
        
        if (Progress >= 1.0f)
        {
            var (ox, oy) = GetOutputPos();
            var target = getMachineAt(ox, oy);
            
            if (target != null)
            {
                int remaining = target.AcceptItem(_carriedItem.Value, _carriedCount);
                if (remaining == 0)
                {
                    _carriedItem = null;
                    _carriedCount = 0;
                    Progress = 0;
                    return true;
                }
                _carriedCount = remaining;
                Progress = 0.9f;
                return false;
            }
            Progress = 0.9f;
            return false;
        }
        
        return false;
    }
    
    public override char GetDisplayChar()
    {
        if (_carriedItem.HasValue)
            return ItemRegistry.GetSymbol(_carriedItem.Value);
        var (dx, dy) = Direction.ToDelta();
        return MachineTypeInfo.GetBeltSymbol(dx, dy);
    }
    
    public override ConsoleColor GetDisplayColor()
    {
        if (_carriedItem.HasValue)
            return ItemRegistry.GetColor(_carriedItem.Value);
        return ConsoleColor.DarkGray; // Conveyors don't need power
    }
}

/// <summary>
/// Furnace: 3x2 multi-tile smelter. Smelts ore into plates using smelting recipes.
/// Two item input ports (left side) and one item output port (right side).
/// </summary>
public class Furnace : MachineBase
{
    private Recipe? _currentRecipe;
    private float _progressPerTick;
    
    public Furnace(int x, int y, Direction direction) 
        : base(MachineType.Furnace, x, y, direction, 6)
    {
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (!IsPowered) return false;
        
        if (_currentRecipe != null && Progress > 0)
        {
            Progress += _progressPerTick;
            
            if (Progress >= 1.0f)
            {
                int overflow = InternalInventory.AddItem(_currentRecipe.Output, _currentRecipe.OutputCount);
                if (overflow == 0)
                {
                    TryOutputItem(_currentRecipe.Output, _currentRecipe.OutputCount, getMachineAt);
                    _currentRecipe = null;
                    Progress = 0;
                    return true;
                }
                Progress = 0.99f;
                return false;
            }
            return false;
        }
        
        // Try smelting recipes
        var recipes = RecipeRegistry.GetByCategory(RecipeCategory.Smelting);
        foreach (var recipe in recipes)
        {
            if (InternalInventory.HasIngredients(recipe.Inputs))
            {
                InternalInventory.RemoveIngredients(recipe.Inputs);
                _currentRecipe = recipe;
                _progressPerTick = 1.0f / (recipe.CraftTime * 20);
                Progress = 0.01f;
                return false;
            }
        }
        
        return false;
    }
    
    // Uses base class box-drawing outline with 'F' label and output port overlay
}

/// <summary>
/// Assembler: 3x3 multi-tile crafting machine. Crafts assembly recipes from input items.
/// Two item input ports (left side) and one item output port (right side).
/// </summary>
public class Assembler : MachineBase
{
    private Recipe? _currentRecipe;
    private float _progressPerTick;
    
    public Assembler(int x, int y, Direction direction) 
        : base(MachineType.Assembler, x, y, direction, 8)
    {
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (!IsPowered) return false;
        
        if (_currentRecipe != null && Progress > 0)
        {
            Progress += _progressPerTick;
            
            if (Progress >= 1.0f)
            {
                int overflow = InternalInventory.AddItem(_currentRecipe.Output, _currentRecipe.OutputCount);
                if (overflow == 0)
                {
                    TryOutputItem(_currentRecipe.Output, _currentRecipe.OutputCount, getMachineAt);
                    _currentRecipe = null;
                    Progress = 0;
                    return true;
                }
                Progress = 0.99f;
                return false;
            }
            return false;
        }
        
        var recipes = RecipeRegistry.GetByCategory(RecipeCategory.Assembly);
        foreach (var recipe in recipes)
        {
            if (InternalInventory.HasIngredients(recipe.Inputs))
            {
                InternalInventory.RemoveIngredients(recipe.Inputs);
                _currentRecipe = recipe;
                _progressPerTick = 1.0f / (recipe.CraftTime * 20);
                Progress = 0.01f;
                return false;
            }
        }
        
        return false;
    }
    
    // Uses base class box-drawing outline with 'A' label and output port overlay
}

/// <summary>
/// Lab: 3x3 multi-tile research machine. Accepts components, outputs Science Packs.
/// Two item input ports (left side) and one item output port (right side).
/// </summary>
public class Lab : MachineBase
{
    private Recipe? _currentRecipe;
    private float _progressPerTick;
    
    public Lab(int x, int y, Direction direction) 
        : base(MachineType.Lab, x, y, direction, 8)
    {
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (!IsPowered) return false;
        
        if (_currentRecipe != null && Progress > 0)
        {
            Progress += _progressPerTick;
            
            if (Progress >= 1.0f)
            {
                int overflow = InternalInventory.AddItem(_currentRecipe.Output, _currentRecipe.OutputCount);
                if (overflow == 0)
                {
                    TryOutputItem(_currentRecipe.Output, _currentRecipe.OutputCount, getMachineAt);
                    _currentRecipe = null;
                    Progress = 0;
                    return true;
                }
                Progress = 0.99f;
                return false;
            }
            return false;
        }
        
        // Lab processes Research recipes
        var recipes = RecipeRegistry.GetByCategory(RecipeCategory.Research);
        foreach (var recipe in recipes)
        {
            if (InternalInventory.HasIngredients(recipe.Inputs))
            {
                InternalInventory.RemoveIngredients(recipe.Inputs);
                _currentRecipe = recipe;
                _progressPerTick = 1.0f / (recipe.CraftTime * 20);
                Progress = 0.01f;
                return false;
            }
        }
        
        return false;
    }
    
    // Uses base class box-drawing outline with 'L' label and output port overlay
}

/// <summary>
/// Power Generator (Legacy): burns fuel to power nearby machines.
/// Wraps BiomassBurner behavior for backward compatibility.
/// </summary>
public class PowerGenerator : MachineBase
{
    public const int PowerRadius = 10;
    private const float BurnTime = 5.0f;
    private const float ProgressPerTick = 1.0f / (BurnTime * 20);
    
    private bool _isBurning;
    
    public PowerGenerator(int x, int y, Direction direction) 
        : base(MachineType.PowerGenerator, x, y, direction, 4)
    {
        IsPowered = true;
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (FuseTripped) return false;
        
        if (_isBurning)
        {
            Progress += ProgressPerTick;
            if (Progress >= 1.0f)
            {
                Progress = 0;
                _isBurning = false;
            }
            return false;
        }
        
        ItemId[] fuels = { ItemId.Fuel, ItemId.Coal, ItemId.Uranium, ItemId.Biomass };
        foreach (var fuel in fuels)
        {
            if (InternalInventory.HasItem(fuel, 1))
            {
                InternalInventory.RemoveItem(fuel, 1);
                _isBurning = true;
                Progress = 0.01f;
                return false;
            }
        }
        
        return false;
    }
    
    public bool IsGenerating => !FuseTripped && (_isBurning || HasFuel());
    
    private bool HasFuel()
    {
        return InternalInventory.HasItem(ItemId.Fuel, 1) 
            || InternalInventory.HasItem(ItemId.Coal, 1)
            || InternalInventory.HasItem(ItemId.Uranium, 1)
            || InternalInventory.HasItem(ItemId.Biomass, 1);
    }
    
    public override char GetDisplayChar() => FuseTripped ? 'g' : (_isBurning ? 'G' : 'g');
    public override ConsoleColor GetDisplayColor() => FuseTripped ? ConsoleColor.Black : (_isBurning ? ConsoleColor.Yellow : ConsoleColor.DarkYellow);
}

/// <summary>
/// Wire: passes power between generators and machines. No item processing.
/// Renders as a transparent overlay on top of terrain tiles.
/// </summary>
public class Wire : MachineBase
{
    public Wire(int x, int y, Direction direction) 
        : base(MachineType.Wire, x, y, direction, 0)
    {
        IsPowered = true;
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt) => false;
    public override int AcceptItem(ItemId itemId, int count) => count;
    
    /// <summary>
    /// Gets the wire connector character based on adjacent wires/machines.
    /// Returns thin line segments that connect to neighbors.
    /// </summary>
    public char GetConnectorChar(Func<int, int, MachineBase?> getMachineAt)
    {
        bool up = HasConnection(X, Y - 1, getMachineAt);
        bool down = HasConnection(X, Y + 1, getMachineAt);
        bool left = HasConnection(X - 1, Y, getMachineAt);
        bool right = HasConnection(X + 1, Y, getMachineAt);
        
        return (up, down, left, right) switch
        {
            (true, true, true, true)   => '┼',
            (true, true, false, false) => '│',
            (false, false, true, true) => '─',
            (true, false, true, true)   => '┴',
            (false, true, true, true)   => '┬',
            (true, true, true, false)  => '┤',
            (true, true, false, true)  => '├',
            (true, false, true, false) => '┘',
            (true, false, false, true) => '└',
            (false, true, true, false) => '┐',
            (false, true, false, true) => '┌',
            (true, false, false, false) => '╵',
            (false, true, false, false) => '╷',
            (false, false, true, false) => '╴',
            (false, false, false, true) => '╶',
            _ => '·'
        };
    }
    
    private bool HasConnection(int nx, int ny, Func<int, int, MachineBase?> getMachineAt)
    {
        var neighbor = getMachineAt(nx, ny);
        return neighbor != null;
    }
    
    public override char GetDisplayChar() => IsPowered ? '·' : '·';
    public override ConsoleColor GetDisplayColor() => IsPowered ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
}

/// <summary>
/// Biomass Burner: burns Biomass to produce 5MW. Self-powered.
/// </summary>
public class BiomassBurner : MachineBase
{
    private const float BurnTime = 3.0f;
    private const float ProgressPerTick = 1.0f / (BurnTime * 20);
    
    private bool _isBurning;
    
    public BiomassBurner(int x, int y, Direction direction) 
        : base(MachineType.BiomassBurner, x, y, direction, 4)
    {
        IsPowered = true;
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (FuseTripped) return false;
        
        if (_isBurning)
        {
            Progress += ProgressPerTick;
            if (Progress >= 1.0f)
            {
                Progress = 0;
                _isBurning = false;
            }
            return false;
        }
        
        if (InternalInventory.HasItem(ItemId.Biomass, 1))
        {
            InternalInventory.RemoveItem(ItemId.Biomass, 1);
            _isBurning = true;
            Progress = 0.01f;
        }
        
        return false;
    }
    
    public bool IsGenerating => !FuseTripped && (_isBurning || InternalInventory.HasItem(ItemId.Biomass, 1));
    public override char GetDisplayChar() => FuseTripped ? 'b' : (_isBurning ? 'B' : 'b');
    public override ConsoleColor GetDisplayColor() => FuseTripped ? ConsoleColor.Black : (_isBurning ? ConsoleColor.Green : ConsoleColor.DarkGreen);
}

/// <summary>
/// Coal Generator: 2x2 multi-tile. Burns Coal to produce 25MW. Needs adjacent Water tile for cooling.
/// One item input port (coal).
/// </summary>
public class CoalGenerator : MachineBase
{
    private const float BurnTime = 8.0f;
    private const float ProgressPerTick = 1.0f / (BurnTime * 20);
    
    private bool _isBurning;
    private readonly bool _hasWaterAccess;
    
    public CoalGenerator(int x, int y, Direction direction, Func<int, int, TileType>? getTile = null) 
        : base(MachineType.CoalGenerator, x, y, direction, 6)
    {
        IsPowered = true;
        _hasWaterAccess = getTile != null && CheckWaterAccess(getTile);
    }
    
    private bool CheckWaterAccess(Func<int, int, TileType> getTile)
    {
        // Check all occupied tiles for adjacent water
        foreach (var (cx, cy) in GetOccupiedCells())
        {
            int[][] dirs = { new[] { 0, -1 }, new[] { 0, 1 }, new[] { -1, 0 }, new[] { 1, 0 } };
            foreach (var d in dirs)
            {
                if (getTile(cx + d[0], cy + d[1]) == TileType.Water)
                    return true;
            }
        }
        return false;
    }
    
    public bool HasWaterAccess => _hasWaterAccess;
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (FuseTripped) return false;
        
        if (_isBurning)
        {
            Progress += ProgressPerTick;
            if (Progress >= 1.0f)
            {
                Progress = 0;
                _isBurning = false;
            }
            return false;
        }
        
        if (_hasWaterAccess && InternalInventory.HasItem(ItemId.Coal, 1))
        {
            InternalInventory.RemoveItem(ItemId.Coal, 1);
            _isBurning = true;
            Progress = 0.01f;
        }
        
        return false;
    }
    
    public bool IsGenerating => !FuseTripped && _hasWaterAccess && (_isBurning || InternalInventory.HasItem(ItemId.Coal, 1));
    
    // Uses base class box-drawing outline with 'C'/'c' label
    public override char GetDisplayChar() => FuseTripped ? 'c' : (_isBurning ? 'C' : 'c');
    public override ConsoleColor GetDisplayColor() => FuseTripped ? ConsoleColor.Black : (_isBurning ? ConsoleColor.Gray : ConsoleColor.DarkGray);
}

/// <summary>
/// Nuclear Reactor: 3x3 multi-tile. Burns Uranium to produce 600MW. High-tier power source.
/// One item input port (top center, uranium).
/// </summary>
public class NuclearReactor : MachineBase
{
    private const float BurnTime = 30.0f;
    private const float ProgressPerTick = 1.0f / (BurnTime * 20);
    
    private bool _isBurning;
    
    public NuclearReactor(int x, int y, Direction direction) 
        : base(MachineType.NuclearReactor, x, y, direction, 4)
    {
        IsPowered = true;
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (FuseTripped) return false;
        
        if (_isBurning)
        {
            Progress += ProgressPerTick;
            if (Progress >= 1.0f)
            {
                Progress = 0;
                _isBurning = false;
            }
            return false;
        }
        
        if (InternalInventory.HasItem(ItemId.Uranium, 1))
        {
            InternalInventory.RemoveItem(ItemId.Uranium, 1);
            _isBurning = true;
            Progress = 0.01f;
        }
        
        return false;
    }
    
    public bool IsGenerating => !FuseTripped && (_isBurning || InternalInventory.HasItem(ItemId.Uranium, 1));
    
    // Uses base class box-drawing outline with '☢' label
    public override char GetDisplayChar() => '☢';
    public override ConsoleColor GetDisplayColor()
    {
        if (FuseTripped) return ConsoleColor.Black;
        return _isBurning ? ConsoleColor.Green : ConsoleColor.DarkGreen;
    }
}

/// <summary>
/// Oil Refinery: 3x3 multi-tile. Processes crude oil into plastic, fuel, and fiber.
/// Two item input ports and one item output port.
/// </summary>
public class Refinery : MachineBase
{
    private Recipe? _currentRecipe;
    private float _progressPerTick;
    
    public Refinery(int x, int y, Direction direction) 
        : base(MachineType.Refinery, x, y, direction, 6)
    {
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (!IsPowered) return false;
        
        if (_currentRecipe != null && Progress > 0)
        {
            Progress += _progressPerTick;
            
            if (Progress >= 1.0f)
            {
                int overflow = InternalInventory.AddItem(_currentRecipe.Output, _currentRecipe.OutputCount);
                if (overflow == 0)
                {
                    TryOutputItem(_currentRecipe.Output, _currentRecipe.OutputCount, getMachineAt);
                    _currentRecipe = null;
                    Progress = 0;
                    return true;
                }
                Progress = 0.99f;
                return false;
            }
            return false;
        }
        
        var recipes = RecipeRegistry.GetByCategory(RecipeCategory.Chemical);
        foreach (var recipe in recipes)
        {
            if (InternalInventory.HasIngredients(recipe.Inputs))
            {
                InternalInventory.RemoveIngredients(recipe.Inputs);
                _currentRecipe = recipe;
                _progressPerTick = 1.0f / (recipe.CraftTime * 20);
                Progress = 0.01f;
                return false;
            }
        }
        
        return false;
    }
    
    // Uses base class box-drawing outline with 'R' label and output port overlay
}

/// <summary>
/// Firewall Turret: auto-targets nearest enemy within range and fires projectiles.
/// Needs power and Iron Plates as ammo. Range: 7 tiles.
/// </summary>
public class FirewallTurret : MachineBase
{
    public const int Range = 7;
    private const float FireTime = 1.0f;
    private const float ProgressPerTick = 1.0f / (FireTime * 20);
    
    private int _targetX, _targetY;
    private bool _hasTarget;
    
    public FirewallTurret(int x, int y, Direction direction) 
        : base(MachineType.FirewallTurret, x, y, direction, 2)
    {
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        if (!IsPowered) return false;
        if (!_hasTarget) return false;
        if (!InternalInventory.HasItem(ItemId.IronPlate, 1)) return false;
        
        Progress += ProgressPerTick;
        
        if (Progress >= 1.0f)
        {
            Progress = 0;
            return true;
        }
        
        return false;
    }
    
    public bool AcquireTarget(System.Collections.Generic.IReadOnlyList<Entities.Enemy> enemies)
    {
        _hasTarget = false;
        int bestDist = int.MaxValue;
        
        foreach (var enemy in enemies)
        {
            int dist = Math.Abs(enemy.X - X) + Math.Abs(enemy.Y - Y);
            if (dist <= Range && dist < bestDist)
            {
                bestDist = dist;
                _targetX = enemy.X;
                _targetY = enemy.Y;
                _hasTarget = true;
            }
        }
        
        return _hasTarget;
    }
    
    public (int dx, int dy, int damage)? Fire()
    {
        if (!_hasTarget) return null;
        if (!InternalInventory.HasItem(ItemId.IronPlate, 1)) return null;
        
        InternalInventory.RemoveItem(ItemId.IronPlate, 1);
        
        int dx = Math.Sign(_targetX - X);
        int dy = Math.Sign(_targetY - Y);
        if (dx == 0 && dy == 0) dx = 1;
        
        return (dx, dy, 2);
    }
    
    public override char GetDisplayChar() => _hasTarget ? 'T' : 't';
    public override ConsoleColor GetDisplayColor() => _hasTarget ? ConsoleColor.Red : ConsoleColor.DarkRed;
}

/// <summary>
/// Storage Chest: acts as a buffer. Accepts items from any direction.
/// </summary>
public class StorageChest : MachineBase
{
    public StorageChest(int x, int y, Direction direction) 
        : base(MachineType.StorageChest, x, y, direction, 16)
    {
    }
    
    public override bool Tick(Func<int, int, MachineBase?> getMachineAt)
    {
        var filledSlots = InternalInventory.GetFilledSlots().ToList();
        if (filledSlots.Count == 0) return false;
        
        var (_, stack) = filledSlots[0];
        if (stack.Count > 0)
        {
            var (ox, oy) = GetOutputPos();
            var target = getMachineAt(ox, oy);
            if (target != null)
            {
                int remaining = target.AcceptItem(stack.Id, 1);
                if (remaining == 0)
                {
                    InternalInventory.RemoveItem(stack.Id, 1);
                    return true;
                }
            }
        }
        
        return false;
    }
    
    public override int AcceptItem(ItemId itemId, int count) => InternalInventory.AddItem(itemId, count);
    public override ConsoleColor GetDisplayColor() => ConsoleColor.Yellow; // Chests don't need power
}
