namespace Asciifactory.WorldGen;

/// <summary>
/// Manages the infinite chunk-based world. Loads/generates chunks on demand
/// and unloads distant chunks to manage memory.
/// </summary>
public class World
{
    private readonly Dictionary<(int cx, int cy), Chunk> _chunks = new();
    private readonly WorldGenerator _generator;
    
    /// <summary>
    /// The world seed used for generation (for save/load).
    /// </summary>
    public int Seed => _generator.Seed;
    
    /// <summary>
    /// How many chunks around the player to keep loaded (in each direction).
    /// </summary>
    private const int RenderDistance = 3;
    
    public WorldGenerator Generator => _generator;
    
    public World(int seed)
    {
        _generator = new WorldGenerator(seed);
    }
    
    /// <summary>
    /// Gets the tile at the given world coordinates.
    /// Generates the chunk if it doesn't exist.
    /// </summary>
    public TileType GetTile(int worldX, int worldY)
    {
        var (cx, cy) = Chunk.WorldToChunk(worldX, worldY);
        var chunk = GetOrCreateChunk(cx, cy);
        var (lx, ly) = Chunk.WorldToLocal(worldX, worldY);
        return chunk.Tiles[lx, ly];
    }
    
    /// <summary>
    /// Sets the tile at the given world coordinates.
    /// </summary>
    public void SetTile(int worldX, int worldY, TileType tile)
    {
        var (cx, cy) = Chunk.WorldToChunk(worldX, worldY);
        var chunk = GetOrCreateChunk(cx, cy);
        var (lx, ly) = Chunk.WorldToLocal(worldX, worldY);
        chunk.Tiles[lx, ly] = tile;
    }
    
    /// <summary>
    /// Gets the biome at the given world coordinates.
    /// </summary>
    public BiomeType GetBiome(int worldX, int worldY)
    {
        var (cx, cy) = Chunk.WorldToChunk(worldX, worldY);
        var chunk = GetOrCreateChunk(cx, cy);
        return chunk.Biome;
    }
    
    /// <summary>
    /// Gets all chunks currently loaded within render distance of the given position.
    /// </summary>
    public IEnumerable<Chunk> GetVisibleChunks(int centerWorldX, int centerWorldY)
    {
        var (ccx, ccy) = Chunk.WorldToChunk(centerWorldX, centerWorldY);
        
        for (int dy = -RenderDistance; dy <= RenderDistance; dy++)
        {
            for (int dx = -RenderDistance; dx <= RenderDistance; dx++)
            {
                yield return GetOrCreateChunk(ccx + dx, ccy + dy);
            }
        }
    }
    
    /// <summary>
    /// Unloads chunks that are too far from the player to free memory.
    /// </summary>
    public void UnloadDistantChunks(int centerWorldX, int centerWorldY)
    {
        var (ccx, ccy) = Chunk.WorldToChunk(centerWorldX, centerWorldY);
        int unloadDistance = RenderDistance + 2;
        
        var toRemove = new List<(int, int)>();
        foreach (var ((cx, cy), _) in _chunks)
        {
            if (Math.Abs(cx - ccx) > unloadDistance || Math.Abs(cy - ccy) > unloadDistance)
            {
                toRemove.Add((cx, cy));
            }
        }
        
        foreach (var key in toRemove)
        {
            _chunks.Remove(key);
        }
    }
    
    /// <summary>
    /// Gets or creates a chunk at the given chunk coordinates.
    /// </summary>
    private Chunk GetOrCreateChunk(int cx, int cy)
    {
        if (!_chunks.TryGetValue((cx, cy), out var chunk))
        {
            chunk = _generator.GenerateChunk(cx, cy);
            _chunks[(cx, cy)] = chunk;
        }
        return chunk;
    }
    
    /// <summary>
    /// Gets a chunk if it's already loaded (returns null if not).
    /// </summary>
    public Chunk? GetLoadedChunk(int cx, int cy)
    {
        return _chunks.TryGetValue((cx, cy), out var chunk) ? chunk : null;
    }
}