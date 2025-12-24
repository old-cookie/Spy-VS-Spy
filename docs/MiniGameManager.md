# MiniGameManager (Unity)

Central controller for starting, tracking, and ending mini games. Provides a singleton entry point, prefab selection, and player state coordination.

## Overview
- Singleton: `Instance` is set in `Awake()`; destroyed duplicates.
- Prefabs: `availableMiniGamePrefabs` (Inspector list) supplies candidates for random selection.
- Active state: Tracks `currentMiniGameInstance` and `currentPlayer`.
- Success callback: Optional `onMiniGameSuccess` invoked when a mini game completes with result `1`.

## Public API
- `bool StartRandomMiniGame(PlayerController player, Action onSuccess = null)`: selects a random prefab, instantiates it, subscribes to end event, marks player as in mini game, and starts the game.
- `void ExitCurrentMiniGame()`: exits active game (ESC-triggered), delegating to the instance.
- `string GetCurrentMiniGameName()`: returns the current mini game name or `null`.
- `bool IsMiniGameActive`: true when an instance exists and `IsActive`.

## Flow
1. Validate player and prefab availability; prevent overlapping sessions.
2. Instantiate selected prefab under manager GameObject.
3. Subscribe to `OnMiniGameEnded` and call `player.SetPlayingMiniGame(true)`.
4. Start mini game via `StartGame(player)`.
5. On end:
   - Unsubscribe event.
   - Call `player.SetPlayingMiniGame(false)` and `player.OnMiniGameResult(result)`.
   - Log outcome and invoke `onMiniGameSuccess` if result is `1`.
   - Destroy instance and clear references.

## Setup (Inspector)
- Assign one or more `MiniGame` prefabs to `availableMiniGamePrefabs`.
- Place manager in scene once; ensure no duplicates.

## ESC Handling
- `PlayerController` calls `ExitCurrentMiniGame()` when ESC is pressed during mini-game.
- Mini games do not need to poll for ESC directly.

## Best Practices
- Keep mini games self-contained and stateless between runs.
- Use `onSuccess` to trigger gameplay rewards only on `result == 1`.
- Avoid starting another mini game while `IsMiniGameActive`.
