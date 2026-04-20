using Asciifactory.Entities;
using Asciifactory.Items;
using Asciifactory.Machines;
using Asciifactory.Network;
using Asciifactory.WorldGen;

namespace Asciifactory;

/// <summary>
/// Double-buffered ASCII renderer. Draws the world, player, and HUD to the terminal.
/// Uses a back buffer to minimize flickering.
/// </summary>
public class Renderer
{
    private readonly record struct Cell(char Char, ConsoleColor ForeColor, ConsoleColor BackColor);
    
    private Cell[,] _backBuffer = null!;
    private Cell[,] _frontBuffer = null!;
    private int _width;
    private int _height;
    private int _hudHeight = 5;
    
    public int Width => _width;
    public int Height => _height;
    public int HudHeight => _hudHeight;
    
    public Renderer()
    {
        InitializeBuffers();
    }
    
    private void InitializeBuffers()
    {
        _width = Console.WindowWidth;
        _height = Console.WindowHeight;
        
        // Ensure minimum size
        _width = Math.Max(_width, 40);
        _height = Math.Max(_height, 12);
        
        _backBuffer = CreateBuffer();
        _frontBuffer = CreateBuffer();
    }
    
    private Cell[,] CreateBuffer()
    {
        var buffer = new Cell[_width, _height];
        // Initialize with spaces on black background
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                buffer[x, y] = new Cell(' ', ConsoleColor.White, ConsoleColor.Black);
        
        return buffer;
    }
    
    /// <summary>
    /// Checks if the terminal was resized and reinitializes buffers if so.
    /// </summary>
    public bool CheckResize()
    {
        if (Console.WindowWidth != _width || Console.WindowHeight != _height)
        {
            InitializeBuffers();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Clears the back buffer.
    /// </summary>
    public void Clear()
    {
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                _backBuffer[x, y] = new Cell(' ', ConsoleColor.White, ConsoleColor.Black);
    }
    
    /// <summary>
    /// Sets a cell in the back buffer at screen coordinates.
    /// </summary>
    public void SetCell(int x, int y, char ch, ConsoleColor foreColor = ConsoleColor.White, ConsoleColor backColor = ConsoleColor.Black)
    {
        if (x >= 0 && x < _width && y >= 0 && y < _height)
        {
            _backBuffer[x, y] = new Cell(ch, foreColor, backColor);
        }
    }
    
    /// <summary>
    /// Sets only the foreground character/color of a cell, preserving the existing background.
    /// Used for transparent overlays like power wires.
    /// </summary>
    public void SetCellOverlay(int x, int y, char ch, ConsoleColor foreColor)
    {
        if (x >= 0 && x < _width && y >= 0 && y < _height)
        {
            var existing = _backBuffer[x, y];
            _backBuffer[x, y] = new Cell(ch, foreColor, existing.BackColor);
        }
    }
    
    /// <summary>
    /// Draws a string into the back buffer starting at the given position.
    /// </summary>
    public void DrawString(int x, int y, string text, ConsoleColor foreColor = ConsoleColor.White, ConsoleColor backColor = ConsoleColor.Black)
    {
        for (int i = 0; i < text.Length; i++)
        {
            SetCell(x + i, y, text[i], foreColor, backColor);
        }
    }
    
    /// <summary>
    /// Draws the visible portion of the world into the back buffer.
    /// </summary>
    public void DrawWorld(World world, Camera camera)
    {
        var (startX, startY) = camera.GetTopLeft();
        int viewH = _height - _hudHeight; // Reserve bottom for HUD
        
        for (int sy = 0; sy < viewH; sy++)
        {
            for (int sx = 0; sx < _width; sx++)
            {
                int wx = startX + sx;
                int wy = startY + sy;
                
                var tile = world.GetTile(wx, wy);
                var (symbol, color) = TileTypeInfo.GetVisual(tile);
                
                SetCell(sx, sy, symbol, color);
            }
        }
    }
    
    /// <summary>
    /// Draws the player character at the correct screen position.
    /// Shows facing direction with arrow characters.
    /// </summary>
    public void DrawPlayer(int playerWorldX, int playerWorldY, int facingX, int facingY, Camera camera)
    {
        var screenPos = camera.WorldToScreen(playerWorldX, playerWorldY);
        if (screenPos.HasValue)
        {
            var (sx, sy) = screenPos.Value;
            if (sy < _height - _hudHeight) // Don't draw over HUD
            {
                char playerChar = (facingX, facingY) switch
                {
                    (0, -1) => '▲',  // Up
                    (0, 1)  => '▼',  // Down
                    (-1, 0) => '◄',  // Left
                    (1, 0)  => '►',  // Right
                    _       => '●',  // Default (no direction)
                };
                SetCell(sx, sy, playerChar, ConsoleColor.Yellow);
            }
        }
    }
    
    /// <summary>
    /// Gets the console color for a player color enum.
    /// </summary>
    public static ConsoleColor GetColorForPlayer(PlayerColor color) => color switch
    {
        PlayerColor.Yellow => ConsoleColor.Yellow,
        PlayerColor.Cyan => ConsoleColor.Cyan,
        PlayerColor.Green => ConsoleColor.Green,
        PlayerColor.Magenta => ConsoleColor.Magenta,
        _ => ConsoleColor.Yellow,
    };
    
    /// <summary>
    /// Draws a player (local or remote) at their world position with color and nickname.
    /// </summary>
    public void DrawPlayerColored(int playerWorldX, int playerWorldY, int facingX, int facingY,
        PlayerColor playerColor, string nickname, Camera camera, bool isLocalPlayer = false)
    {
        var screenPos = camera.WorldToScreen(playerWorldX, playerWorldY);
        if (!screenPos.HasValue) return;
        
        var (sx, sy) = screenPos.Value;
        if (sy >= _height - _hudHeight) return;
        
        ConsoleColor color = GetColorForPlayer(playerColor);
        
        char playerChar = (facingX, facingY) switch
        {
            (0, -1) => '▲',
            (0, 1)  => '▼',
            (-1, 0) => '◄',
            (1, 0)  => '►',
            _       => '●',
        };
        
        SetCell(sx, sy, playerChar, color);
        
        // Draw nickname above player (if space allows)
        if (!string.IsNullOrEmpty(nickname) && sy > 0)
        {
            int nameX = sx - nickname.Length / 2;
            for (int i = 0; i < nickname.Length; i++)
            {
                if (nameX + i >= 0 && nameX + i < _width)
                    SetCell(nameX + i, sy - 1, nickname[i], color);
            }
        }
    }
    
    /// <summary>
    /// Draws remote players from a game snapshot.
    /// </summary>
    public void DrawRemotePlayers(List<RemotePlayerState> remotePlayers, int localPlayerIndex, Camera camera)
    {
        foreach (var rp in remotePlayers)
        {
            if (rp.Index == localPlayerIndex) continue; // Skip local player
            DrawPlayerColored(rp.X, rp.Y, rp.FacingX, rp.FacingY, rp.Color, rp.Nickname, camera);
            
            // Draw mining laser for remote players
            if (rp.IsMining)
            {
                DrawLaserBeam(rp.X, rp.Y, rp.FacingX, rp.FacingY,
                    rp.MiningProgress, rp.MiningAnimFrame, camera);
            }
        }
    }
    
    /// <summary>
    /// Draws all visible enemies with health bars.
    /// </summary>
    public void DrawEnemies(EnemyManager enemyManager, Camera camera)
    {
        foreach (var enemy in enemyManager.Enemies)
        {
            var screenPos = camera.WorldToScreen(enemy.X, enemy.Y);
            if (!screenPos.HasValue) continue;
            
            var (sx, sy) = screenPos.Value;
            if (sy >= _height - _hudHeight || sx < 0 || sx >= _width || sy < 0) continue;
            
            char sym = enemy.GetSymbol();
            ConsoleColor color = enemy.GetColor();
            
            // Flash white when recently hit (health < max and close to max)
            SetCell(sx, sy, sym, color);
            
            // Health bar above enemy (if damaged)
            if (enemy.Health < enemy.MaxHealth)
            {
                int barWidth = 5;
                int barX = sx - barWidth / 2;
                int barY = sy - 1;
                
                if (barY >= 0 && barY < _height - _hudHeight)
                {
                    float pct = (float)enemy.Health / enemy.MaxHealth;
                    int filled = (int)(barWidth * pct);
                    
                    DrawString(barX, barY, new string('█', filled), 
                        pct > 0.5f ? ConsoleColor.Green : pct > 0.25f ? ConsoleColor.Yellow : ConsoleColor.Red);
                    DrawString(barX + filled, barY, new string('░', barWidth - filled), ConsoleColor.DarkGray);
                }
            }
        }
    }
    
    /// <summary>
    /// Draws all projectiles as fast-moving dots.
    /// </summary>
    public void DrawProjectiles(EnemyManager enemyManager, Camera camera)
    {
        foreach (var proj in enemyManager.Projectiles)
        {
            var screenPos = camera.WorldToScreen(proj.X, proj.Y);
            if (!screenPos.HasValue) continue;
            
            var (sx, sy) = screenPos.Value;
            if (sy >= _height - _hudHeight || sx < 0 || sx >= _width || sy < 0) continue;
            
            SetCell(sx, sy, '•', ConsoleColor.Yellow);
        }
    }
    
    /// <summary>
    /// Draws all visible machines from the machine grid.
    /// Uses GetTileVisuals() for multi-tile machines to render each tile individually.
    /// Wires are drawn as a transparent overlay on top of terrain.
    /// </summary>
    public void DrawMachines(MachineGrid machineGrid, Camera camera, World? world = null)
    {
        var (startX, startY) = camera.GetTopLeft();
        int viewH = _height - _hudHeight;
        
        // First pass: collect wire positions (drawn last as overlay)
        var wireOverlays = new List<(int sx, int sy, char ch, ConsoleColor color)>();
        
        // Get all distinct machines that have any part in the viewport
        var drawn = new HashSet<MachineBase>();
        
        for (int sy = 0; sy < viewH; sy++)
        {
            for (int sx = 0; sx < _width; sx++)
            {
                int wx = startX + sx;
                int wy = startY + sy;
                
                var machine = machineGrid.GetMachineAt(wx, wy);
                if (machine == null) continue;
                if (drawn.Contains(machine)) continue;
                
                // Wires are handled separately as transparent overlays
                if (machine is Wire wireMachine)
                {
                    // Get connector character based on adjacent machines
                    char wireChar = wireMachine.GetConnectorChar(machineGrid.GetMachineAt);
                    ConsoleColor wireColor = wireMachine.IsPowered ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
                    
                    // Queue wire overlay for each wire tile (wires are 1x1 but be consistent)
                    foreach (var (pos, _) in machine.GetTileVisuals())
                    {
                        var screenPos = camera.WorldToScreen(pos.x, pos.y);
                        if (screenPos.HasValue)
                        {
                            var (tsx, tsy) = screenPos.Value;
                            if (tsx >= 0 && tsx < _width && tsy >= 0 && tsy < viewH)
                            {
                                wireOverlays.Add((tsx, tsy, wireChar, wireColor));
                            }
                        }
                    }
                    
                    drawn.Add(machine);
                    continue;
                }
                
                // Draw all tiles of this machine via GetTileVisuals
                // (which returns the correct visual for each occupied tile)
                foreach (var (pos, visual) in machine.GetTileVisuals())
                {
                    var screenPos = camera.WorldToScreen(pos.x, pos.y);
                    if (screenPos.HasValue)
                    {
                        var (tsx, tsy) = screenPos.Value;
                        if (tsx >= 0 && tsx < _width && tsy >= 0 && tsy < viewH)
                        {
                            SetCell(tsx, tsy, visual.Char, visual.ForeColor, visual.BackColor);
                        }
                    }
                }
                
                drawn.Add(machine);
            }
        }
        
        // Second pass: draw wire overlays on top of everything
        // Wires preserve the terrain tile underneath (transparent overlay)
        foreach (var (sx, sy, ch, color) in wireOverlays)
        {
            SetCellOverlay(sx, sy, ch, color);
        }
    }
    
    /// <summary>
    /// Draws the build menu as an overlay.
    /// </summary>
    public void DrawBuildMenu(Inventory inventory, int selectedIndex, Direction currentDirection)
    {
        var types = new[]
        {
            MachineType.Miner,
            MachineType.ConveyorBelt,
            MachineType.Furnace,
            MachineType.Assembler,
            MachineType.StorageChest,
            MachineType.BiomassBurner,
            MachineType.CoalGenerator,
            MachineType.NuclearReactor,
            MachineType.Wire,
            MachineType.Lab,
            MachineType.Refinery,
            MachineType.FirewallTurret,
        };
        
        var lines = new List<string>
        {
            "╔═════════════════════════════════════════════════════════╗",
            "║                   BUILD MENU                           ║",
            "╠═════════════════════════════════════════════════════════╣",
            $"║  Direction: {currentDirection,-10} (R to rotate)              ║",
            "╠═════════════════════════════════════════════════════════╣",
        };
        
        for (int i = 0; i < types.Length; i++)
        {
            var mt = types[i];
            var cost = MachineGrid.GetBuildCost(mt);
            bool canBuild = cost.All(c => inventory.HasItem(c.Id, c.Count));
            bool selected = i == selectedIndex;
            
            string selector = selected ? " > " : "   ";
            string costStr = string.Join("+", cost.Select(c => $"{ItemRegistry.GetName(c.Id)}x{c.Count}"));
            string canBuildStr = canBuild ? " ✓" : " ✗";
            
            string entry = $"{selector}{MachineTypeInfo.GetSymbol(mt)} {MachineTypeInfo.GetName(mt),-15} [{costStr}]{canBuildStr}";
            lines.Add($"║{entry.PadRight(57)}║");
        }
        
        lines.Add("╠═════════════════════════════════════════════════════════╣");
        lines.Add("║  ↑↓ Select  R=Rotate  Enter=Place  B/Esc=Close        ║");
        lines.Add("╚═════════════════════════════════════════════════════════╝");
        
        DrawOverlay(lines.ToArray(), ConsoleColor.Cyan, ConsoleColor.White);
    }
    
    /// <summary>
    /// Draws the HUD bar at the bottom of the screen.
    /// </summary>
    public void DrawHUD(string biomeName, ConsoleColor biomeColor, int playerX, int playerY, 
        int tickRate, bool showHelp, TileType facingTile,
        int health, int maxHealth, bool damageFlash,
        int enemyCount, int machineCount, bool godMode = false)
    {
        int hudY = _height - _hudHeight;
        int w = _width;

        // ── Row 0: Top border ──
        DrawString(0, hudY, $"╔{new string('═', w - 2)}╗", ConsoleColor.DarkCyan);

        // ── Row 1: Health + Biome + Position + Facing ──
        DrawString(0, hudY + 1, "║", ConsoleColor.DarkCyan);
        DrawString(w - 1, hudY + 1, "║", ConsoleColor.DarkCyan);

        // Health bar
        int healthPct = maxHealth > 0 ? health * 10 / maxHealth : 0;
        string healthBar = new string('█', healthPct) + new string('░', 10 - healthPct);
        ConsoleColor healthColor = health > 60 ? ConsoleColor.Green 
            : health > 30 ? ConsoleColor.Yellow : ConsoleColor.Red;
        DrawString(2, hudY + 1, $"♥{healthBar}", damageFlash ? ConsoleColor.White : healthColor);

        // Biome
        DrawString(14, hudY + 1, $"│", ConsoleColor.DarkGray);
        DrawString(16, hudY + 1, biomeName, biomeColor);

        // Position
        DrawString(28, hudY + 1, $"│ Pos:({playerX},{playerY})", ConsoleColor.Gray);

        // Facing tile
        var (facingSym, facingColor) = TileTypeInfo.GetVisual(facingTile);
        string facingName = GetTileDisplayName(facingTile);
        bool isResource = TileTypeInfo.IsResource(facingTile);
        DrawString(48, hudY + 1, "│ ▸", ConsoleColor.DarkGray);
        SetCell(52, hudY + 1, facingSym, isResource ? facingColor : ConsoleColor.DarkGray);
        DrawString(53, hudY + 1, facingName, isResource ? facingColor : ConsoleColor.DarkGray);

        // Tick rate
        DrawString(w - 12, hudY + 1, $"│ {tickRate}ms", ConsoleColor.DarkGray);

        // God mode indicator
        if (godMode)
        {
            DrawString(w - 20, hudY + 1, "⚡GOD", ConsoleColor.Yellow);
        }

        // ── Row 2: Threat + Enemies + Machines ──
        DrawString(0, hudY + 2, "║", ConsoleColor.DarkCyan);
        DrawString(w - 1, hudY + 2, "║", ConsoleColor.DarkCyan);

        // Threat level
        string threat = machineCount < 5 ? "SAFE" : machineCount < 15 ? "LOW" 
            : machineCount < 30 ? "MODERATE" : machineCount < 50 ? "HIGH" : "CRITICAL";
        ConsoleColor threatColor = machineCount < 5 ? ConsoleColor.Green : machineCount < 15 ? ConsoleColor.DarkGreen 
            : machineCount < 30 ? ConsoleColor.Yellow : machineCount < 50 ? ConsoleColor.DarkRed : ConsoleColor.Red;
        DrawString(2, hudY + 2, $"⚠ {threat}", threatColor);

        DrawString(14, hudY + 2, "│", ConsoleColor.DarkGray);
        DrawString(16, hudY + 2, $"Enemies: {enemyCount}", 
            enemyCount > 0 ? ConsoleColor.Red : ConsoleColor.DarkGray);

        DrawString(30, hudY + 2, "│", ConsoleColor.DarkGray);
        DrawString(32, hudY + 2, $"Machines: {machineCount}", ConsoleColor.Gray);

        // ── Row 3: Controls + Inventory bar ──
        DrawString(0, hudY + 3, "║", ConsoleColor.DarkCyan);
        DrawString(w - 1, hudY + 3, "║", ConsoleColor.DarkCyan);

        string controls = showHelp 
            ? "WASD:Move Space:Mine B:Build I:Inv C:Craft E:Inspect H:Help Q:Quit"
            : "H: Show controls";
        DrawString(2, hudY + 3, controls, ConsoleColor.DarkGray);

        // ── Row 4: Bottom border ──
        DrawString(0, hudY + 4, $"╚{new string('═', w - 2)}╝", ConsoleColor.DarkCyan);
    }
    
    private static string GetTileDisplayName(TileType tile) => tile switch
    {
        TileType.Grass => "Grass",
        TileType.Sand => "Sand",
        TileType.Dirt => "Dirt",
        TileType.Stone => "Stone",
        TileType.Snow => "Snow",
        TileType.Water => "Water",
        TileType.Swamp => "Swamp",
        TileType.IronOre => "Iron Ore",
        TileType.CopperOre => "Copper Ore",
        TileType.Coal => "Coal",
        TileType.StoneDeposit => "Stone Deposit",
        TileType.Oil => "Oil",
        TileType.Quartz => "Quartz",
        TileType.Uranium => "Uranium",
        _ => "???"
    };
    
    /// <summary>
    /// Draws a ghost preview of a machine at the given world position.
    /// Shows the machine's box outline tinted green (valid) or red (invalid).
    /// </summary>
    public void DrawBuildGhost(int worldX, int worldY, int machineW, int machineH, char symbol, bool valid, Camera camera)
    {
        ConsoleColor color = valid ? ConsoleColor.Green : ConsoleColor.Red;
        int viewH = _height - _hudHeight;
        
        for (int dy = 0; dy < machineH; dy++)
        {
            for (int dx = 0; dx < machineW; dx++)
            {
                var screenPos = camera.WorldToScreen(worldX + dx, worldY + dy);
                if (!screenPos.HasValue) continue;
                var (sx, sy) = screenPos.Value;
                if (sx < 0 || sx >= _width || sy < 0 || sy >= viewH) continue;
                
                // Box-drawing characters for multi-tile outline
                char ch;
                if (machineW == 1 && machineH == 1)
                {
                    ch = symbol;
                }
                else
                {
                    bool top = dy == 0;
                    bool bottom = dy == machineH - 1;
                    bool left = dx == 0;
                    bool right = dx == machineW - 1;
                    
                    ch = (top, bottom, left, right) switch
                    {
                        (true, _, true, _) => '┌',
                        (true, _, _, true) => '┐',
                        (_, true, true, _) => '└',
                        (_, true, _, true) => '┘',
                        (true, _, _, _) => '─',
                        (_, true, _, _) => '─',
                        (_, _, true, _) => '│',
                        (_, _, _, true) => '│',
                        _ => symbol, // interior tile shows the machine symbol
                    };
                }
                
                SetCell(sx, sy, ch, color);
            }
        }
    }
    
    /// <summary>
    /// Draws a laser beam from the player to the mining target with spark effects.
    /// </summary>
    public void DrawLaserBeam(int playerWorldX, int playerWorldY, int facingX, int facingY,
        int miningProgress, int animFrame, Camera camera)
    {
        var playerScreen = camera.WorldToScreen(playerWorldX, playerWorldY);
        var (targetWX, targetWY) = (playerWorldX + facingX, playerWorldY + facingY);
        var targetScreen = camera.WorldToScreen(targetWX, targetWY);
        
        if (!playerScreen.HasValue || !targetScreen.HasValue) return;
        
        var (px, py) = playerScreen.Value;
        var (tx, ty) = targetScreen.Value;
        
        if (ty >= _height - _hudHeight) return;
        
        // Laser beam characters that cycle with animation
        var beamChars = new[] { '─', '═', '━', '─', '∙', '●', '∙', '★' };
        var sparkChars = new[] { '*', '✦', '★', '·', '✧', '∙', '*', '✶' };
        
        // Draw the beam line using Bresenham-like interpolation
        int dx = Math.Abs(tx - px), dy = Math.Abs(ty - py);
        int steps = Math.Max(dx, dy);
        if (steps == 0) steps = 1;
        
        for (int i = 1; i <= steps; i++)
        {
            int bx = px + (tx - px) * i / steps;
            int by = py + (ty - py) * i / steps;
            
            if (bx < 0 || bx >= _width || by < 0 || by >= _height - _hudHeight) continue;
            
            // Alternate beam characters for flickering effect
            int charIdx = (animFrame + i) % beamChars.Length;
            char beamChar = beamChars[charIdx];
            
            // Color alternates between red, dark red, and occasional yellow flash
            ConsoleColor beamColor = ((animFrame + i) % 5) switch
            {
                0 => ConsoleColor.Red,
                1 => ConsoleColor.DarkRed,
                2 => ConsoleColor.Red,
                3 => ConsoleColor.Yellow,  // flash!
                _ => ConsoleColor.DarkRed,
            };
            
            SetCell(bx, by, beamChar, beamColor);
        }
        
        // Impact sparks at target tile - pulsing ring
        int sparkIdx = animFrame % sparkChars.Length;
        char sparkChar = sparkChars[sparkIdx];
        
        // Draw sparks around the target
        if (tx >= 0 && tx < _width && ty >= 0 && ty < _height - _hudHeight)
        {
            SetCell(tx, ty, sparkChar, ConsoleColor.Yellow);
        }
        
        // Pulsing ring around impact
        int pulseRadius = 1 + (animFrame % 3);
        for (int angle = 0; angle < 8; angle++)
        {
            double a = angle * Math.PI / 4 + animFrame * 0.3;
            int sx2 = tx + (int)(Math.Cos(a) * pulseRadius);
            int sy2 = ty + (int)(Math.Sin(a) * pulseRadius);
            
            if (sx2 >= 0 && sx2 < _width && sy2 >= 0 && sy2 < _height - _hudHeight)
            {
                int si = (animFrame + angle) % sparkChars.Length;
                SetCell(sx2, sy2, sparkChars[si], ConsoleColor.Red);
            }
        }
        
        // Progress bar above target
        int barWidth = 10;
        int barX = tx - barWidth / 2 + 1;
        int barY = ty - 2;
        if (barY < 0) barY = ty - 1;
        if (barY < 0) barY = ty;
        
        int filled = (int)(barWidth * miningProgress / 100f);
        
        if (barY >= 0 && barY < _height - _hudHeight)
        {
            DrawString(barX, barY, "[", ConsoleColor.DarkRed);
            DrawString(barX + 1, barY, new string('░', barWidth), ConsoleColor.DarkGray);
            DrawString(barX + barWidth + 1, barY, "]", ConsoleColor.DarkRed);
            
            if (filled > 0)
            {
                DrawString(barX + 1, barY, new string('█', filled), ConsoleColor.Red);
            }
        }
    }
    
    /// <summary>
    /// Draws a mining splash effect when a resource is successfully mined.
    /// </summary>
    public void DrawMiningSplash(int worldX, int worldY, int timer, ItemId? minedItem, Camera camera)
    {
        var screenPos = camera.WorldToScreen(worldX, worldY);
        if (!screenPos.HasValue) return;
        
        var (sx, sy) = screenPos.Value;
        if (sy >= _height - _hudHeight) return;
        
        // Expanding burst of characters
        var burstChars = new[] { '★', '*', '✦', '★', '✧', '●', '✶', '·' };
        var colors = new[] { ConsoleColor.Yellow, ConsoleColor.Red, ConsoleColor.White, ConsoleColor.Yellow };
        
        int radius = 8 - timer; // Expands from 0 to 7
        
        // Center flash
        if (timer > 5)
        {
            SetCell(sx, sy, '█', ConsoleColor.White);
        }
        else if (timer > 3)
        {
            SetCell(sx, sy, '▓', ConsoleColor.Yellow);
        }
        
        // Burst particles in all directions
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4 + timer * 0.5;
            int px = sx + (int)(Math.Cos(angle) * radius);
            int py = sy + (int)(Math.Sin(angle) * radius);
            
            if (px >= 0 && px < _width && py >= 0 && py < _height - _hudHeight)
            {
                char ch = burstChars[(i + timer) % burstChars.Length];
                ConsoleColor col = colors[(i + timer) % colors.Length];
                SetCell(px, py, ch, col);
            }
        }
        
        // "POW!" text on first frames
        if (timer >= 6 && sx + 4 < _width && sy - 2 >= 0)
        {
            DrawString(sx - 1, sy - 2, "POW!", ConsoleColor.Yellow);
        }
        else if (timer >= 4 && sx + 4 < _width && sy - 2 >= 0)
        {
            DrawString(sx - 1, sy - 2, "+1!", ConsoleColor.Green);
        }
    }
    
    /// <summary>
    /// Draws the inventory quick-bar in the HUD area.
    /// </summary>
    public void DrawInventoryBar(Inventory inventory, int hudHeight)
    {
        // Draw inventory items at end of controls row (row 3 of the box HUD)
        int barY = _height - hudHeight + 3;
        int barX = 56;

        DrawString(barX, barY, "│", ConsoleColor.DarkGray);
        barX += 2;

        var filledSlots = inventory.GetFilledSlots().Take(8).ToList();

        foreach (var (_, stack) in filledSlots)
        {
            if (barX + 8 > _width - 2) break;

            char sym = ItemRegistry.GetSymbol(stack.Id);
            ConsoleColor color = ItemRegistry.GetColor(stack.Id);

            DrawString(barX, barY, $"{sym}", color);
            DrawString(barX + 1, barY, $"x{stack.Count}", ConsoleColor.Gray);
            barX += 7;
        }
    }
    
    /// <summary>
    /// Draws the full inventory screen as an overlay.
    /// </summary>
    public void DrawInventoryScreen(Inventory inventory)
    {
        var lines = new List<string>
        {
            "╔══════════════════════════════════════╗",
            "║           INVENTORY                  ║",
            "╠══════════════════════════════════════╣",
        };
        
        var filledSlots = inventory.GetFilledSlots().ToList();
        if (filledSlots.Count == 0)
        {
            lines.Add("║  (empty)                             ║");
        }
        else
        {
            foreach (var (index, stack) in filledSlots)
            {
                string name = ItemRegistry.GetName(stack.Id);
                string entry = $"  [{index + 1,2}] {name} x{stack.Count}";
                lines.Add($"║{entry.PadRight(38)}║");
            }
        }
        
        lines.Add("╠══════════════════════════════════════╣");
        lines.Add("║     Press I or Esc to close          ║");
        lines.Add("╚══════════════════════════════════════╝");
        
        DrawOverlay(lines.ToArray(), ConsoleColor.Yellow, ConsoleColor.White);
    }
    
    /// <summary>
    /// Draws the crafting menu as an overlay.
    /// </summary>
    public void DrawCraftingMenu(Inventory inventory, int selectedindex)
    {
        // Build combined recipe list: Manual + CraftingTable (if owned)
        var recipes = RecipeRegistry.GetByCategory(RecipeCategory.Manual);
        bool hasTable = inventory.HasItem(ItemId.CraftingTable, 1);
        if (hasTable)
            recipes.AddRange(RecipeRegistry.GetByCategory(RecipeCategory.CraftingTable));
        
        var craftable = recipes.Where(r => r.Inputs.All(i => inventory.HasItem(i.Id, i.Count))).ToList();
        
        var lines = new List<string>
        {
            "╔════════════════════════════════════════════════════╗",
            "║                  CRAFTING MENU                     ║",
            "╠════════════════════════════════════════════════════╣",
        };
        
        // Manual section header
        lines.Add("║  ── Manual Recipes ──                              ║");
        
        for (int i = 0; i < recipes.Count; i++)
        {
            // Add section header for Crafting Table recipes
            if (hasTable && i == RecipeRegistry.GetByCategory(RecipeCategory.Manual).Count)
            {
                lines.Add("║  ── Crafting Table Recipes ──                      ║");
            }
            
            var recipe = recipes[i];
            bool canCraft = craftable.Contains(recipe);
            bool selected = i == selectedindex;
            
            string selector = selected ? " > " : "   ";
            string color = canCraft ? " ✓" : " ✗";
            string inputStr = string.Join("+", recipe.Inputs.Select(inp => $"{ItemRegistry.GetName(inp.Id)}x{inp.Count}"));
            
            string entry = $"{selector}{recipe.OutputCount}x {recipe.Name} [{inputStr}]{color}";
            lines.Add($"║{entry.PadRight(52)}║");
        }
        
        lines.Add("╠════════════════════════════════════════════════════╣");
        lines.Add("║  ↑↓ Select  Enter=Craft  C/Esc=Close              ║");
        lines.Add("╚════════════════════════════════════════════════════╝");
        
        DrawOverlay(lines.ToArray(), ConsoleColor.Green, ConsoleColor.White);
    }
    
    /// <summary>
    /// Draws an overlay message centered on screen (for help, menus, etc.)
    /// </summary>
    public void DrawOverlay(string[] lines, ConsoleColor borderColor = ConsoleColor.Gray, ConsoleColor textColor = ConsoleColor.White)
    {
        if (lines.Length == 0) return;
        
        int maxLen = lines.Max(l => l.Length) + 4; // 2 padding each side
        int boxH = lines.Length + 2; // 1 border top + 1 border bottom
        int startX = (_width - maxLen) / 2;
        int startY = (_height - _hudHeight - boxH) / 2;
        
        // Top border
        DrawString(startX, startY, $"╔{new string('═', maxLen - 2)}╗", borderColor);
        
        // Lines
        for (int i = 0; i < lines.Length; i++)
        {
            DrawString(startX, startY + 1 + i, $"║ ", borderColor);
            DrawString(startX + 2, startY + 1 + i, lines[i].PadRight(maxLen - 4), textColor);
            DrawString(startX + maxLen - 2, startY + 1 + i, $"║", borderColor);
        }
        
        // Bottom border
        DrawString(startX, startY + 1 + lines.Length, $"╚{new string('═', maxLen - 2)}╝", borderColor);
    }
    
    /// <summary>
    /// Draws the terrain scanner overlay with minimap and resource list.
    /// </summary>
    public void DrawScannerOverlay(TerrainScanner scanner)
    {
        if (scanner.IsScanning && !scanner.HasResults)
        {
            // Scanning in progress
            int pct = (int)(scanner.Progress * 100);
            int barW = 30;
            int filled = (int)(barW * scanner.Progress);
            
            var lines = new List<string>
            {
                "╔══════════════════════════════════════╗",
                "║       TERRAIN SCANNER ACTIVE         ║",
                "╠══════════════════════════════════════╣",
                $"║                                      ║",
                $"║   Scanning... {pct,3}%                   ║",
                $"║   [{new string('█', filled)}{new string('░', barW - filled)}]   ║",
                $"║                                      ║",
                $"║   Press F/Esc to cancel              ║",
                "╚══════════════════════════════════════╝",
            };
            DrawOverlay(lines.ToArray(), ConsoleColor.Magenta, ConsoleColor.White);
            return;
        }
        
        if (!scanner.HasResults || scanner.Minimap == null) return;
        
        int mapSize = scanner.Minimap.GetLength(0);
        
        // Build the overlay with minimap on left, resource list on right
        int boxW = mapSize + 32;
        int boxH = mapSize + 4;
        
        int startX = (_width - boxW) / 2;
        int startY = (_height - HudHeight - boxH) / 2;
        
        if (startX < 0) startX = 0;
        if (startY < 0) startY = 0;
        
        // Title
        DrawString(startX, startY, $"╔{new string('═', boxW - 2)}╗", ConsoleColor.Magenta);
        DrawString(startX, startY + 1, $"║ TERRAIN SCAN RESULTS (radius: {TerrainScanner.ScanRadius})".PadRight(boxW - 1) + "║", ConsoleColor.Magenta);
        DrawString(startX, startY + 2, $"╠{new string('═', boxW - 2)}╣", ConsoleColor.Magenta);
        
        // Minimap
        for (int y = 0; y < mapSize; y++)
        {
            DrawString(startX, startY + 3 + y, "║ ", ConsoleColor.Magenta);
            for (int x = 0; x < mapSize; x++)
            {
                var (ch, color) = scanner.Minimap[x, y];
                SetCell(startX + 2 + x, startY + 3 + y, ch == ' ' ? '.' : ch, color);
            }
            
            // Center marker
            int center = mapSize / 2;
            SetCell(startX + 2 + center, startY + 3 + center, '@', ConsoleColor.Yellow);
            
            DrawString(startX + 2 + mapSize + 1, startY + 3 + y, "║", ConsoleColor.Magenta);
        }
        
        // Resource list on right side of minimap
        var nearest = scanner.GetNearestResources();
        int listX = startX + mapSize + 4;
        
        DrawString(listX, startY + 3, "NEAREST RESOURCES:", ConsoleColor.White);
        DrawString(listX, startY + 4, new string('─', 24), ConsoleColor.DarkGray);
        
        for (int i = 0; i < Math.Min(nearest.Count, mapSize - 3); i++)
        {
            var (type, dx, dy, dist) = nearest[i];
            string name = type switch
            {
                TileType.IronOre => "Iron",
                TileType.CopperOre => "Copper",
                TileType.Coal => "Coal",
                TileType.StoneDeposit => "Stone",
                TileType.Quartz => "Quartz",
                TileType.Oil => "Oil",
                TileType.Uranium => "Uranium",
                _ => type.ToString()
            };
            
            string direction = GetDirection(dx, dy);
            int tiles = (int)dist;
            
            ConsoleColor color = type switch
            {
                TileType.IronOre => ConsoleColor.White,
                TileType.CopperOre => ConsoleColor.DarkYellow,
                TileType.Coal => ConsoleColor.DarkGray,
                TileType.StoneDeposit => ConsoleColor.Gray,
                TileType.Quartz => ConsoleColor.Cyan,
                TileType.Oil => ConsoleColor.DarkMagenta,
                TileType.Uranium => ConsoleColor.Green,
                _ => ConsoleColor.White
            };
            
            DrawString(listX, startY + 5 + i, $"  {name,-8} {direction,2} {tiles,3}t ({dx:+0;-0},{dy:+0;-0})", color);
        }
        
        if (nearest.Count == 0)
        {
            DrawString(listX, startY + 5, "  No resources found!", ConsoleColor.Red);
        }
        
        // Bottom border
        DrawString(startX, startY + 3 + mapSize, $"╚{new string('═', boxW - 2)}╝", ConsoleColor.Magenta);
        DrawString(listX, startY + 3 + mapSize - 1, "Press F/Esc to close", ConsoleColor.DarkGray);
    }
    
    private static string GetDirection(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return "·";
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        return angle switch
        {
            >= -22.5 and < 22.5 => "→",
            >= 22.5 and < 67.5 => "↘",
            >= 67.5 and < 112.5 => "↓",
            >= 112.5 and < 157.5 => "↙",
            >= 157.5 or < -157.5 => "←",
            >= -157.5 and < -112.5 => "↖",
            >= -112.5 and < -67.5 => "↑",
            >= -67.5 and < -22.5 => "↗",
            _ => "?"
        };
    }
    
    /// <summary>
    /// Swaps buffers and renders only the changed cells to the console.
    /// </summary>
    public void Present()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                ref var back = ref _backBuffer[x, y];
                ref var front = ref _frontBuffer[x, y];
                
                if (back != front)
                {
                    Console.SetCursorPosition(x, y);
                    Console.ForegroundColor = back.ForeColor;
                    Console.BackgroundColor = back.BackColor;
                    Console.Write(back.Char);
                }
            }
        }
        
        // Swap buffers
        (_frontBuffer, _backBuffer) = (_backBuffer, _frontBuffer);
        
        // Reset cursor to hidden position
        Console.SetCursorPosition(0, 0);
        Console.ResetColor();
    }
}