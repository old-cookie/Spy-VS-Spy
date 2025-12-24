# LevelSelectionState (Unity Netcode)

Networked state holder for lobby-selected level and match-winning team, persisted across scene loads.

## Overview
- Singleton: `Instance` set in `Awake()`; destroys duplicates.
- Persistence: `DontDestroyOnLoad` in `OnNetworkSpawn()` keeps the object across scenes.
- Networked data:
  - `selectedLevelName`: `FixedString64Bytes`, readable by everyone, server-writable.
  - `winningTeam`: `Team`, readable by everyone, server-writable.

## Public API
- `string SelectedLevelName`: current chosen level prefab name.
- `Team WinningTeam`: last match’s winning team.
- `void SetSelectedLevelName(string levelName)`: server-only setter.
- `void SetWinningTeam(Team team)`: server-only setter.
- `void ClearWinningTeam()`: server-only clear.

## Integration
- Lobby assigns selected level before loading the game scene.
- End scene queries `WinningTeam` to present results.

## Tips
- Validate name strings against available level prefabs/scenes.
- Call setters only on server/host to respect write permissions.
