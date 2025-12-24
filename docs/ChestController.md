# ChestController (Unity)

Handles player interaction with chests and requests networked item spawning via `ItemSpawnManager`.

## Overview
- Interaction entry point: `HandlePickStarted()` is called by `PlayerController` when the player presses pick near a chest.
- Only the local player sends the spawn request; items are spawned server-side and synced to all clients.
- The spawned item is registered to the player via network callbacks; method returns `null` intentionally.

## Fields
- `Item activeItem`: tracked current item spawned by this chest (cleared on consume).

## Public API
- `Item HandlePickStarted(Transform pickerTransform)`: validates picker, ensures local player, requests item spawn, returns null.
- `void NotifyItemConsumed(Item consumedItem)`: clears `activeItem` when the spawned item is consumed/discarded.

## Spawn Flow
1. Validate `pickerTransform` has a `NetworkObject` and is local player.
2. Compute chest center via `GetChestCenter()` (collider bounds or transform position).
3. Call `ItemSpawnManager.Instance.RequestSpawnItem(playerNetworkObjectId, chestCenter)`.
4. Item is spawned server-side; owner player registers it through `PlayerController.RegisterHeldItemFromNetwork()` when notified.
5. Method returns `null` because registration occurs asynchronously over the network.

## Helpers
- `Vector3 GetChestCenter()`: returns collider bounds center if collider exists; otherwise transform position.

## Integration Notes
- Requires `ItemSpawnManager` singleton in the scene; logs warning if missing.
- `activeItem` currently only cleared on `NotifyItemConsumed`; extend if tracking is needed.
- Ensure chest colliders are set as triggers or accessible for player interaction.
