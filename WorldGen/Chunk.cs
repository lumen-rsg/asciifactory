namespace Asciifactory.WorldGen;

/// <summary>
/// A 32x32 chunk of tiles in the infinite world.
/// Chunks are identified by their grid coordinates (not world coordinates).
/// </summary>
public class Chunk
{
    public const int Size = 32;
    
    public int ChunkX { get; }
    public int ChunkY { get; }
    
    public TileType[,] Tiles { get; }
    public BiomeType Biome { get; }
    
    public Chunk(int chunkX, int chunkY, BiomeType biome)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        Biome = biome;
        Tiles = new TileType[Size, Size];
    }
    
    /// <summary>
    /// Convert chunk-local coordinates to world coordinates.
    /// </summary>
    public (int WorldX, int WorldY) LocalToWorld(int localX, int localY)
    {
        return (ChunkX * Size + localX, ChunkY * Size + localY);
    }
    
    /// <summary>
    /// Convert world coordinates to chunk-local coordinates.
    /// </summary>
    public static (int LocalX, int LocalY) WorldToLocal(int worldX, int worldY)
    {
        int localX = ((worldX % Size) + Size) % Size;
        int localY = ((worldY % Size) + Size) % Size;
        return (localX, localY);
    }
    
    /// <summary>
    /// Get the chunk coordinate for a given world position.
    /// </summary>
    public static (int ChunkX, int ChunkY) WorldToChunk(int worldX, int worldY)
    {
        // Floor division for negative coordinates
        int cx = worldX >= 0 ? worldX / Size : (worldX - Size + 1) / Size;
        int cy = worldY >= 0 ? worldY / Size : (worldY - Size + 1) / Size;
        return (cx, cy);
    }
}