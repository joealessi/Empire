# Empire Remastered

Empire Remastered is a modern reimagining of the classic DOS strategy game Empire. It keeps the slow, thoughtful pacing and the importance of every unit while updating the interface to be clean, readable, and easy to control with a mouse. The world map is large, fog of war matters, scouting is meaningful, and surviving long enough to earn veteran status still feels like a story in itself.

The goal of this project is not to reinvent the game, but to present it in a way that matches the memory of how it felt to play. The intent is clarity. The rules are visible, the outcomes are explainable, and the strategy emerges from the map rather than from hidden systems.

## Gameplay Overview

You begin with a single foothold somewhere on a large and mostly unexplored map. Cities and bases produce units over time. Armies and tanks advance across land. Ships push outward along coastlines and sea lanes. Air units scout, strike, defend, and sometimes run out of fuel at the worst possible moment.

Bombers are built as two-plane flights and commit to full mission plans when launched. Fighters can escort them, but only as far as their fuel takes them, unless a tanker is assigned to the mission. Anti-aircraft units provide protection around cities and bases. Artillery supports land operations from behind the lines. Spies move quietly across enemy territory while appearing to them as one of their own units, revealed only when discovered or engaged.

Every choice is a tradeoff. Every unit is an investment of time and attention. The game rewards planning and patience more than speed.

## Technology and Structure

The game is written in C# using WPF. The visual layout is simple: a large interactive map view and a side control panel that updates based on what you have selected. The rules engine is deterministic so outcomes can be reasoned about and tested. The user interface and the core game logic are intentionally separated so that unit behavior, combat resolution, and turn processing can evolve without rewriting the display layer.

## Current Status

Core systems defined:
- Unit classes
- Structures and production mechanics
- Veterancy and survival rules
- Movement and mission assignment

In progress:
- Map rendering and camera controls
- Pathfinding (A*)
- Combat resolution
- AI behavior
- Save and load support

## Running the Project

Clone the repository and open the solution in Visual Studio or Rider. Restore NuGet packages if prompted. Build and run. The project targets .NET 6 or later.

## Contributing

If you want to contribute, the most helpful areas are pathfinding, UI refinement, and AI planning. If you remember something specific from the original Empire that should be preserved, feel free to open an issue or discussion. Clarity and simplicity remain the design priorities.

## License

MIT License.
