using Asciifactory.WorldGen;

namespace Asciifactory;

/// <summary>
/// Scans terrain around the player for resource deposits.
/// Shows results as a minimap overlay with resource directions.
/// </summary>
public class TerrainScanner
{
    public const int ScanRadius = 64;
    
    /// <summary>
    /// Scan progress 0..1
    /// </summary>
    public float Progress { get; private set; }
    
    /// <summary>
    /// Whether a scan is currently in progress.
    /// </summary>
    public bool IsScanning { get; private set; }
    
    /// <summary>
    /// Whether scan results are ready to display.
    /// </summary>
    public bool HasResults { get; private set; }
    
    /// <summary>
    /// Detected resources: resource type → list of (dx, dy) from scan center.
    /// </summary>
    public Dictionary<TileType, List<(int dx, int dy)>> DetectedResources { get; private set; } = new();
    
    /// <summary>
    /// Minimap representation: char/color grid (small, e.g. 33x33).
    /// </summary>
    public (char ch, ConsoleColor color)[,]? Minimap { get; private set; }
    
    private int _centerX, _centerY;
    private int _scannedRows;
    private int _minimapSize;
    
    private static readonly TileType[] ResourceTypes = new[]
    {
        TileType.IronOre, TileType.CopperOre, TileType.Coal, TileType.StoneDeposit,
        TileType.Quartz, TileType.Oil, TileType.Uranium,
    };
    
    private static readonly Dictionary<TileType, (char sym, ConsoleColor color)> ResourceVisuals = new()
    {
        { TileType.IronOre,      ('i', ConsoleColor.White) },
        { TileType.CopperOre,    ('c', ConsoleColor.DarkYellow) },
        { TileType.Coal,         ('*', ConsoleColor.DarkGray) },
        { TileType.StoneDeposit, ('o', ConsoleColor.Gray) },
        { TileType.Quartz,       ('q', ConsoleColor.Cyan) },
        { TileType.Oil,          ('p', ConsoleColor.DarkMagenta) },
        { TileType.Uranium,      ('u', ConsoleColor.Green) },
    };
    
    /// <summary>
    /// Start a new scan centered on the given position.
    /// </summary>
    public void StartScan(int centerX, int centerY)
    {
        _centerX = centerX;
        _centerY = centerY;
        Progress = 0;
        IsScanning = true;
        HasResults = false;
        _scannedRows = 0;
        DetectedResources = new Dictionary<TileType, List<(int, int)>>();
        
        // Minimap covers scan radius, scaled down to ~33 chars
        _minimapSize = Math.Min(33, ScanRadius * 2 + 1);
        Minimap = new (char, ConsoleColor)[_minimapSize, _minimapSize];
        
        // Initialize minimap with empty
        for (int y = 0; y < _minimapSize; y++)
            for (int x = 0; x < _minimapSize; x++)
                Minimap[x, y] = (' ', ConsoleColor.Black);
    }
    
    /// <summary>
    /// Process a batch of scan rows. Call each tick until complete.
    /// </summary>
    public void TickScan(World world, int rowsPerTick = 8)
    {
        if (!IsScanning || Minimap == null) return;
        
        int diameter = ScanRadius * 2 + 1;
        float scale = (float)_minimapSize / diameter;
        
        int rowsThisTick = 0;
        
        for (int dy = -ScanRadius; dy <= ScanRadius; dy++)
        {
            if (_scannedRows >= diameter * 2)
                break;
            
            if (rowsThisTick >= rowsPerTick)
                break;
            
            int wy = _centerY + dy;
            
            for (int dx = -ScanRadius; dx <= ScanRadius; dx++)
            {
                int wx = _centerX + dx;
                var tile = world.GetTile(wx, wy);
                
                // Map to minimap coordinates
                int mx = (int)((dx + ScanRadius) * scale);
                int my = (int)((dy + ScanRadius) * scale);
                
                if (mx >= 0 && mx < _minimapSize && my >= 0 && my < _minimapSize)
                {
                    // Only overwrite if current is empty or this is a resource
                    if (ResourceVisuals.ContainsKey(tile))
                    {
                        var (sym, color) = ResourceVisuals[tile];
                        Minimap[mx, my] = (sym, color);
                        
                        if (!DetectedResources.ContainsKey(tile))
                            DetectedResources[tile] = new List<(int, int)>();
                        DetectedResources[tile].Add((dx, dy));
                    }
                    else if (Minimap[mx, my].ch == ' ')
                    {
                        // Background terrain
                        var (sym, color) = GetTerrainChar(tile);
                        Minimap[mx, my] = (sym, color);
                    }
                }
            }
            
            _scannedRows++;
            rowsThisTick++;
        }
        
        Progress = (float)_scannedRows / (diameter * 2);
        
        if (_scannedRows >= diameter * 2)
        {
            IsScanning = false;
            HasResults = true;
            Progress = 1.0f;
        }
    }
    
    private static (char, ConsoleColor) GetTerrainChar(TileType tile) => tile switch
    {
        TileType.Water  => ('~', ConsoleColor.DarkBlue),
        TileType.Grass  => ('.', ConsoleColor.DarkGreen),
        TileType.Dirt   => ('.', ConsoleColor.DarkYellow),
        TileType.Sand   => ('.', ConsoleColor.DarkYellow),
        TileType.Stone  => ('.', ConsoleColor.DarkGray),
        TileType.Snow   => ('.', ConsoleColor.White),
        TileType.Swamp  => ('.', ConsoleColor.DarkCyan),
        _               => (' ', ConsoleColor.Black),
    };
    
    /// <summary>
    /// Get the nearest resource of each type, sorted by distance.
    /// </summary>
    public List<(TileType type, int dx, int dy, double distance)> GetNearestResources()
    {
        var results = new List<(TileType, int, int, double)>();
        
        foreach (var (type, positions) in DetectedResources)
        {
            double bestDist = double.MaxValue;
            int bestDx = 0, bestDy = 0;
            
            foreach (var (dx, dy) in positions)
            {
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestDx = dx;
                    bestDy = dy;
                }
            }
            
            results.Add((type, bestDx, bestDy, bestDist));
        }
        
        return results.OrderBy(r => r.Item4).ToList();
    }
    
    /// <summary>
    /// Close the scanner results overlay.
    /// </summary>
    public void Close()
    {
        HasResults = false;
        IsScanning = false;
        Minimap = null;
        DetectedResources.Clear();
    }
}