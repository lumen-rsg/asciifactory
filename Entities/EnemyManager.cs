using Asciifactory.Items;
using Asciifactory.Machines;
using Asciifactory.WorldGen;

namespace Asciifactory.Entities;

/// <summary>
/// Manages all enemies and projectiles. Handles spawning (scaled by factory size),
/// movement, collision, and cleanup.
/// </summary>
public class EnemyManager
{
    private readonly List<Enemy> _enemies = new();
    private readonly List<Projectile> _projectiles = new();
    
    public IReadOnlyList<Enemy> Enemies => _enemies;
    public IReadOnlyList<Projectile> Projectiles => _projectiles;
    public int EnemyCount => _enemies.Count;
    
    private int _spawnTimer;
    private const int BaseSpawnInterval = 300; // ~15 seconds at 50ms tick
    
    /// <summary>
    /// Tick all enemies and projectiles. Returns killed enemies for loot.
    /// </summary>
    public List<Enemy> Tick(Player player, MachineGrid machineGrid, World world)
    {
        var killed = new List<Enemy>();
        
        // Tick enemies
        foreach (var enemy in _enemies.ToList())
        {
            if (enemy.IsDead)
            {
                killed.Add(enemy);
                _enemies.Remove(enemy);
                continue;
            }
            
            enemy.MoveTimer--;
            if (enemy.MoveTimer <= 0)
            {
                enemy.MoveTimer = enemy.Speed;
                
                // Glitch: teleport occasionally
                if (enemy.Type == EnemyType.Glitch)
                {
                    enemy.TeleportCooldown--;
                    if (enemy.TeleportCooldown <= 0 && Random.Shared.Next(4) == 0)
                    {
                        // Teleport 3-8 tiles in random direction
                        int dist = Random.Shared.Next(3, 9);
                        int nx = enemy.X + Random.Shared.Next(-dist, dist + 1);
                        int ny = enemy.Y + Random.Shared.Next(-dist, dist + 1);
                        if (TileTypeInfo.IsWalkable(world.GetTile(nx, ny)))
                        {
                            enemy.X = nx;
                            enemy.Y = ny;
                        }
                        enemy.TeleportCooldown = 10;
                    }
                }
                
                // Normal move
                var (dx, dy) = enemy.ChooseMove(player.X, player.Y, machineGrid);
                int nx2 = enemy.X + dx;
                int ny2 = enemy.Y + dy;
                
                if (dx != 0 || dy != 0)
                {
                    // Check collision with player
                    if (nx2 == player.X && ny2 == player.Y)
                    {
                        player.TakeDamage(enemy.Damage);
                    }
                    // Check collision with machine
                    else if (machineGrid.GetMachineAt(nx2, ny2) is { } machine)
                    {
                        // Attack machine — reduce its type (we'll handle machine damage)
                        AttackMachine(machine, enemy);
                    }
                    else if (TileTypeInfo.IsWalkable(world.GetTile(nx2, ny2)))
                    {
                        enemy.X = nx2;
                        enemy.Y = ny2;
                    }
                    // Can't move there (wall/water/machine) — stay put
                }
            }
        }
        
        // Tick projectiles
        foreach (var proj in _projectiles.ToList())
        {
            proj.Move();
            
            if (proj.IsDead)
            {
                _projectiles.Remove(proj);
                continue;
            }
            
            // Check collision with enemies
            foreach (var enemy in _enemies.ToList())
            {
                if (enemy.X == proj.X && enemy.Y == proj.Y)
                {
                    bool died = enemy.TakeDamage(proj.Damage);
                    proj.Lifetime = 0; // Projectile consumed
                    
                    if (died)
                    {
                        killed.Add(enemy);
                        _enemies.Remove(enemy);
                    }
                    break;
                }
            }
        }
        
        return killed;
    }
    
    /// <summary>
    /// Enemy attacks a machine. Simple damage: mark it for removal if hit enough.
    /// </summary>
    private void AttackMachine(MachineBase machine, Enemy enemy)
    {
        // For simplicity: enemies damage machines by reducing a hidden HP
        // We'll add MachineDamage tracking
        machine.TakeDamage(enemy.Damage);
    }
    
    /// <summary>
    /// Spawns enemies based on factory size. Called each tick.
    /// </summary>
    public void TickSpawning(int playerX, int playerY, int machineCount, World world)
    {
        _spawnTimer--;
        if (_spawnTimer > 0) return;
        
        // Spawn interval decreases with more machines
        int interval = Math.Max(60, BaseSpawnInterval - machineCount * 5);
        _spawnTimer = interval;
        
        // Max enemies scale with factory size
        int maxEnemies = Math.Max(3, machineCount / 3);
        if (_enemies.Count >= maxEnemies) return;
        
        // Determine which enemy types can spawn
        var available = new List<EnemyType>();
        foreach (EnemyType type in Enum.GetValues<EnemyType>())
        {
            if (machineCount >= Enemy.GetMinMachines(type))
                available.Add(type);
        }
        
        if (available.Count == 0) return;
        
        // Pick random type (weighted toward weaker enemies)
        EnemyType chosen = available[Random.Shared.Next(available.Count)];
        
        // HP scales with factory size
        int hpScale = 1 + machineCount / 50;
        
        // Spawn at random position 20-35 tiles from player
        int dist = Random.Shared.Next(20, 36);
        double angle = Random.Shared.NextDouble() * Math.PI * 2;
        int sx = playerX + (int)(Math.Cos(angle) * dist);
        int sy = playerY + (int)(Math.Sin(angle) * dist);
        
        // Make sure spawn is on walkable ground
        if (TileTypeInfo.IsWalkable(world.GetTile(sx, sy)))
        {
            _enemies.Add(new Enemy(chosen, sx, sy, hpScale));
        }
    }
    
    /// <summary>
    /// Spawns a projectile from a turret.
    /// </summary>
    public void FireProjectile(int x, int y, int dx, int dy, int damage = 2)
    {
        _projectiles.Add(new Projectile(x, y, dx, dy, damage));
    }
    
    /// <summary>
    /// Handles the player's laser hitting an enemy at the facing tile.
    /// Returns the enemy hit (or null).
    /// </summary>
    public Enemy? LaserHitEnemy(int targetX, int targetY, int damage)
    {
        foreach (var enemy in _enemies)
        {
            if (enemy.X == targetX && enemy.Y == targetY)
            {
                enemy.TakeDamage(damage);
                return enemy;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Clears all enemies and projectiles.
    /// </summary>
    public void Clear()
    {
        _enemies.Clear();
        _projectiles.Clear();
    }
}