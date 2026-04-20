namespace Asciifactory.Items;

/// <summary>
/// Slot-based inventory for the player or a storage container.
/// </summary>
public class Inventory
{
    private readonly ItemStack?[] _slots;
    
    public int SlotCount { get; }
    
    public Inventory(int slots = 32)
    {
        SlotCount = slots;
        _slots = new ItemStack?[slots];
    }
    
    /// <summary>
    /// Gets the item stack in a slot (may be null).
    /// </summary>
    public ItemStack? GetSlot(int index) => index >= 0 && index < SlotCount ? _slots[index] : null;
    
    /// <summary>
    /// Gets all non-empty slots.
    /// </summary>
    public IEnumerable<(int Index, ItemStack Stack)> GetFilledSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] is { IsEmpty: false } stack)
                yield return (i, stack);
        }
    }
    
    /// <summary>
    /// Tries to add items to the inventory. Returns the number that couldn't be added.
    /// First tries to stack with existing items, then uses empty slots.
    /// </summary>
    public int AddItem(ItemId id, int count)
    {
        int remaining = count;
        
        // First, try to add to existing stacks
        foreach (var (_, stack) in GetFilledSlots())
        {
            if (stack.Id == id && remaining > 0)
            {
                remaining = stack.Add(remaining);
            }
        }
        
        // Then, use empty slots
        for (int i = 0; i < SlotCount && remaining > 0; i++)
        {
            if (_slots[i] == null || _slots[i]!.IsEmpty)
            {
                int maxStack = ItemRegistry.GetMaxStack(id);
                int toAdd = Math.Min(remaining, maxStack);
                _slots[i] = new ItemStack(id, toAdd);
                remaining -= toAdd;
            }
        }
        
        return remaining;
    }
    
    /// <summary>
    /// Tries to remove items from the inventory. Returns true if successful.
    /// </summary>
    public bool RemoveItem(ItemId id, int count)
    {
        if (!HasItem(id, count)) return false;
        
        int remaining = count;
        for (int i = SlotCount - 1; i >= 0 && remaining > 0; i--)
        {
            if (_slots[i] is { IsEmpty: false } stack && stack.Id == id)
            {
                int removed = stack.Remove(remaining);
                remaining -= removed;
                if (stack.IsEmpty) _slots[i] = null;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Checks if the inventory has at least the specified amount of an item.
    /// </summary>
    public bool HasItem(ItemId id, int count = 1)
    {
        int total = 0;
        foreach (var (_, stack) in GetFilledSlots())
        {
            if (stack.Id == id)
            {
                total += stack.Count;
                if (total >= count) return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Gets the total count of a specific item.
    /// </summary>
    public int GetCount(ItemId id)
    {
        int total = 0;
        foreach (var (_, stack) in GetFilledSlots())
        {
            if (stack.Id == id) total += stack.Count;
        }
        return total;
    }
    
    /// <summary>
    /// Checks if the inventory has all the specified ingredients.
    /// </summary>
    public bool HasIngredients(IEnumerable<(ItemId Id, int Count)> ingredients)
    {
        return ingredients.All(ing => HasItem(ing.Id, ing.Count));
    }
    
    /// <summary>
    /// Removes all specified ingredients (assumes HasIngredients was checked).
    /// </summary>
    public void RemoveIngredients(IEnumerable<(ItemId Id, int Count)> ingredients)
    {
        foreach (var (id, count) in ingredients)
        {
            RemoveItem(id, count);
        }
    }
}