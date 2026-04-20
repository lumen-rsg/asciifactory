using Asciifactory.Items;
using Asciifactory.Network;
using Asciifactory.WorldGen;

namespace Asciifactory.Entities;

/// <summary>
/// The player entity. Moves around the infinite world, mines resources, and builds machines.
/// </summary>
public class Player
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Health { get; set; } = 100;
    public int MaxHealth { get; } = 100;
    public Inventory Inventory { get; } = new(32);
    public int DamageFlash { get; set; }
    
    /// <summary>God mode: no damage, instant mining, infinite items.</summary>
    public bool GodMode { get; set; }
    
    /// <summary>Multiplayer: player index (0 = host / singleplayer).</summary>
    public int PlayerIndex { get; set; }
    
    /// <summary>Multiplayer: player nickname.</summary>
    public string Nickname { get; set; } = "Player";
    
    /// <summary>Multiplayer: player color.</summary>
    public PlayerColor PlayerColor { get; set; } = PlayerColor.Yellow;
    
    /// <summary>
    /// Take damage from an enemy. In god mode, damage is ignored.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (GodMode) return;
        Health -= amount;
        DamageFlash = 5;
        if (Health < 0) Health = 0;
    }
    
    /// <summary>
    /// The direction the player is facing (for mining/interacting).
    /// </summary>
    public int FacingX { get; internal set; } = 0;
    public int FacingY { get; internal set; } = 1; // Default facing down
    
    /// <summary>
    /// Progress toward mining the current tile (0-100).
    /// </summary>
    public int MiningProgress { get; set; }
    public bool IsMining { get; set; }
    
    // Mining target
    public int MiningTargetX { get; set; }
    public int MiningTargetY { get; set; }
    
    private const int MiningSpeed = 20; // Mining progress per tick while holding space
    public int MiningAnimFrame { get; set; } // Cycles each tick for animation
    public int MiningSplashTimer { get; set; } // Countdown for splash effect on completion
    public ItemId? LastMinedItem { get; set; } // What was just mined (for splash)
    
    public Player(int startX, int startY)
    {
        X = startX;
        Y = startY;
    }
    
    /// <summary>
    /// Attempts to move the player. Turn-then-walk: first press turns,
    /// second press of same direction moves. Returns (turned, moved).
    /// </summary>
    public (bool turned, bool moved) TryMove(int dx, int dy, Func<int, int, bool> isWalkable)
    {
        if (dx == 0 && dy == 0) return (false, false);
        
        // If pressing a different direction than facing, just turn
        if (dx != FacingX || dy != FacingY)
        {
            FacingX = dx;
            FacingY = dy;
            return (true, false);
        }
        
        // Same direction — try to move
        int nx = X + dx;
        int ny = Y + dy;
        
        if (isWalkable(nx, ny))
        {
            X = nx;
            Y = ny;
            IsMining = false;
            MiningProgress = 0;
            return (false, true);
        }
        
        return (false, false);
    }
    
    /// <summary>
    /// Gets the world coordinates of the tile the player is facing.
    /// </summary>
    public (int X, int Y) GetFacingTile() => (X + FacingX, Y + FacingY);
    
    /// <summary>
    /// Ticks the mining process. Returns the mined item if mining completed, null otherwise.
    /// </summary>
    public ItemId? TickMining(Func<int, int, TileType> getTile, Action<int, int, TileType> setTile)
    {
        var (fx, fy) = GetFacingTile();
        var tile = getTile(fx, fy);
        var itemId = ItemRegistry.GetItemFromTile(tile);
        
        if (itemId == null)
        {
            IsMining = false;
            MiningProgress = 0;
            return null;
        }
        
        // If target changed, reset progress
        if (fx != MiningTargetX || fy != MiningTargetY)
        {
            MiningTargetX = fx;
            MiningTargetY = fy;
            MiningProgress = 0;
        }
        
        IsMining = true;
        MiningProgress += MiningSpeed;
        
        if (MiningProgress >= 100)
        {
            MiningProgress = 0;
            
            // Add item to inventory
            int overflow = Inventory.AddItem(itemId.Value, 1);
            
            // Deplete the resource tile (turn it into the ground beneath)
            if (overflow == 0)
            {
                // Turn resource into appropriate ground tile
                setTile(fx, fy, ItemRegistry.GetMinedTileReplacement(tile));
            }
            
            return itemId.Value;
        }
        
        return null;
    }
}