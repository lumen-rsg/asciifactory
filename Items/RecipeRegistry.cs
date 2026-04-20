namespace Asciifactory.Items;

/// <summary>
/// A crafting recipe: inputs → output with crafting duration.
/// </summary>
public record Recipe(string Name, ItemId Output, int OutputCount, (ItemId Id, int Count)[] Inputs, float CraftTime, RecipeCategory Category);

public enum RecipeCategory
{
    Smelting,
    Assembly,
    Chemical,
    Manual,      // Hand-crafted (no machine needed)
    CraftingTable, // Requires Crafting Table item in inventory
    Research,    // Lab-only recipes (science packs)
}

public static class RecipeRegistry
{
    private static readonly List<Recipe> Recipes = new()
    {
        // === Manual crafting (no machine needed) ===
        new("Brick",           ItemId.Brick,        1, new[] { (ItemId.Stone, 2) },                                            1.0f, RecipeCategory.Manual),
        new("Crafting Table",  ItemId.CraftingTable,1, new[] { (ItemId.Stone, 5), (ItemId.IronOre, 3) },                       2.0f, RecipeCategory.Manual),
        new("Hand-Spin Wire",  ItemId.CopperWire,   2, new[] { (ItemId.CopperOre, 2) },                                        2.0f, RecipeCategory.Manual),
        new("Compost Biomass", ItemId.Biomass,      2, new[] { (ItemId.Stone, 2), (ItemId.IronPlate, 1) },                     3.0f, RecipeCategory.Manual),
        
        // === Crafting Table (requires CraftingTable item) ===
        new("Crude Iron Plate",  ItemId.IronPlate,   1, new[] { (ItemId.IronOre, 2), (ItemId.Coal, 1) },                       3.0f, RecipeCategory.CraftingTable),
        new("Crude Copper Plate",ItemId.CopperPlate,1, new[] { (ItemId.CopperOre, 2), (ItemId.Coal, 1) },                        3.0f, RecipeCategory.CraftingTable),
        new("Crude Gear",        ItemId.Gear,        1, new[] { (ItemId.IronOre, 3) },                                          4.0f, RecipeCategory.CraftingTable),
        new("Basic Wire",        ItemId.CopperWire,  2, new[] { (ItemId.CopperOre, 2) },                                        2.0f, RecipeCategory.CraftingTable),
        
        // === Smelting (Furnace) ===
        new("Iron Plate",      ItemId.IronPlate,    1, new[] { (ItemId.IronOre, 1), (ItemId.Coal, 1) },                        3.0f, RecipeCategory.Smelting),
        new("Copper Plate",    ItemId.CopperPlate,  1, new[] { (ItemId.CopperOre, 1), (ItemId.Coal, 1) },                      3.0f, RecipeCategory.Smelting),
        new("Steel Plate",     ItemId.SteelPlate,   1, new[] { (ItemId.IronPlate, 2), (ItemId.Coal, 2) },                      5.0f, RecipeCategory.Smelting),
        new("Silicon",         ItemId.Silicon,      1, new[] { (ItemId.Quartz, 2), (ItemId.Coal, 1) },                          4.0f, RecipeCategory.Smelting),
        new("Glass",           ItemId.Glass,        1, new[] { (ItemId.Quartz, 1) },                                            3.0f, RecipeCategory.Smelting),
        new("Brick (Furnace)", ItemId.Brick,        2, new[] { (ItemId.Stone, 2), (ItemId.Coal, 1) },                           3.0f, RecipeCategory.Smelting),
        
        // === Assembly (Assembler) ===
        new("Copper Wire",     ItemId.CopperWire,   2, new[] { (ItemId.CopperPlate, 1) },                                      1.5f, RecipeCategory.Assembly),
        new("Gear",            ItemId.Gear,         1, new[] { (ItemId.IronPlate, 2) },                                         2.0f, RecipeCategory.Assembly),
        new("Circuit",         ItemId.Circuit,      1, new[] { (ItemId.CopperWire, 3), (ItemId.IronPlate, 1) },                 3.0f, RecipeCategory.Assembly),
        new("Processor",       ItemId.Processor,    1, new[] { (ItemId.Circuit, 2), (ItemId.Silicon, 1) },                       5.0f, RecipeCategory.Assembly),
        new("Memory Module",   ItemId.MemoryModule, 1, new[] { (ItemId.Circuit, 2), (ItemId.CopperWire, 4) },                  4.0f, RecipeCategory.Assembly),
        new("Motherboard",     ItemId.Motherboard,  1, new[] { (ItemId.Processor, 1), (ItemId.MemoryModule, 2), (ItemId.Fiber, 2), (ItemId.Circuit, 3) }, 8.0f, RecipeCategory.Assembly),
        new("Monitor",         ItemId.Monitor,      1, new[] { (ItemId.Glass, 2), (ItemId.Circuit, 2), (ItemId.IronPlate, 3) }, 6.0f, RecipeCategory.Assembly),
        new("Keyboard",        ItemId.Keyboard,     1, new[] { (ItemId.Plastic, 3), (ItemId.Circuit, 1) },                      4.0f, RecipeCategory.Assembly),
        new("Power Supply",    ItemId.PowerSupply,  1, new[] { (ItemId.SteelPlate, 2), (ItemId.CopperWire, 5), (ItemId.Uranium, 1) }, 8.0f, RecipeCategory.Assembly),
        new("Computer Case",   ItemId.ComputerCase, 1, new[] { (ItemId.SteelPlate, 5), (ItemId.Gear, 2) },                     5.0f, RecipeCategory.Assembly),
        
        // Science packs
        new("Science Pack 1",  ItemId.SciencePack1, 1, new[] { (ItemId.IronPlate, 1), (ItemId.Gear, 1) },                      5.0f, RecipeCategory.Assembly),
        new("Science Pack 2",  ItemId.SciencePack2, 1, new[] { (ItemId.IronPlate, 1), (ItemId.Circuit, 1) },                   8.0f, RecipeCategory.Assembly),
        new("Science Pack 3",  ItemId.SciencePack3, 1, new[] { (ItemId.Plastic, 1), (ItemId.Processor, 1) },                   12.0f, RecipeCategory.Assembly),
        new("Science Pack 4",  ItemId.SciencePack4, 1, new[] { (ItemId.Motherboard, 1), (ItemId.MemoryModule, 1) },            20.0f, RecipeCategory.Assembly),
        
        // === Chemical (Refinery) ===
        new("Plastic",         ItemId.Plastic,      2, new[] { (ItemId.Oil, 3) },                                               4.0f, RecipeCategory.Chemical),
        new("Fuel",            ItemId.Fuel,         1, new[] { (ItemId.Oil, 2) },                                               3.0f, RecipeCategory.Chemical),
        new("Fiber",           ItemId.Fiber,        1, new[] { (ItemId.Oil, 1), (ItemId.Plastic, 1) },                          5.0f, RecipeCategory.Chemical),
        
        // === Research (Lab) ===
        new("Lab Science 1",   ItemId.SciencePack1, 2, new[] { (ItemId.IronPlate, 2), (ItemId.Gear, 1) },                      8.0f, RecipeCategory.Research),
        new("Lab Science 2",   ItemId.SciencePack2, 2, new[] { (ItemId.IronPlate, 1), (ItemId.Circuit, 2) },                   12.0f, RecipeCategory.Research),
        new("Lab Science 3",   ItemId.SciencePack3, 1, new[] { (ItemId.Plastic, 2), (ItemId.Processor, 1) },                   18.0f, RecipeCategory.Research),
        new("Lab Science 4",   ItemId.SciencePack4, 1, new[] { (ItemId.Motherboard, 1), (ItemId.MemoryModule, 2) },            25.0f, RecipeCategory.Research),
        
        // === THE GOAL ===
        new("THE COMPUTER 9000™", ItemId.Computer9000, 1, new[] 
        { 
            (ItemId.Motherboard, 1), 
            (ItemId.Processor, 2), 
            (ItemId.MemoryModule, 4), 
            (ItemId.Monitor, 1), 
            (ItemId.Keyboard, 1), 
            (ItemId.PowerSupply, 1), 
            (ItemId.ComputerCase, 1) 
        }, 30.0f, RecipeCategory.Assembly),
    };
    
    /// <summary>
    /// Gets all recipes in a category.
    /// </summary>
    public static List<Recipe> GetByCategory(RecipeCategory category) 
        => Recipes.Where(r => r.Category == category).ToList();
    
    /// <summary>
    /// Gets all recipes.
    /// </summary>
    public static List<Recipe> GetAll() => Recipes.ToList();
    
    /// <summary>
    /// Gets recipes that can be crafted with the given inventory.
    /// </summary>
    public static List<Recipe> GetCraftable(Inventory inventory, RecipeCategory? category = null)
    {
        var recipes = category.HasValue ? GetByCategory(category.Value) : GetAll();
        return recipes.Where(r => r.Inputs.All(i => inventory.HasItem(i.Id, i.Count))).ToList();
    }
}