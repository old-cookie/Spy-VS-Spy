# ItemSpawnManager (Unity Netcode)

Singleton manager for networked item spawning from chests. Handles server-side spawning, client notification, and item following behavior.

## Overview
- Singleton: `Instance` set in `Awake()`; destroyed on duplicates.
- Server-authoritative: item spawning occurs on server; synced to clients via `NetworkObject.Spawn()`.
- Follow system: items animate from chest to player and continuously follow above player's head.

## Serialized Fields
- `itemPrefabs (List<GameObject>)`: pool of item prefabs for random selection; must have `NetworkObject` and `Item` components.
- `itemFollowHeight (float)`: vertical offset above player for item position (default 3).
- `itemMoveDuration (float)`: duration for item to move from chest to player (default 0.75s).
- `itemFollowSmoothSpeed (float)`: interpolation speed for smooth following (default 8).

## Public API
- `void RequestSpawnItem(ulong playerNetworkObjectId, Vector3 chestPosition, int itemPrefabIndex = -1)`: spawns item for player at chest position.
  - `playerNetworkObjectId`: requesting player's network ID.
  - `chestPosition`: spawn position (chest location).
  - `itemPrefabIndex`: specific prefab index; `-1` for random selection.
- `void StopItemFollow(ulong itemNetworkObjectId)`: stops follow coroutine for item (called on consume/discard).
- `void ChangeItemOwner(ulong itemNetworkObjectId, ulong newOwnerPlayerNetworkObjectId)`: transfers item ownership and updates follow target (used for item stealing).

## Spawning Flow
1. `RequestSpawnItem()` called by `ChestController` on item pickup.
2. If non-server, sends `RequestSpawnItemRpc()` to server.
3. Server:
   - Validates player exists via `NetworkManager.SpawnedObjects`.
   - Selects random prefab (if index < 0).
   - Instantiates item at `chestPosition` with `-45°` X rotation.
   - Calls `NetworkObject.Spawn(true)` to sync across network.
   - Sends `NotifyItemSpawnedClientRpc()` to notify owning player.
   - Starts `ItemFollowRoutine()` coroutine.
4. Client (owner only):
   - Receives `NotifyItemSpawnedClientRpc()`.
   - Waits one frame for item spawn.
   - Calls `PlayerController.RegisterHeldItemFromNetwork()`.

## Follow Behavior
- `ItemFollowRoutine()`:
  1. Animates item from `chestPosition` to player anchor over `itemMoveDuration`.
  2. Continuously lerps item position toward player anchor using `itemFollowSmoothSpeed`.
  3. Anchor position: player collider center + `Vector3.up * itemFollowHeight`.
  4. Runs until item or player destroyed.

## Coroutine Management
- `activeItemCoroutines`: dictionary tracking active follow coroutines by item network ID.
- `StopItemFollow()`: stops and removes coroutine; called when item consumed/discarded.
- `OnNetworkDespawn()`: stops all active coroutines on despawn.

## Ownership Transfer (Item Stealing)
- `ChangeItemOwner()`:
  1. Stops current follow coroutine.
  2. Validates item and new owner exist.
  3. Starts new `ItemFollowRoutine()` with new owner's transform.
  4. Updates `activeItemCoroutines` entry.
- Called by `ItemEffectHandler.ApplyItemStealServerRpc()` after stealing logic.

## Integration
- `ChestController.HandlePickStarted()` calls `RequestSpawnItem()`.
- `Item.Consume()`/`Discard()` triggers despawn; manager stops follow coroutine automatically.
- `ItemEffectHandler` uses `ChangeItemOwner()` for magnet item effect.

## Notes
- Ensure item prefabs have `NetworkObject` and `Item` components.
- Follow coroutine handles null checks for destroyed items/players.
- Only server spawns items; clients receive via network replication.
- Owner registration delayed by one frame to ensure network spawn completes.
