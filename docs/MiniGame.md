# MiniGame (Unity)

Abstract base class for flag-related mini games. Defines lifecycle, player association, activation state, and completion/failure/exit signaling via an end event.

## Overview
- Purpose: Provide a common scaffold for small, self-contained mini games triggered during flag interactions.
- Activation: `StartGame(PlayerController)` sets active, stores player, enables GameObject, then calls `OnGameStart()`.
- Completion: `CompleteGame()` → result `1`; `FailGame()` → result `-1`; `ExitGame()` → result `0` (ESC).
- Deactivation: `EndGame(int)` calls `OnGameEnd(result)`, fires `OnMiniGameEnded`, clears player, and disables GameObject.

## Key Properties
- `MiniGameName`: display name (Inspector-backed).
- `IsActive`: whether this mini game is currently running.
- `CurrentPlayer`: the `PlayerController` who started the game.

## Events
- `OnMiniGameEnded(int result)`: lifecycle end notification.
  - `1` = completed successfully
  - `-1` = failed
  - `0` = exited via ESC

## Lifecycle Methods
- `StartGame(PlayerController player)`: guard against double starts; sets state and invokes `OnGameStart()`.
- `OnGameStart()`: override for initialization (spawn UI, reset state).
- `EndGame(int result)`: internal end; calls `OnGameEnd(result)`, raises event, disables object.
- `OnGameEnd(int result)`: override for cleanup (dispose UI, stop timers).
- `ExitGame()`: convenience exit with result `0`.
- `CompleteGame()`: convenience completion with result `1`.
- `FailGame()`: convenience failure with result `-1`.
- `Update()`: when active, run per-frame game logic; ESC handling is centralized elsewhere.

## Integration
- `MiniGameManager` instantiates and manages lifecycle, subscribing to `OnMiniGameEnded`.
- `PlayerController` toggles mini-game input mode and forwards ESC via `MiniGameManager`.
- UI/timers can be attached to the mini game prefab; use `MiniGameTimer` if needed.

## Implementation Tips
- Keep mini game logic encapsulated inside the prefab.
- Use `IsActive` to guard input and updates.
- Prefer event-driven finish (button press, timer expiry) to call `CompleteGame()` or `FailGame()`.
- Cleanly undo any scene/UI changes in `OnGameEnd(result)`.
