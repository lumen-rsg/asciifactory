namespace Asciifactory.WorldGen;

/// <summary>
/// Procedurally generates chunks using layered noise for biomes, elevation, and resources.
/// </summary>
public class WorldGenerator
{
    private readonly Noise _biomeNoise;
    private readonly Noise _elevationNoise;
    private readonly Noise _resourceNoise;
    private readonly Noise _moistureNoise;
    
    private const float BiomeScale = 0.005f;
    private const float ElevationScale = 0.02f;
    private const float ResourceScale = 0.08f;
    private const float MoistureScale = 0.008f;
    
    public int Seed { get; }
    
    public WorldGenerator(int seed)
    {
        Seed = seed;
        _biomeNoise = new Noise(seed);
        _elevationNoise = new Noise(seed + 1000);
        _resourceNoise = new Noise(seed + 2000);
        _moistureNoise = new Noise(seed + 3000);
    }
    
    /// <summary>
    /// Determines the biome for a chunk based on its position.
    /// Uses the center tile of the chunk to decide the biome.
    /// </summary>
    public BiomeType GetBiome(int chunkX, int chunkY)
    {
        // Sample at chunk center
        float cx = (chunkX * Chunk.Size + Chunk.Size / 2) * BiomeScale;
        float cy = (chunkY * Chunk.Size + Chunk.Size / 2) * BiomeScale;
        
        float temp = _biomeNoise.OctavePerlin2D(cx, cy, 4, 0.5f); // Temperature
        float moisture = _moistureNoise.OctavePerlin2D(cx + 500, cy + 500, 4, 0.5f); // Moisture
        
        // Biome selection based on temperature and moisture
        return (temp, moisture) switch
        {
            // Cold + dry = Mountains
            ( < -0.2f, _) => BiomeType.Mountains,
            // Hot + dry = Desert
            ( > 0.2f, < -0.1f) => BiomeType.Desert,
            // Hot + wet = Swamp
            ( > 0.1f, > 0.3f) => BiomeType.Swamp,
            // Moderate + wet = Forest
            ( > -0.2f, > 0.15f) => BiomeType.Forest,
            // Default = Plains
            _ => BiomeType.Plains
        };
    }
    
    /// <summary>
    /// Generates a complete chunk at the given chunk coordinates.
    /// </summary>
    public Chunk GenerateChunk(int chunkX, int chunkY)
    {
        var biome = GetBiome(chunkX, chunkY);
        var chunk = new Chunk(chunkX, chunkY, biome);
        
        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        
        for (int ly = 0; ly < Chunk.Size; ly++)
        {
            for (int lx = 0; lx < Chunk.Size; lx++)
            {
                int wx = worldOffsetX + lx;
                int wy = worldOffsetY + ly;
                
                // Get elevation at this tile
                float elevation = _elevationNoise.OctavePerlin2D(
                    wx * ElevationScale, wy * ElevationScale, 3, 0.5f);
                
                // Start with base terrain
                TileType tile = BiomeInfo.GetBaseTile(biome, elevation);
                
                // Try to place a resource deposit
                if (TileTypeInfo.IsWalkable(tile))
                {
                    float resourceVal = (_resourceNoise.OctavePerlin2D(
                        wx * ResourceScale, wy * ResourceScale, 2, 0.5f) + 1f) / 2f; // normalize to [0, 1]
                    
                    var resources = BiomeInfo.GetResources(biome);
                    foreach (var (resource, threshold) in resources)
                    {
                        if (resourceVal >= threshold)
                        {
                            tile = resource;
                            break;
                        }
                    }
                }
                
                chunk.Tiles[lx, ly] = tile;
            }
        }
        
        return chunk;
    }
}