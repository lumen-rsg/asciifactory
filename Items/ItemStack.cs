namespace Asciifactory.Items;

/// <summary>
/// A stack of a single item type with quantity.
/// </summary>
public class ItemStack
{
    public ItemId Id { get; set; }
    public int Count { get; set; }
    
    public bool IsEmpty => Count <= 0;
    
    public ItemStack(ItemId id, int count = 1)
    {
        Id = id;
        Count = count;
    }
    
    /// <summary>
    /// Returns how many more items can fit in this stack.
    /// </summary>
    public int RemainingSpace => ItemRegistry.GetMaxStack(Id) - Count;
    
    /// <summary>
    /// Tries to add items to this stack. Returns the number that couldn't be added.
    /// </summary>
    public int Add(int amount)
    {
        int maxStack = ItemRegistry.GetMaxStack(Id);
        int canAdd = Math.Min(amount, maxStack - Count);
        Count += canAdd;
        return amount - canAdd;
    }
    
    /// <summary>
    /// Removes items from this stack. Returns the number actually removed.
    /// </summary>
    public int Remove(int amount)
    {
        int removed = Math.Min(amount, Count);
        Count -= removed;
        return removed;
    }
    
    public override string ToString() => Count > 0 
        ? $"{ItemRegistry.GetName(Id)} x{Count}" 
        : "(empty)";
}