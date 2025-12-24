# ItemEffectHandler (Unity Netcode)

Component attached to player that applies and manages item effects: speed boosts, slow downs, jump boosts, item stealing, and teleportation.

## Overview
- Requires `PlayerController` component.
- Tracks active effect timers and multipliers; updates per frame.
- Uses server RPCs to apply effects to other players or request server actions.
- Effects disabled while player is in mini-game.

## Serialized Settings

### Speed Boost (Cookie)
- `speedBoostMultiplier (float)`: multiplier for movement speed (1–5, default 2).
- `speedBoostDuration (float)`: effect duration in seconds (default 5).

### Slow Down (Banana)
- `slowDownMultiplier (float)`: multiplier for opponent speed (0.1–1, default 0.5).
- `slowDownDuration (float)`: effect duration in seconds (default 3).

### Jump Boost (Super Drink)
- `jumpBoostMultiplier (float)`: multiplier for jump force (1–5, default 1.5).
- `jumpBoostDuration (float)`: effect duration in seconds (default 4).

### Slow Down (Rust Gear)
- `rustGearSlowDownMultiplier (float)`: multiplier for opponent speed (0.1–1, default 0.2).
- `rustGearSlowDownDuration (float)`: effect duration in seconds (default 10).

## Properties
- `float CurrentSpeedMultiplier`: combined speed multiplier from boost and slow effects.
- `float CurrentJumpMultiplier`: current jump multiplier from jump boost.

## Public API
- `void ApplyEffect(string itemType)`: applies effect based on consumed item type.
  - Supported types: `"cookie"`, `"banana"`, `"super drink"`, `"rust gear"`, `"magnet"`, `"teleport"`, `"poop"`, `"bomb"`.
  - Guards against application during mini-game.

## Effect Details

### Speed Boost (cookie)
- Applied locally to consuming player.
- Sets `activeBoostMultiplier` and `speedBoostTimer`.

### Slow Down (banana, rust gear)
- Server RPC (`ApplySlowDownToOthersServerRpc`, `ApplyRustGearSlowDownServerRpc`) finds all other players.
- Calls client RPC on each opponent to apply slow locally.
- Client RPC guarded by `IsLocalPlayer` and mini-game check.

### Jump Boost (super drink)
- Applied locally to consuming player.
- Sets `activeJumpMultiplier` and `jumpBoostTimer`.

### Item Steal (magnet)
- Server RPC (`ApplyItemStealServerRpc`) identifies opposing team player.
- Checks if opponent has held item via `PlayerController.HasHeldItem()`.
- Calls `PlayerController.ExecuteItemSteal()` to transfer item.
- No range limit (unlimited across level).

### Teleport (teleport)
- Server RPC (`TeleportToSpawnServerRpc`) requests teleport.
- Client RPC (`TeleportToSpawnClientRpc`) calls `PlayerController.TeleportToSpawn()` on all clients for sync.

### Poop / Bomb
- Handled by subclass `Consume()` methods (spawn projectile/trap).

## Timer Updates
- `Update()` decrements active timers (`speedBoostTimer`, `slowDownTimer`, `jumpBoostTimer`).
- Resets multipliers to `1f` when timers expire.

## Integration
- `PlayerController.TryConsumeHeldItem()` calls `ApplyEffect(itemType)`.
- `PlayerController` reads `CurrentSpeedMultiplier` and `CurrentJumpMultiplier` for movement/jump.
- Teammates are filtered via `TeamMember.IsOnTeam()` checks.

## Notes
- Effects do not stack; new application resets timer and multiplier.
- `IsPlayingMiniGame()` guard prevents effects during mini-games.
- Item steal searches all players by tag; ensure `Player` tag is assigned.
