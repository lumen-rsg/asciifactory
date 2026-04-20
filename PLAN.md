# 🏭 Asciifactory — Development Plan

*A Factorio-inspired ASCII factory game for the terminal.*

## End Goal
Build **THE COMPUTER 9000™** — an interstellar factory spanning an alien planet, just to boot it up and have it print `"Hello, World!"` — the same thing Program.cs already did.

---

## Phases

### Phase 1 — Foundation ✅ ✅ COMPLETE
- [x] Double-buffered ASCII renderer with colors
- [x] Non-blocking keyboard input handler
- [x] Infinite chunk-based world generation (32x32 chunks, noise-based biomes)
- [x] Player entity with movement
- [x] Camera system (follows player, viewport)
- [x] Basic HUD (coordinates, biome name, controls)
- [x] Perlin noise generator for procedural terrain
- [x] Biome system (Plains, Forest, Desert, Mountains, Swamp)
- [x] Resource deposit generation per biome

### Phase 2 — Mining & Inventory ✅ COMPLETE
- [x] Tile interaction system (walk-up mining)
- [x] Item system (ItemRegistry with all game items)
- [x] Slot-based inventory with stacking
- [x] Manual crafting menu (craft basic tools, furnace fuel)
- [x] HUD shows inventory quick-bar

### Phase 3 — Machines & Belts ✅ COMPLETE
- [x] Machine base class (position, direction, power state, processing timer)
- [x] Miner (place on deposit → auto-extracts)
- [x] Conveyor Belt system (directional, items flow, visual)
- [x] Furnace (smelts ore → plates, fueled by coal)
- [x] Assembler (input items → output per recipe)
- [x] Storage Chest (input/output buffer)
- [x] Adjacency-based item transfer
- [x] RecipeRegistry with full recipe chain

### Phase 4 — Power ✅ COMPLETE
- [x] Power Generator (burns fuel → power in radius)
- [x] Machines only operate when powered
- [x] Power grid radius system (10 tiles)

### Phase 5 — Oil Processing ✅ COMPLETE
- [x] Oil Refinery (oil → plastic, fuel, fiber)
- [x] Chemical recipe category
- [x] Full item chain through all processing stages

### Phase 6 — The Computer 9000™ & Win Condition ✅ COMPLETE
- [x] Full recipe chain for Computer 9000™
- [x] Win screen with "Hello, World!" punchline
- [x] Intro screen, controls overlay, help screen
- [x] Save/Load system (JSON, auto-save, all game state)

### Phase 7 — Enemies & Defense ✅ COMPLETE
- [x] Software Bug enemies (Bug, Glitch, Memory Leak, Kernel Panic, Segfault, Null Pointer)
- [x] Enemy spawning scaled by factory size (more machines = harder enemies)
- [x] Defense: Firewall Turret (auto-targets, fires projectiles, needs Iron Plates as ammo)
- [x] Player laser damages enemies (Space while facing enemy)
- [x] Player health + regen + damage flash
- [x] Machine health + destruction + damage flash
- [x] Enemy health bars, projectile rendering
- [x] HUD: Health bar, Threat level, Enemy count
- [x] Loot drops from killed enemies

### Phase 8 — Animated Main Menu ✅ COMPLETE
- [x] Animated scrolling terrain background (dimmed Factorio-style)
- [x] Rainbow-cycling ASCII art title with sine wave float + letter wiggle
- [x] Menu: Play (Singleplayer / Multiplayer), Settings, About, Exit
- [x] Settings: Game Speed slider, Enemies toggle, Reset defaults
- [x] About screen with game description
- [x] Scrolling marquee at bottom with goofy quotes
- [x] Cycling tips in menu box
- [x] Background factory: machines, conveyor items, wandering bugs, sparkle particles
- [x] Hello World easter egg (Kernel Panic chases and eats it!)
- [x] Game.cs accepts GameSettings (tick rate, enemies enabled)
- [x] Program.cs shows menu first, then launches game

### Phase 9 — Power Grid Overhaul 🔄 IN PROGRESS
- [ ] **Legend fix**: Unicode machine diagrams with I/O ports in help screen
- [ ] **Grass minable**: Mine Grass/Dirt → Biomass item (early fuel)
- [ ] **New generators**: Biomass Burner (5MW), Coal Generator (25MW), Nuclear Reactor (250MW)
- [ ] **Power Wire machine**: connects generators to machines via flood-fill
- [ ] **PowerGrid class**: supply/demand calculation, overload detection
- [ ] **Machine PowerDraw**: each machine has MW requirement
- [ ] **Machine interaction menu** (E key): inventory, status, power, I/O display
- [ ] **Fuse/overload system**: explosion animation, manual fuse reset (Satisfactory-style)
- [ ] **Storage containers**: improved multi-I/O storage

### Phase 10 — Future Expansion (not yet implemented)
- [ ] Tech tree (Lab building, Science Packs, Tier unlocks)
- [ ] Power grid visualization overlay
- [ ] Production Calculator tool
- [ ] Antivirus Beacon, Debug Tower defense buildings

---

## Architecture

```
asciifactory/
├── Program.cs                 — Entry point
├── PLAN.md                    — This file
├── Game.cs                    — Main game loop & state
├── Camera.cs                  — Viewport
├── Renderer.cs                — Double-buffered ASCII renderer
├── InputHandler.cs            — Keyboard input
├── Entities/
│   └── Player.cs              — Player entity
├── WorldGen/
│   ├── Noise.cs               — Perlin noise generator
│   ├── TileType.cs            — Tile types & visual info
│   ├── Biome.cs               — Biome definitions
│   ├── Chunk.cs               — 32x32 chunk data
│   ├── WorldGenerator.cs      — Procedural generation
│   └── World.cs               — Infinite chunk world container
├── Machines/                  — (Phase 3+)
├── Items/                     — (Phase 2+)
├── Enemies/                   — (Phase 6+)
└── UI/                        — (Phase 2+)