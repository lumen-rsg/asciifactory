namespace Asciifactory.WorldGen;

/// <summary>
/// Represents the type of terrain tile in the world.
/// </summary>
public enum TileType
{
    // Ground tiles
    Grass,
    Sand,
    Dirt,
    Stone,
    Snow,
    Water,
    Swamp,
    
    // Resource deposits (on top of ground)
    IronOre,
    CopperOre,
    Coal,
    StoneDeposit,
    Oil,
    Quartz,
    Uranium,
}

public static class TileTypeInfo
{
    public static (char Symbol, ConsoleColor Color) GetVisual(TileType type) => type switch
    {
        TileType.Grass         => ('▒', ConsoleColor.DarkGreen),
        TileType.Sand          => ('≡', ConsoleColor.DarkYellow),
        TileType.Dirt          => ('▒', ConsoleColor.DarkYellow),
        TileType.Stone         => ('▓', ConsoleColor.DarkGray),
        TileType.Snow          => ('✦', ConsoleColor.White),
        TileType.Water         => ('≋', ConsoleColor.Blue),
        TileType.Swamp         => ('≈', ConsoleColor.DarkCyan),
        
        TileType.IronOre       => ('◆', ConsoleColor.White),
        TileType.CopperOre     => ('◈', ConsoleColor.DarkYellow),
        TileType.Coal          => ('✦', ConsoleColor.DarkGray),
        TileType.StoneDeposit  => ('◉', ConsoleColor.Gray),
        TileType.Oil           => ('¤', ConsoleColor.DarkMagenta),
        TileType.Quartz        => ('◇', ConsoleColor.Cyan),
        TileType.Uranium       => ('☢', ConsoleColor.Green),
        
        _ => ('?', ConsoleColor.Red)
    };

    public static bool IsResource(TileType type) => type switch
    {
        TileType.IronOre => true,
        TileType.CopperOre => true,
        TileType.Coal => true,
        TileType.StoneDeposit => true,
        TileType.Oil => true,
        TileType.Quartz => true,
        TileType.Uranium => true,
        TileType.Grass => true,
        TileType.Dirt => true,
        _ => false
    };

    public static bool IsWalkable(TileType type) => type != TileType.Water;
}