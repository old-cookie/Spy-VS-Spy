# Teleport Item Implementation

## Summary
A new **Teleport item** has been successfully implemented that returns players to their spawn position when used.

## Files Created/Modified

### 1. **TeleportItem.cs** (NEW)
- Location: `Assets/Items/TeleportItem.cs`
- Inherits from the `Item` base class
- Overrides the `Consume()` method
- When consumed, triggers the teleport effect (handled in ItemEffectHandler)

### 2. **ItemEffectHandler.cs** (MODIFIED)
- Added "teleport" case to the `ApplyEffect()` switch statement
- Added new method: `TeleportToSpawnServerRpc()`
- This RPC calls `PlayerController.TeleportToSpawn()` to move the player back to spawn

### 3. **PlayerController.cs** (MODIFIED)
- Added new field: `spawnPosition` (Vector3) to store the player's spawn location
- Updated `OnNetworkSpawn()` to capture and store the spawn position: `spawnPosition = transform.position;`
- Added new public method: `TeleportToSpawn()`
  - Resets the player's velocity to zero
  - Moves the player to the stored spawn position
  - Provides debug logging

## How It Works

1. **Item Pickup**: Player picks up the Teleport item (Crown C model)
2. **Item Use**: Player presses the use key (Q) to consume the item
3. **Effect Trigger**: ItemEffectHandler recognizes the item type as "teleport"
4. **Teleportation**: `TeleportToSpawnServerRpc()` is called, which invokes `PlayerController.TeleportToSpawn()`
5. **Result**: Player is instantly moved back to their spawn position with zero velocity

## Setup Instructions

### In Unity Editor:
1. Open the **ItemSpawnManager** prefab in the scene or Inspector
2. Locate the **itemPrefabs** list
3. Add the **Crown C** prefab from `Assets/Items/Teleport(back to the spawn)/`
4. Ensure the Crown C prefab has:
   - A **NetworkObject** component
   - A **TeleportItem** script component
   - The **itemType** field set to **"teleport"**

### Item Type Configuration:
The item type is case-sensitive and must match exactly:
```
itemType = "teleport"
```

This value is referenced in the ItemEffectHandler switch statement.

## Testing
1. Create a new game session with at least one player
2. Move the player away from their spawn position
3. Pick up the Teleport item from a chest
4. Use the item (press Q by default)
5. Verify the player returns to their original spawn position

## Item Type Reference
All available item types in the game:
- `"cookie"` - Speed boost
- `"banana"` - Slow down other players
- `"super drink"` - Jump boost
- `"rust gear"` - Heavy slow down for other players
- `"magnet"` - Steal items from opponents
- `"teleport"` - Teleport back to spawn (NEW)
