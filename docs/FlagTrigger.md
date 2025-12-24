# FlagTrigger (Unity)

Trigger zone for team flags. Handles eligibility checks, hands off pickup to `PlayerController`, and starts mini-games via `MiniGameManager` when the local player grabs the flag.

## Overview
- Team-gated: only players on `flagTeam` and not already carrying a flag can interact.
- Local authority for mini-games: only the local player triggers mini-game start; others pick up directly.
- Integration: cooperates with `PlayerController` and `TeamMember` to set flag state and mini-game flow.

## Serialized Fields
- `flagTeam (Team)`: which team can pick up this flag.

## Public API
- `Team Team`: read-only access to `flagTeam`.
- `void PerformPickup()`: called by `PlayerController` after pick animation; triggers mini-game for local player and awards flag on success.

## Trigger Flow
1. `OnTriggerEnter(Collider other)`:
   - Check tag `Player`.
   - Get `TeamMember` (self or parent); require same team and `HasFlag == false`.
   - Get `PlayerController`; store references; call `playerController.SetCurrentFlag(this)`.
2. `OnTriggerExit`: clears references and calls `SetCurrentFlag(null)` if the exiting player was the current one.

## Pickup Flow (PerformPickup)
- Validate stored `TeamMember` and `PlayerController`.
- Determine `isLocalPlayer` from the controller's `NetworkObject`.
- If local player:
  - If `MiniGameManager.Instance` exists, start random mini-game with success callback awarding flag via `teamMember.PickUpFlagRpc()`.
  - If manager missing, log warning and award flag directly.
- If not local: award flag directly via `PickUpFlagRpc()`.

## Integration Notes
- Requires `TeamMember` and `PlayerController` on entering collider (self or parent).
- `PlayerController.SetCurrentFlag(this/null)` enables interaction via the player's pick input.
- Mini-game success path uses `MiniGameManager.StartRandomMiniGame(player, onSuccess)`; only success (`result == 1`) awards the flag.
- Ensure flag GameObjects are tagged/layered appropriately to receive trigger events.
