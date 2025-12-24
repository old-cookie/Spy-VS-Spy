# GameController (Unity Netcode)

Central match controller for multiplayer: spawns players, assigns teams, tracks scores, manages pause UI, and handles end-of-match outcome presentation.

## Overview
- Networking: Inherits `NetworkBehaviour`; uses `NetworkVariable<int>` for team scores.
- Spawning: Instantiates `playerPrefabs` at positions in `spawnPos` per connected client.
- Scoring: Tracks blue/red team points; triggers end scene when `pointsToWin` reached.
- UI: Uses `UIDocument`/UI Toolkit for score and pause menu display.
- Flags/Items: Prefabs referenced; typically placed in scenes (no runtime spawning for flags/chests).

## Key Fields
- `playerPrefabs`: player prefab to spawn.
- `spawnPos`: list of spawn transforms.
- `pointsToWin`: threshold to end match.
- `blueTeamScore`, `redTeamScore`: networked scores.
- UI elements: score containers, labels, pause menu, end-game controls.
- Animator params: win/lose trigger names and state names (`win`, `lose`, `Win`, `Lose`, fallback `Idle`).

## Behaviour Highlights
- Pause menu: toggled by local input via `GameController.Instance.TogglePauseMenu()`.
- Outcome: Plays win/lose animations per player (client-authoritative), then drives end scene flow.
- Local team: Receives notifications from `TeamMember` to set the client’s team for UI presentation.

## Integration Points
- `ScoreZone`: calls scoring RPC on the controller to add points for a team.
- `TeamMember`: notifies controller of local team changes via `SetLocalPlayerTeam(...)`.
- `PlayerController`: queries `IsPauseMenuOpen` to suppress input; plays outcome animations using parameters defined here.
- `LevelSelectionState`: communicates winning team for end scene transitions.

## Notes
- Ensure UI Document is assigned and visual elements are bound at runtime.
- Keep `matchEnded` guard to prevent duplicate end triggers.
