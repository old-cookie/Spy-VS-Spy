# PlayerController (Unity, Netcode for GameObjects)

Controls player movement, jumping, facing, item interaction, flag/chest pickup, mini-game/pause input, and network-synchronized outcome animations. Requires `Animator`, `Rigidbody`, `ItemEffectHandler`, and `TeamMember`.

## Overview
- Responsibilities: movement, facing rotation, step-climbing, jump, ground-check; item pick/use/steal; flag pickup; pause/mini-game ESC handling; fall reset; outcome animations; spawn teleport.
- Networking: Extends `NetworkBehaviour`. Uses `ClientRpc` and server-side logic for syncing item steals and outcome animations with client authority.
- Inputs: New Input System via `InputSystem_Actions` (`Move`, `Jump`, `Pick`, `Use`). Only enabled on `IsLocalPlayer`.

## Key Dependencies
- Components: `Animator`, `Rigidbody`, `ItemEffectHandler`, `TeamMember` (all required), optional `SkinnedMeshRenderer[]` for team materials.
- Tags: `Player` (for ignoring player-vs-player collisions), `Floor` (for fall reset snap target).
- Animator parameters: `isRunning` (bool), `isLand` (bool), `Pick` (trigger), plus optional win/lose state names or triggers.

## Serialized Settings (tuning)
- Movement: `playerSpeed`
- Jump: `jumpForce`
- Team visuals: `playerRenderers`, `blueTeamMaterial`, `redTeamMaterial`
- Ground check: `groundCheckPos`, `groundMask`, `groundDistance`
- Step-climb: `stepHeight`, `stepCheckDistance`, `stepSmoothSpeed`
- Facing: `rotationSmoothSpeed`
- Pick lock: `pickLockDuration`
- Fallback pickup radii: `chestPickupRadius`, `flagPickupRadius`
- Fall reset: `fallResetThreshold`, `floorSnapHeightOffset`

## Movement & Facing
- Horizontal movement: translates along world X based on `Move` input, scaled by `playerSpeed * ItemEffectHandler.CurrentSpeedMultiplier`.
- Facing: smoothly slerps toward target (`right`, `left`, or `front` during pick); speed set by `rotationSmoothSpeed`.
- Step-climb: raycasts at foot and step height to allow climbing small obstacles (`stepHeight`, `stepCheckDistance`).
- Ground check: sphere check at `groundCheckPos` with `groundMask` and `groundDistance`.
- Jump: `Jump` adds upward impulse (`jumpForce * CurrentJumpMultiplier`) when grounded; triggers `Jump` animator.

## Items & Flags
- Chest pickup: `Pick` plays `Pick` animation, locks facing forward, and calls `ChestController.HandlePickStarted(transform)`. Chests handle network spawn; immediate return may be null.
- Flag pickup: `Pick` attempts flags first; respects team membership and whether player already has a flag.
- Held items: `RegisterHeldItem(Item)`, `HasHeldItem()`, `GetHeldItem()`; `Use` consumes and applies effects via `ItemEffectHandler.ApplyEffect(itemType)`.
- Fallback proximity: uses `Physics.OverlapSphereNonAlloc` to recover missed trigger events for chests/flags.

## Item Stealing (Server + Client)
- Server-side entry: `ExecuteItemSteal(ulong stealerNetworkObjectId)` checks held item, transfers ownership, updates `ItemSpawnManager`.
- RPCs:
  - `StealHeldItemServerRpc(ulong stealerNetworkObjectId)`: public server RPC for initiating a steal.
  - `RegisterStolenItemClientRpc(ulong itemNetworkObjectId)`: stealer client registers item locally after spawn.

## Pause & Mini-Game
- ESC handling:
  - In mini-games: ESC calls `MiniGameManager.ExitCurrentMiniGame()`.
  - Otherwise: toggles pause menu via `GameController.Instance.TogglePauseMenu()`.
- State: `SetPlayingMiniGame(bool)` stops movement and zeroes rigidbody velocity when entering.

## Outcome Animation (Win/Lose)
- Entry: `PlayOutcomeAnimation(bool isWinner, string winTrigger, string loseTrigger, string winStateName, string loseStateName, string idleStateName)`.
- Network model: Owner client must drive animations (client authority). If not owner, a `ClientRpc` requests the owner to play locally.
- Local play:
  - Freezes `Rigidbody` (`isKinematic = true`, gravity off, zero velocities).
  - Locks rotation to face camera (Y=180°) and disables `applyRootMotion`.
  - Plays provided state name (preferred) or trigger; falls back to idle state.
  - Disables player input but keeps component enabled so Animator updates.

## Fall Reset & Spawn Teleport
- Fall reset: if below `fallResetThreshold`, snaps to nearest `Floor` plus `floorSnapHeightOffset`; zeroes velocity.
- Teleport: `TeleportToSpawn()` moves to initial `spawnPosition` and clears velocity.

## Public API Summary
- Movement/State:
  - `IsPlayingMiniGame(): bool`
  - `SetPlayingMiniGame(bool playing)`
  - `TeleportToSpawn()`
- Items:
  - `HasHeldItem(): bool`
  - `GetHeldItem(): Item`
  - `RegisterHeldItemFromNetwork(Item newItem)`
  - `StealHeldItemServerRpc(ulong stealerNetworkObjectId)`
- Flags:
  - `SetCurrentFlag(FlagTrigger flag)`
- Outcome:
  - `PlayOutcomeAnimation(bool isWinner, string winTrigger, string loseTrigger, string winStateName, string loseStateName, string idleStateName)`

## Integration Notes
- Ensure `InputSystem_Actions` has `Player` map with `Move`, `Jump`, `Pick`, `Use` and is initialized.
- Animator should include `isRunning` and `isLand` booleans, a `Pick` trigger, and optional win/lose states or triggers.
- `TeamMember` must expose current team; materials applied to `playerRenderers`.
- Collisions: component ignores collisions with other `Player`-tagged objects.

## Extensibility Tips
- Replace facing strategy by modifying `CacheFacingRotations()` or target rotation updates.
- Adjust step handling by tuning `stepHeight`, `stepCheckDistance`, `stepSmoothSpeed`.
- Add new item effects via `ItemEffectHandler.ApplyEffect()` and per-item logic.
- Expand outcome animation variants using state names for clean looping.
