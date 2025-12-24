# Item (Unity Netcode)

Base class for pickable items spawned from chests. Handles initialization, consumption, discard, and network despawning.

## Overview
- Extends `NetworkBehaviour`; requires `NetworkObject` component.
- Lifecycle: spawned by chest → initialized with chest reference → follows player → consumed/discarded → despawned.
- Owner tracking: maintains reference to spawning `ChestController` for cleanup notification.

## Serialized Fields
- `itemType (string)`: type identifier used by `ItemEffectHandler.ApplyEffect()` to determine effects.
- `itemDescription (string)`: player-facing description displayed in UI.

## Properties
- `string ItemType`: gets the item type identifier (can be overridden).
- `string ItemDescription`: gets the UI description (can be overridden).

## Public API
- `void Initialize(ChestController chestController)`: sets the owning chest reference; called by chest when spawning.
- `void Consume()`: consumes the item, applies effect, notifies owner, and despawns.
- `void Discard()`: discards the item without applying effect (e.g., when picking up a new item while holding one); notifies owner and despawns.

## Networking
- `DespawnItem()`: handles despawning for both host and non-host clients.
  - Host: directly calls `NetworkObject.Despawn()`.
  - Non-host: sends `RequestDespawnRpc()` to server for despawn.
- `RequestDespawnRpc()`: server RPC to despawn item from non-host clients.

## Integration Flow
1. `ChestController` spawns item via `ItemSpawnManager.RequestSpawnItem()`.
2. Item instantiated and `NetworkObject.Spawn()` called.
3. `Initialize(chestController)` sets owner reference.
4. Item follows player via `ItemSpawnManager` coroutine.
5. `PlayerController` registers held item.
6. Player consumes (`Q` key) or discards (picks up new item).
7. `Consume()`/`Discard()` notifies chest via `NotifyItemConsumed()`.
8. `DespawnItem()` removes from network.

## Notes
- `NotifyOwnerConsumed()`: clears owner reference after notification to prevent duplicate calls.
- Subclasses can override `Consume()` for custom behavior (e.g., `PoopItem`, `Bomb`).
- Item type string must match cases in `ItemEffectHandler.ApplyEffect()`.
