namespace Asciifactory.WorldGen;

/// <summary>
/// Biome types that determine terrain and resource distribution.
/// </summary>
public enum BiomeType
{
    Plains,
    Forest,
    Desert,
    Mountains,
    Swamp,
}

public static class BiomeInfo
{
    public static (string Name, ConsoleColor Color) GetInfo(BiomeType biome) => biome switch
    {
        BiomeType.Plains    => ("Plains", ConsoleColor.Green),
        BiomeType.Forest    => ("Forest", ConsoleColor.DarkGreen),
        BiomeType.Desert    => ("Desert", ConsoleColor.DarkYellow),
        BiomeType.Mountains => ("Mountains", ConsoleColor.Gray),
        BiomeType.Swamp     => ("Swamp", ConsoleColor.DarkCyan),
        _                   => ("Unknown", ConsoleColor.Red)
    };

    /// <summary>
    /// Gets the base ground tile for a biome.
    /// </summary>
    public static TileType GetBaseTile(BiomeType biome, float elevationNoise) => biome switch
    {
        BiomeType.Plains => elevationNoise switch
        {
            < -0.3f => TileType.Water,
            < 0.2f  => TileType.Grass,
            _       => TileType.Dirt
        },
        BiomeType.Forest => elevationNoise switch
        {
            < -0.3f => TileType.Water,
            < 0.1f  => TileType.Grass,
            < 0.4f  => TileType.Dirt,
            _       => TileType.Stone
        },
        BiomeType.Desert => elevationNoise switch
        {
            < -0.4f => TileType.Water,
            _       => TileType.Sand
        },
        BiomeType.Mountains => elevationNoise switch
        {
            < -0.2f => TileType.Stone,
            < 0.3f  => TileType.Stone,
            < 0.6f  => TileType.Stone,
            _       => TileType.Snow
        },
        BiomeType.Swamp => elevationNoise switch
        {
            < -0.1f => TileType.Water,
            < 0.2f  => TileType.Swamp,
            _       => TileType.Dirt
        },
        _ => TileType.Grass
    };

    /// <summary>
    /// Resource distribution probabilities per biome. Returns (resource, probability) pairs.
    /// Probability is checked against a noise value [0,1].
    /// </summary>
    public static (TileType Resource, float Threshold)[] GetResources(BiomeType biome) => biome switch
    {
        BiomeType.Plains => new[]
        {
            (TileType.IronOre, 0.72f),
            (TileType.CopperOre, 0.80f),
            (TileType.StoneDeposit, 0.86f),
            (TileType.Coal, 0.92f),
        },
        BiomeType.Forest => new[]
        {
            (TileType.Coal, 0.70f),
            (TileType.IronOre, 0.78f),
            (TileType.CopperOre, 0.86f),
            (TileType.Quartz, 0.93f),
        },
        BiomeType.Desert => new[]
        {
            (TileType.CopperOre, 0.70f),
            (TileType.StoneDeposit, 0.78f),
            (TileType.Quartz, 0.86f),
            (TileType.Oil, 0.93f),
        },
        BiomeType.Mountains => new[]
        {
            (TileType.StoneDeposit, 0.65f),
            (TileType.IronOre, 0.74f),
            (TileType.Coal, 0.82f),
            (TileType.Quartz, 0.90f),
            (TileType.Uranium, 0.96f),
        },
        BiomeType.Swamp => new[]
        {
            (TileType.Coal, 0.68f),
            (TileType.Oil, 0.78f),
            (TileType.CopperOre, 0.88f),
            (TileType.Uranium, 0.95f),
        },
        _ => Array.Empty<(TileType, float)>()
    };
}