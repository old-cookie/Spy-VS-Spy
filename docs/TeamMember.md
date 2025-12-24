# TeamMember (Unity Netcode)

Networked component indicating a player’s team and whether they carry a flag. Notifies UI/controller on team changes; exposes RPCs for team assignment and flag state.

## Overview
- Network variables: `team` (`Team`), `hasFlag` (`bool`).
- Properties: `CurrentTeam`, `HasFlag`.
- Notifications: On local changes, calls `GameController.Instance.SetLocalPlayerTeam(...)`.

## RPCs & Methods
- `SetTeamRpc(Team newTeam)`: server-side assignment of team.
- `PickUpFlagRpc()`: mark player as carrying a flag (server).
- `TryScoreFlag()`: returns `true` if flag carried; clears flag state (server or via `ClearFlagRpc`).
- `ClearFlagRpc()`: server-side clear of flag state.

## Integration
- `ScoreZone` checks `IsOnTeam(...)` and calls `TryScoreFlag()` to validate scoring.
- `GameController` uses team info for UI and scoring attribution.

## Tips
- Call setters on server/host to respect write permissions.
- Guard UI updates until `GameController.Instance` is available (script handles with coroutine).
