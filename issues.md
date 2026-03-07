# Oxygen Not Included - Multiplayer Mod Issues & Solutions

The current version of the mod is a structural prototype and will not fully work in-game out of the box. Below is a deep dive into the specific issues present in the codebase and comprehensive solutions to implement for a fully functional mod.

---

## Issue 1: Fake Networking Library (Mirror Stub)

### The Problem

During compilation, we used `MirrorStub.cs` to fake the Mirror networking classes (like `NetworkBehaviour`, `[Command]`, `[ClientRpc]`). This means the networking attributes do absolutely nothing. The mod compiles, but no network sockets are opened, and no data is sent across the internet. The `StartServer` and `JoinServer` methods in `NetworkManager.cs` also contain commented-out pseudo-code rather than actual connection logic.

### Deep Solution

1. **Acquire Mirror DLLs**: You must download the official [Mirror Networking](https://mirror-networking.com/) library for Unity and compile it into a DLL compatible with .NET 4.7.2.
2. **Remove Stub**: Delete `MirrorStub.cs` from your source folder.
3. **Reference Real Mirror**: Add the real `Mirror.dll` to your mod's directory and add it as a reference in `MultiplayerTrade.csproj`.
4. **Implement KMod Assembly Loading**: ONI doesn't load third-party DLLs automatically. You must use ILMerge to combine `Mirror.dll` into your `MultiplayerTrade.dll` OR use `System.Reflection.Assembly.Load` in your `MultiplayerCore` class to load `Mirror.dll` into memory before trying to use its classes.
5. **Implement Connection Logic**: Uncomment and implement the `NetworkManager.singleton.StartHost()` and `NetworkManager.singleton.StartClient()` logic inside `MultiplayerServerManager.cs`.

---

## Issue 2: Empty Trading Mechanics (No Resources Spawning/Consumed)

### The Problem

In `TradeManager.cs`, the methods `ConsumeLocalResources()` and `SpawnCargoPod()` are completely empty placeholders. If you send a trade, nothing is deducted from your asteroid. If you receive a trade, no resources ever actually spawn.

### Deep Solution

1. **Consuming Resources**:
   - You need to use Harmony to inject logic into the `RailGun` or `Storage` classes where the player launches items.
   - When the user selects items in the Multiplayer UI, iterate through their selected `Storage` components, and call `storage.ConsumeIgnoringDisease(SimHashes.Iron, mass)`.
2. **Spawning Cargo Pods**:
   - Inside `SpawnCargoPod(List<CargoItem> items)`:

     ```csharp
     // 1. Get the payload prefab
     GameObject podPrefab = Assets.GetPrefab(new Tag("RailGunPayload"));

     // 2. Spawn it at the world's surface (you must loop through Grid.Top() to find a safe drop spot)
     int cell = Grid.PosToCell(new Vector3(randomX, Grid.HeightInCells - 5));
     GameObject spawnedPod = GameUtil.KInstantiate(podPrefab, Grid.CellToPos(cell), Grid.SceneLayer.Ore);
     spawnedPod.SetActive(true);

     // 3. Add items to the pod's storage
     Storage storage = spawnedPod.GetComponent<Storage>();
     foreach(var item in items) {
         GameObject resource = ElementLoader.FindElementByHash(item.ResourceHash).substance.SpawnResource(Vector3.zero, item.amount, item.temperature, 0, 0);
         storage.Store(resource);
     }
     ```

---

## Issue 3: Missing Multiplayer Lobby UI

### The Problem

In `UIManager.cs`, clicking the new Multiplayer button simply calls `OpenMultiplayerMenu()`, which just prints to the console (`Debug.Log`). There is no actual UI screen to input a server IP, port, or password.

### Deep Solution

1. **Build a Custom UI**: Standard Unity UI does not work easily in ONI because Klei uses a custom `KScreen` system.
2. **Create a `MultiplayerLobbyScreen` class** that inherits from `KScreen` (similar to `MainMenu`).
3. **Instantiate UI Elements**: You will need to clone existing ONI menu canvases dynamically.
   ```csharp
   public class MultiplayerLobbyScreen : KScreen {
       protected override void OnPrefabInit() {
           base.OnPrefabInit();
           // Instantiate panels, text inputs using KInputTextField
           // Add Connect and Host buttons
       }
   }
   ```
4. Map the newly built UI canvas to the `OpenMultiplayerMenu()` function so it transitions smoothly instead of just logging to the console.

---

## Issue 4: Save File & State Synchronization

### The Problem

When players save their game, their networking session is not saved. If a payload is delayed by 2 cycles, and the player quits before it arrives, the payload memory is wiped from RAM and lost forever.

### Deep Solution

1. **Persistent State Manager**: Implement a custom class attached to ONI's `KMonoBehaviour` and use ONI's `[Serialize]` attributes.
2. **Store Pending Payloads**: Serialize a list of incoming `TradeMessage` objects inside the host player's ONI save file (`.sav`).
3. **Restore on Load**: Use `[HarmonyPatch(typeof(SaveLoader), "Load")]` to hook into game load events. On load, read the saved pending trades and restart the `IEnumerator ProcessIncomingPayload` coroutine with the remaining arrival time.

---

## Issue 5: Mapping Game Objects and Network Data (Data Serialization)

### The Problem

Mirror networking cannot send complex ONI objects (like `GameObject` or `Element`) over the network. Our `CargoItem` struct uses a standard `string` for `resourceType`. Simply passing "IronOre" across the network is not automatically understood by ONI's engine on the receiving side.

### Deep Solution

1. **Serialize to SimHashes**: Instead of using strings for resource names, network data must use the `SimHashes` enum (which maps integer IDs to resource types, like Copper, Water, Oxygen).
2. Update `CargoItem`:
   ```csharp
   public struct CargoItem {
       public SimHashes resourceHash; // Send the precise integer hash, not a string
       public float mass;
       public float temperature;
   }
   ```
3. Use the `ElementLoader.FindElementByHash()` on the receiving client to convert the network integer back into an actual material that ONI's physics engine can spawn.

---

## Issue 6: UI Layering and Main Menu Initialization

### The Problem

In `UIManager.cs`, we patch `MainMenu.OnSpawn()` to clone the "New Game" button. Klei frequently updates the UI hierarchy (e.g., adding DLC panels, cross-promotions). The path `"Canvas/Screen/ButtonContainer"` is extremely fragile. If ONI updates the UI hierarchy slightly, the button will fail to inject and silently crash.

### Deep Solution

1. Use robust component searches instead of hardcoded transform paths.

   ```csharp
   // Instead of: transform.Find("Canvas/Screen/ButtonContainer")

   // Do this: Add multiplayer button precisely next to the New Game button by referencing the New Game button directly
   LocText[] allTexts = mainMenu.GetComponentsInChildren<LocText>(true);
   LocText newGameText = allTexts.FirstOrDefault(t => t.text.Contains("NEW GAME"));

   if (newGameText != null) {
       KButton newGameBtn = newGameText.GetComponentInParent<KButton>();
       Transform container = newGameBtn.transform.parent;

       // Now clone safely into the container
       GameObject multiBtn = GameObject.Instantiate(newGameBtn.gameObject, container);
       multiBtn.transform.SetSiblingIndex(newGameBtn.transform.GetSiblingIndex() + 1);
   }
   ```

---

## Conclusion & Next Steps

To make the mod fully playable:

1. Compile the **Mirror Networking DLL**.
2. Write the **UI code** to draw the Lobby and Chat windows using `KScreen`.
3. Fill in the **Trading Hooks** utilizing `ElementLoader` and `GameUtil`.
4. Hook into the `SaveLoader` ensuring the server state writes to the save file.
