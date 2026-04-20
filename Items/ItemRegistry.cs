namespace Asciifactory.Items;

/// <summary>
/// All item IDs in the game. Each maps to display info and stack size.
/// </summary>
public enum ItemId
{
    // Raw resources (from mining)
    IronOre,
    CopperOre,
    Coal,
    Stone,
    Oil,
    Quartz,
    Uranium,
    Biomass,
    
    // Smelted materials
    IronPlate,
    CopperPlate,
    SteelPlate,
    Silicon,
    Glass,
    Brick,
    
    // Processed materials
    CopperWire,
    Plastic,
    Fiber,
    Fuel,
    
    // Components
    Gear,
    Circuit,
    Processor,
    MemoryModule,
    Motherboard,
    Monitor,
    Keyboard,
    PowerSupply,
    ComputerCase,
    
    // Science packs
    SciencePack1,
    SciencePack2,
    SciencePack3,
    SciencePack4,
    
    // Tools / Equipment
    CraftingTable,
    
    // The goal
    Computer9000,
}

public static class ItemRegistry
{
    private readonly record struct ItemInfo(string Name, char Symbol, ConsoleColor Color, int MaxStack);
    
    private static readonly Dictionary<ItemId, ItemInfo> Items = new()
    {
        // Raw resources
        { ItemId.IronOre,    new("Iron Ore",    'i', ConsoleColor.White,      100) },
        { ItemId.CopperOre,  new("Copper Ore",  'c', ConsoleColor.DarkYellow, 100) },
        { ItemId.Coal,       new("Coal",        '*', ConsoleColor.DarkGray,   100) },
        { ItemId.Stone,      new("Stone",       'o', ConsoleColor.Gray,       100) },
        { ItemId.Oil,        new("Crude Oil",   '=', ConsoleColor.Black,      100) },
        { ItemId.Quartz,     new("Quartz",      'q', ConsoleColor.Cyan,       100) },
        { ItemId.Uranium,    new("Uranium",     'u', ConsoleColor.Green,      50) },
        { ItemId.Biomass,    new("Biomass",     '~', ConsoleColor.DarkGreen,  100) },
        
        // Smelted materials
        { ItemId.IronPlate,  new("Iron Plate",  'I', ConsoleColor.White,      200) },
        { ItemId.CopperPlate,new("Copper Plate",'C', ConsoleColor.DarkYellow, 200) },
        { ItemId.SteelPlate, new("Steel Plate", 'S', ConsoleColor.Gray,       200) },
        { ItemId.Silicon,    new("Silicon",     's', ConsoleColor.Cyan,       200) },
        { ItemId.Glass,      new("Glass",       'g', ConsoleColor.Cyan,       200) },
        { ItemId.Brick,      new("Brick",       'b', ConsoleColor.DarkRed,    200) },
        
        // Processed materials
        { ItemId.CopperWire, new("Copper Wire", 'w', ConsoleColor.DarkYellow, 400) },
        { ItemId.Plastic,    new("Plastic",     'p', ConsoleColor.Magenta,    200) },
        { ItemId.Fiber,      new("Fiber",       'f', ConsoleColor.DarkGreen,  200) },
        { ItemId.Fuel,       new("Fuel",        'F', ConsoleColor.DarkYellow, 100) },
        
        // Components
        { ItemId.Gear,        new("Gear",         'G', ConsoleColor.Gray,       200) },
        { ItemId.Circuit,     new("Circuit",      'z', ConsoleColor.Green,      200) },
        { ItemId.Processor,   new("Processor",    'P', ConsoleColor.Green,      100) },
        { ItemId.MemoryModule,new("Memory Module", 'M', ConsoleColor.Blue,      100) },
        { ItemId.Motherboard, new("Motherboard",  'B', ConsoleColor.DarkGreen,  50) },
        { ItemId.Monitor,     new("Monitor",      'm', ConsoleColor.Cyan,       50) },
        { ItemId.Keyboard,    new("Keyboard",     'k', ConsoleColor.DarkGray,   50) },
        { ItemId.PowerSupply, new("Power Supply",  'X', ConsoleColor.Yellow,     50) },
        { ItemId.ComputerCase,new("Computer Case", 'K', ConsoleColor.Gray,      50) },
        
        // Science packs
        { ItemId.SciencePack1, new("Science Pack 1", '1', ConsoleColor.Red,       200) },
        { ItemId.SciencePack2, new("Science Pack 2", '2', ConsoleColor.Green,     200) },
        { ItemId.SciencePack3, new("Science Pack 3", '3', ConsoleColor.Blue,      200) },
        { ItemId.SciencePack4, new("Science Pack 4", '4', ConsoleColor.Magenta,   200) },
        
        // Tools / Equipment
        { ItemId.CraftingTable, new("Crafting Table", 'T', ConsoleColor.DarkCyan, 1) },
        
        // The goal
        { ItemId.Computer9000, new("THE COMPUTER 9000™", '█', ConsoleColor.Yellow, 1) },
    };
    
    public static string GetName(ItemId id) => Items[id].Name;
    public static char GetSymbol(ItemId id) => Items[id].Symbol;
    public static ConsoleColor GetColor(ItemId id) => Items[id].Color;
    public static int GetMaxStack(ItemId id) => Items[id].MaxStack;
    
    /// <summary>
    /// Maps a world tile resource to the item you get from mining it.
    /// </summary>
    public static ItemId? GetItemFromTile(WorldGen.TileType tile) => tile switch
    {
        WorldGen.TileType.IronOre      => ItemId.IronOre,
        WorldGen.TileType.CopperOre    => ItemId.CopperOre,
        WorldGen.TileType.Coal         => ItemId.Coal,
        WorldGen.TileType.StoneDeposit => ItemId.Stone,
        WorldGen.TileType.Oil          => ItemId.Oil,
        WorldGen.TileType.Quartz       => ItemId.Quartz,
        WorldGen.TileType.Uranium      => ItemId.Uranium,
        WorldGen.TileType.Grass        => ItemId.Biomass,
        WorldGen.TileType.Dirt         => ItemId.Biomass,
        _ => null
    };
    
    /// <summary>
    /// Gets the tile type that remains after mining a resource tile.
    /// Grass→Dirt, Dirt→Sand, resources→Grass.
    /// </summary>
    public static WorldGen.TileType GetMinedTileReplacement(WorldGen.TileType tile) => tile switch
    {
        WorldGen.TileType.Grass => WorldGen.TileType.Dirt,
        WorldGen.TileType.Dirt => WorldGen.TileType.Sand,
        _ => WorldGen.TileType.Grass
    };
}