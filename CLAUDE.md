# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
dotnet build
dotnet run
```

Output: `bin\Debug\net8.0-windows\Empire.exe`. No external dependencies beyond .NET 8.0 SDK. Can also be opened and run from Visual Studio 2022 (F5).

## Architecture Overview

Empire Remastered is a turn-based strategy game built with C# 8.0 / .NET 8.0 and WPF. The code is organized into three clear layers:

**UI Layer** (`MainWindow.xaml.cs`, `CombatWindow.xaml.cs`, etc.) — Handles input, selection state, and delegates all game logic to the model layer. `MainWindow` owns the game loop and calls `game.NextTurn()` at end-of-turn, then triggers a re-render.

**Game Logic Layer** (`Models/Game.cs`, `Models/Map.cs`, `Models/Player.cs`) — All game rules live here, entirely decoupled from WPF. `Game.cs` (~1540 lines) is the central orchestrator: combat resolution, production, resource income, automatic orders, and victory/elimination checks all run through `ProcessTurnMechanics()` on each turn.

**Rendering Layer** (`Services/MapRenderer.cs`) — Converts game state to a `WriteableBitmap` using PNG sprites from `Resources/Empire_Icons/`. Writes directly to an `Image` control in `MainWindow`.

## Key Domain Models

**Unit hierarchy**: `Unit` (abstract) → `LandUnit` / `SeaUnit` / `AirUnit` / `Satellite` → concrete types (Army, Tank, Artillery, Fighter, Bomber, Destroyer, Submarine, etc.). Each subclass defines movement costs, terrain restrictions, vision range, attack/defense stats, and fuel rules (air units crash at 0 fuel).

**Structure hierarchy**: `Structure` (abstract) → `Base` (has Shipyard, Barracks, Airport, MotorPool; 10 production pts/turn) and `City` (no Shipyard; 8 production pts/turn). Both maintain a `UnitProductionOrder` queue with progress tracking.

**Map & Tile**: `Map` holds a `Tile[,]` grid and owns A\* pathfinding (`FindPath`). `Tile` stores terrain type, resource type, owning player, structure reference, and the list of units currently on it. Movement costs and defense bonuses are computed on `Tile`.

**Player**: Owns `List<Unit>`, `List<Structure>`, fog-of-war (`Dictionary<TilePosition, VisibilityLevel>`), and resource totals (Gold, Steel, Oil). `Player.UpdateVision()` recalculates fog from unit and structure positions each turn using Bresenham line-of-sight (mountains block sight).

## Core Game Systems

**Turn flow**: `Game.NextTurn()` → `ProcessTurnMechanics()` which: checks sentry units, burns air-unit fuel, restores movement points, executes automatic orders (Patrol, Return-to-Base), advances production queues, then calls `CheckForEliminatedPlayers()`.

**Combat**: Percentage-based rolls (1–100) modified by unit matchup bonuses, veteran status (+5 attack), terrain defense, and structure bonuses. Artillery attacks are ranged with no counter-attack. Results are stored as `CombatResult` objects and replayed in `CombatWindow`.

**AI**: Personalities (Balanced, Aggressive, Defensive, Buildup, Naval, Aerial) are loaded from `AIPersonalities.txt`. `Services/AIController.cs` drives AI turns; aggression scales with losses tracked in `GameStatistics`.

**Sapper building**: Sappers construct Bases and Bridges over multiple turns via `Game.ProcessSapperBuilding()` — check here when changing construction rules.

**Satellites**: `OrbitingSatellite` follows predefined orbital paths; `GeosynchronousSatellite` holds a fixed position. Satellite vision is global (no fog-of-war for owning player).

## Important Patterns

- All combat math is in `Game.cs` (`CalculateCombat`, `AttackStructure`). Touch nothing else when adjusting balance.
- `MapRenderer` loads PNG sprites by unit class name convention — new unit types need a matching file in `Resources/Empire_Icons/`.
- `Orders` (waypoints, targets) are attached to `Unit`; `AutomaticOrder` (in `Services/`) represents a queued background movement dispatched by the game engine.
- AI personality data is data-driven via `AIPersonalities.txt` — prefer editing that file over hardcoding personality values.
