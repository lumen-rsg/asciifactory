using Asciifactory.Items;
using Asciifactory.WorldGen;

namespace Asciifactory.Machines;

/// <summary>
/// Represents a connected power grid: generators + wires + consuming machines.
/// Built via flood-fill from generators through wires.
/// </summary>
public class PowerGrid
{
    public int GridId { get; }
    public List<MachineBase> Generators { get; } = new();
    public List<MachineBase> Consumers { get; } = new();
    public List<MachineBase> Wires { get; } = new();
    public HashSet<(int x, int y)> AllPositions { get; } = new();
    
    public int TotalSupply => Generators.Sum(g =>
    {
        return g switch
        {
            PowerGenerator pg when pg.IsGenerating => MachineTypeInfo.GetPowerOutput(g.Type),
            BiomassBurner bb when bb.IsGenerating => MachineTypeInfo.GetPowerOutput(g.Type),
            CoalGenerator cg when cg.IsGenerating => MachineTypeInfo.GetPowerOutput(g.Type),
            NuclearReactor nr when nr.IsGenerating => MachineTypeInfo.GetPowerOutput(g.Type),
            _ => 0
        };
    });
    
    public int TotalDemand => Consumers.Sum(c => c.PowerDraw);
    public int MaxSupply => Generators.Sum(g => MachineTypeInfo.GetPowerOutput(g.Type));
    public bool IsOverloaded => TotalDemand > TotalSupply && TotalSupply > 0;
    
    public PowerGrid(int gridId)
    {
        GridId = gridId;
    }
}

/// <summary>
/// Manages all placed machines in the world.
/// Handles placement, removal, lookup, ticking, and power grid computation.
/// Supports multi-tile machines: all occupied cells map to the master machine.
/// </summary>
public class MachineGrid
{
    // Maps every occupied tile to the machine that occupies it
    private readonly Dictionary<(int x, int y), MachineBase> _machines = new();
    // Tracks which positions are master (origin) cells of multi-tile machines
    private readonly HashSet<(int x, int y)> _masterCells = new();
    
    private List<PowerGrid> _powerGrids = new();
    
    public int MachineCount => _masterCells.Count;
    public IReadOnlyList<PowerGrid> PowerGrids => _powerGrids;
    
    /// <summary>
    /// Gets the machine at the given world position, or null.
    /// For multi-tile machines, any occupied tile returns the master machine.
    /// </summary>
    public MachineBase? GetMachineAt(int x, int y)
    {
        return _machines.TryGetValue((x, y), out var machine) ? machine : null;
    }
    
    /// <summary>
    /// Gets all distinct machines (master cells only, no duplicates for multi-tile).
    /// </summary>
    public IEnumerable<MachineBase> GetAllMachines()
    {
        foreach (var pos in _masterCells)
        {
            if (_machines.TryGetValue(pos, out var machine))
                yield return machine;
        }
    }
    
    /// <summary>
    /// Gets all machines whose master cell is in the visible viewport.
    /// </summary>
    public IEnumerable<MachineBase> GetVisibleMachines(int topLeftX, int topLeftY, int width, int height)
    {
        foreach (var pos in _masterCells)
        {
            if (pos.x >= topLeftX && pos.x < topLeftX + width &&
                pos.y >= topLeftY && pos.y < topLeftY + height)
            {
                if (_machines.TryGetValue(pos, out var machine))
                    yield return machine;
            }
        }
    }
    
    /// <summary>
    /// Checks if a world position is occupied by any machine.
    /// </summary>
    public bool IsOccupied(int x, int y) => _machines.ContainsKey((x, y));
    
    /// <summary>
    /// Places a machine. Registers all occupied tiles.
    /// Returns true if successful. Checks all tiles are unoccupied and walkable.
    /// </summary>
    public bool PlaceMachine(MachineBase machine, Func<int, int, TileType> getTile)
    {
        // Check all cells are free and walkable
        foreach (var (cx, cy) in machine.GetOccupiedCells())
        {
            if (_machines.ContainsKey((cx, cy)))
                return false;
            
            var tile = getTile(cx, cy);
            if (tile == TileType.Water)
                return false;
        }
        
        // Register all cells
        foreach (var (cx, cy) in machine.GetOccupiedCells())
        {
            _machines[(cx, cy)] = machine;
        }
        
        // Mark origin as master
        _masterCells.Add((machine.X, machine.Y));
        
        return true;
    }
    
    /// <summary>
    /// Removes and returns the machine at the given position.
    /// Removes all occupied cells for multi-tile machines.
    /// </summary>
    public MachineBase? RemoveMachine(int x, int y)
    {
        if (!_machines.TryGetValue((x, y), out var machine))
            return null;
        
        // Remove all occupied cells
        foreach (var (cx, cy) in machine.GetOccupiedCells())
        {
            _machines.Remove((cx, cy));
        }
        
        // Remove master cell
        _masterCells.Remove((machine.X, machine.Y));
        
        return machine;
    }
    
    /// <summary>
    /// Gets the power grid info for a machine at the given position.
    /// Returns (supply, demand) for the grid the machine belongs to.
    /// </summary>
    public (int supply, int demand) GetPowerInfoFor(int x, int y)
    {
        // Find the machine at this position (may be a non-master cell)
        var machine = GetMachineAt(x, y);
        if (machine == null) return (0, 0);
        
        // Use the master position for grid lookup
        foreach (var grid in _powerGrids)
        {
            if (grid.AllPositions.Contains((machine.X, machine.Y)))
                return (grid.TotalSupply, grid.TotalDemand);
        }
        return (0, 0);
    }
    
    /// <summary>
    /// Updates the power state of all machines using flood-fill from generators through wires.
    /// For multi-tile machines, only the master cell participates in flood-fill.
    /// Power is applied to the machine object (shared by all cells).
    /// </summary>
    public void UpdatePowerGrid()
    {
        var visited = new HashSet<(int x, int y)>();
        _powerGrids.Clear();
        int gridId = 0;
        
        // Unpower all non-generator machines
        foreach (var machine in GetAllMachines())
        {
            if (!MachineTypeInfo.IsGenerator(machine.Type))
            {
                machine.IsPowered = false;
            }
        }
        
        // Find all generators (master cells only) and flood-fill from each unvisited one
        foreach (var machine in GetAllMachines())
        {
            if (!MachineTypeInfo.IsGenerator(machine.Type)) continue;
            if (visited.Contains((machine.X, machine.Y))) continue;
            
            var grid = new PowerGrid(gridId++);
            var queue = new Queue<MachineBase>();
            queue.Enqueue(machine);
            visited.Add((machine.X, machine.Y));
            
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                grid.AllPositions.Add((current.X, current.Y));
                
                if (MachineTypeInfo.IsGenerator(current.Type))
                    grid.Generators.Add(current);
                else if (MachineTypeInfo.IsWire(current.Type))
                    grid.Wires.Add(current);
                else
                    grid.Consumers.Add(current);
                
                // Expand to 4-directional neighbors of ALL occupied cells
                // For multi-tile machines, check all edges
                var expandPositions = current.GetOccupiedCells().ToList();
                
                foreach (var (cx, cy) in expandPositions)
                {
                    int[][] dirs = { new[] { 0, -1 }, new[] { 0, 1 }, new[] { -1, 0 }, new[] { 1, 0 } };
                    foreach (var d in dirs)
                    {
                        int nx = cx + d[0];
                        int ny = cy + d[1];
                        
                        // Skip if this neighbor is part of the same machine
                        if (current.GetOccupiedCells().Any(c => c.x == nx && c.y == ny))
                            continue;
                        
                        var neighbor = GetMachineAt(nx, ny);
                        if (neighbor == null) continue;
                        
                        // Use the master cell of the neighbor for visited check
                        if (visited.Contains((neighbor.X, neighbor.Y))) continue;
                        
                        // Connection rules (same as before)
                        bool canConnect = false;
                        
                        if (MachineTypeInfo.IsWire(current.Type))
                        {
                            canConnect = true;
                        }
                        else if (MachineTypeInfo.IsGenerator(current.Type))
                        {
                            canConnect = MachineTypeInfo.IsWire(neighbor.Type) 
                                || !MachineTypeInfo.IsGenerator(neighbor.Type);
                        }
                        else if (MachineTypeInfo.IsWire(neighbor.Type))
                        {
                            canConnect = true;
                        }
                        
                        if (canConnect)
                        {
                            visited.Add((neighbor.X, neighbor.Y));
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
            
            _powerGrids.Add(grid);
        }
        
        // Set power state for each grid
        foreach (var grid in _powerGrids)
        {
            bool overloaded = grid.IsOverloaded;
            
            foreach (var gen in grid.Generators)
                gen.IsPowered = true;
            
            foreach (var wire in grid.Wires)
                wire.IsPowered = !overloaded;
            
            foreach (var consumer in grid.Consumers)
            {
                if (overloaded)
                {
                    consumer.IsPowered = false;
                    consumer.FuseTripped = true;
                }
                else
                {
                    consumer.IsPowered = grid.TotalSupply > 0;
                    if (grid.TotalSupply >= grid.TotalDemand)
                        consumer.FuseTripped = false;
                }
            }
            
            foreach (var gen in grid.Generators)
            {
                if (overloaded)
                    gen.FuseTripped = true;
                else if (grid.TotalSupply >= grid.TotalDemand)
                    gen.FuseTripped = false;
            }
        }
    }
    
    /// <summary>
    /// Resets the fuse on a generator, restoring power to its grid.
    /// </summary>
    public void ResetFuse(MachineBase generator)
    {
        foreach (var grid in _powerGrids)
        {
            if (grid.AllPositions.Contains((generator.X, generator.Y)))
            {
                foreach (var pos in grid.AllPositions)
                {
                    var m = GetMachineAt(pos.x, pos.y);
                    if (m != null) m.FuseTripped = false;
                }
                UpdatePowerGrid();
                return;
            }
        }
    }
    
    /// <summary>
    /// Ticks all machines. Updates power first, then processes machines.
    /// Returns the number of items produced this tick.
    /// </summary>
    public int TickAll()
    {
        UpdatePowerGrid();
        
        int produced = 0;
        
        foreach (var machine in GetAllMachines())
        {
            if (machine.FuseTripped && !MachineTypeInfo.IsGenerator(machine.Type)) continue;
            // Turrets are ticked separately in Game.Update() with targeting logic
            if (machine.Type == MachineType.FirewallTurret) continue;
            if (machine.Tick(GetMachineAt))
            {
                produced++;
            }
        }
        
        return produced;
    }
    
    /// <summary>
    /// Gets the build cost for each machine type. Multi-tile machines cost more.
    /// </summary>
    public static (ItemId Id, int Count)[] GetBuildCost(MachineType type) => type switch
    {
        MachineType.Miner => new[] { (ItemId.Stone, 3), (ItemId.IronPlate, 2) },
        MachineType.ConveyorBelt => new[] { (ItemId.IronPlate, 1) },
        MachineType.Furnace => new[] { (ItemId.Stone, 8), (ItemId.IronPlate, 3) },
        MachineType.Assembler => new[] { (ItemId.IronPlate, 5), (ItemId.Gear, 3), (ItemId.Circuit, 2) },
        MachineType.StorageChest => new[] { (ItemId.IronPlate, 2) },
        MachineType.PowerGenerator => new[] { (ItemId.IronPlate, 3), (ItemId.Stone, 3), (ItemId.Gear, 1) },
        MachineType.Lab => new[] { (ItemId.IronPlate, 8), (ItemId.Circuit, 5), (ItemId.Gear, 3) },
        MachineType.Refinery => new[] { (ItemId.SteelPlate, 5), (ItemId.IronPlate, 5), (ItemId.Circuit, 3) },
        MachineType.FirewallTurret => new[] { (ItemId.IronPlate, 4), (ItemId.Circuit, 2), (ItemId.Gear, 2) },
        MachineType.Wire => new[] { (ItemId.CopperWire, 2) },
        MachineType.BiomassBurner => new[] { (ItemId.Stone, 5), (ItemId.IronPlate, 3), (ItemId.Gear, 1) },
        MachineType.CoalGenerator => new[] { (ItemId.IronPlate, 5), (ItemId.Stone, 5), (ItemId.Gear, 3) },
        MachineType.NuclearReactor => new[] { (ItemId.SteelPlate, 10), (ItemId.Circuit, 5), (ItemId.Gear, 5) },
        _ => Array.Empty<(ItemId, int)>()
    };
}