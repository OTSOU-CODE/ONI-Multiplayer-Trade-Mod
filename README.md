# Oxygen Not Included - Multiplayer Trade Mod

This is a prototype implementation of the "Spaced Out Approach" for an asynchronous multiplayer system in Oxygen Not Included.

## Project Structure

- `mod_info.yaml` / `mod.yaml`: Mod metadata
- `multiplayer_config.json`: Configuration for Networking, Gameplay, and UI
- `src/`: Contains all C# scripts:
  - `MultiplayerCore.cs`: Harmony patches and mod initialization
  - `NetworkManager.cs`: NetworkBehaviour class implementing Mirror features (Client/Server roles)
  - `TradeManager.cs`: Trade validation, payload sending, and processing payload arrivals
  - `UIManager.cs`: Harmony patches for Main Menu and HUD/Notifications
  - `DataStructures.cs`: DTOs and structs for networking messages
  - `ConfigManager.cs`: JSON configuration loader/saver

## Dependencies

- BepInEx or KMod default mod framework
- Harmony (bundled with ONI / KMod)
- Newtonsoft.Json (bundled in ONI)
- **Mirror Networking** for Unity: Requires Mirror DLLs to be packaged or present in the mod folder

## How To Build

1. Reference the standard ONI libraries: `Assembly-CSharp.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `Newtonsoft.Json.dll`, `0Harmony.dll`.
2. Reference the Mirror Networking assemblies.
3. Compile all `.cs` files in `src/` to `MultiplayerTrade.dll`.
4. Copy `MultiplayerTrade.dll`, `mod.yaml`, `mod_info.yaml`, and `multiplayer_config.json` to your local mods directory (e.g. `Documents/Klei/OxygenNotIncluded/mods/Local/MultiplayerTrade`).

## Features

- **Main Menu Integration**: Injects a MULTIPLAYER button below New Game.
- **Configurable Rules**: Change port, max players, payload travel time, and UI keys in `multiplayer_config.json`.
- **Asynchronous Trading**: Players can send resources through the network without needing real-time simulation synchronization.
- **Chat & Notifications**: Basic implementation hooks for chat and trade arrival sounds/notifications.
