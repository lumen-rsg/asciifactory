using Asciifactory.Items;
using Asciifactory.Machines;

namespace Asciifactory.Entities;

/// <summary>
/// Enemy type enumeration.
/// </summary>
public enum EnemyType
{
    Bug,
    Glitch,
    MemoryLeak,
    Segfault,
    NullPointer,
    KernelPanic,
}

/// <summary>
/// Base class for all enemies. Enemies move toward machines/player and deal damage.
/// </summary>
public class Enemy
{
    public EnemyType Type { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; }
    public int Damage { get; }
    public int Speed { get; } // Ticks between moves
    public int MoveTimer { get; set; }
    public bool IsDead => Health <= 0;
    
    /// <summary>
    /// For Memory Leak: grows as it absorbs damage.
    /// </summary>
    public int GrowthStage { get; set; }
    
    /// <summary>
    /// For Glitch: teleport cooldown.
    /// </summary>
    public int TeleportCooldown { get; set; }
    
    /// <summary>
    /// Loot drop when killed.
    /// </summary>
    public (ItemId Id, int Count) Loot { get; }
    
    private static readonly (char Symbol, ConsoleColor Color, int HP, int Speed, int Damage, (ItemId Id, int Count) Loot)[] EnemyStats =
    {
        ('b', ConsoleColor.Green,       3,  8, 1, (ItemId.IronOre, 1)),
        ('g', ConsoleColor.Magenta,     5,  5, 2, (ItemId.CopperWire, 1)),
        ('m', ConsoleColor.DarkCyan,   10, 10, 1, (ItemId.Coal, 2)),
        ('S', ConsoleColor.Red,         8,  3, 3, (ItemId.Circuit, 1)),
        ('ø', ConsoleColor.White,       1,  2, 5, (ItemId.Quartz, 1)),
        ('K', ConsoleColor.DarkRed,    20, 15, 5, (ItemId.SteelPlate, 2)),
    };
    
    public Enemy(EnemyType type, int x, int y, int hpScale = 1)
    {
        Type = type;
        X = x;
        Y = y;
        
        int idx = (int)type;
        var (symbol, color, hp, speed, damage, loot) = EnemyStats[idx];
        
        MaxHealth = hp * hpScale;
        Health = MaxHealth;
        Speed = speed;
        Damage = damage;
        MoveTimer = speed;
        Loot = loot;
        GrowthStage = 1;
        TeleportCooldown = 0;
    }
    
    public char GetSymbol() => Type switch
    {
        EnemyType.MemoryLeak => GrowthStage switch
        {
            1 => 'm',
            2 => 'M',
            3 => '█',
            _ => '█',
        },
        _ => EnemyStats[(int)Type].Symbol,
    };
    
    public ConsoleColor GetColor() => Type switch
    {
        EnemyType.MemoryLeak => GrowthStage switch
        {
            1 => ConsoleColor.DarkCyan,
            2 => ConsoleColor.Cyan,
            3 => ConsoleColor.Yellow,
            _ => ConsoleColor.Yellow,
        },
        EnemyType.KernelPanic => ConsoleColor.DarkRed,
        _ => EnemyStats[(int)Type].Color,
    };
    
    /// <summary>
    /// Gets the minimum machine count required for this enemy type to spawn.
    /// </summary>
    public static int GetMinMachines(EnemyType type) => type switch
    {
        EnemyType.Bug => 0,
        EnemyType.Glitch => 5,
        EnemyType.MemoryLeak => 15,
        EnemyType.Segfault => 15,
        EnemyType.NullPointer => 30,
        EnemyType.KernelPanic => 50,
        _ => 100,
    };
    
    /// <summary>
    /// Takes damage. Returns true if killed.
    /// </summary>
    public bool TakeDamage(int amount)
    {
        Health -= amount;
        
        // Memory Leak grows when damaged
        if (Type == EnemyType.MemoryLeak && !IsDead)
        {
            float pct = 1f - (float)Health / MaxHealth;
            GrowthStage = pct switch
            {
                < 0.33f => 1,
                < 0.66f => 2,
                _ => 3,
            };
        }
        
        return IsDead;
    }
    
    /// <summary>
    /// Chooses a move direction. Basic AI: move toward nearest machine or player.
    /// </summary>
    public (int dx, int dy) ChooseMove(int playerX, int playerY, MachineGrid machineGrid)
    {
        // Find nearest machine
        MachineBase? nearest = null;
        int bestDist = int.MaxValue;
        
        foreach (var machine in machineGrid.GetAllMachines())
        {
            int dist = Math.Abs(machine.X - X) + Math.Abs(machine.Y - Y);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = machine;
            }
        }
        
        // Also consider player distance
        int playerDist = Math.Abs(playerX - X) + Math.Abs(playerY - Y);
        
        int targetX, targetY;
        if (nearest != null && bestDist <= playerDist)
        {
            targetX = nearest.X;
            targetY = nearest.Y;
        }
        else
        {
            targetX = playerX;
            targetY = playerY;
        }
        
        // Segfault: rush in straight line
        if (Type == EnemyType.Segfault)
        {
            int adx = Math.Sign(targetX - X);
            int ady = Math.Sign(targetY - Y);
            if (adx != 0 && ady != 0)
            {
                // Pick dominant axis
                if (Math.Abs(targetX - X) > Math.Abs(targetY - Y))
                    ady = 0;
                else
                    adx = 0;
            }
            return (adx, ady);
        }
        
        // Normal movement: step toward target
        int dx = Math.Sign(targetX - X);
        int dy = Math.Sign(targetY - Y);
        
        // Random wobble for bugs
        if (Type == EnemyType.Bug && Random.Shared.Next(3) == 0)
        {
            dx = Random.Shared.Next(-1, 2);
            dy = Random.Shared.Next(-1, 2);
        }
        
        return (dx, dy);
    }
}

/// <summary>
/// A projectile fired by a Firewall Turret.
/// </summary>
public class Projectile
{
    public int X { get; set; }
    public int Y { get; set; }
    public int DX { get; }
    public int DY { get; }
    public int Damage { get; }
    public int Lifetime { get; set; } // Ticks before disappearing
    public bool IsDead => Lifetime <= 0;
    
    public Projectile(int x, int y, int dx, int dy, int damage = 2, int lifetime = 15)
    {
        X = x;
        Y = y;
        DX = dx;
        DY = dy;
        Damage = damage;
        Lifetime = lifetime;
    }
    
    public void Move()
    {
        X += DX;
        Y += DY;
        Lifetime--;
    }
}