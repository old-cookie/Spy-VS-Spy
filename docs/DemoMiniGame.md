# DemoMiniGame (Unity)

A simple demonstration mini game that shows typical integration with UI buttons and a countdown timer. Completes via a Finish button, fails via a Fail button or when the timer expires, and exits via ESC handled by the manager/player controller.

## Overview
- Purpose: Template for building actual mini games based on the `MiniGame` base class.
- UI: Uses a `Canvas` for visibility and two `Button`s for Finish/Fail actions.
- Timer: Optional `MiniGameTimer` to enforce a time limit.

## Serialized Fields
- `timeLimit (float)`: seconds until automatic failure; set to `0` for no limit.
- `miniGameCanvas (Canvas)`: root canvas for the mini game UI.
- `timer (MiniGameTimer)`: countdown component; optional.
- `btnFinish (Button)`: completes the game.
- `btnFail (Button)`: fails the game.

## Lifecycle
- `Awake()`: registers button listeners and `timer.OnTimeUp`.
- `OnGameStart()`: shows the canvas; if `timeLimit > 0`, calls `timer.SetDisplayVisible(true)` and `timer.StartTimer(timeLimit)`.
- `OnGameEnd(int result)`: stops/hides timer and hides canvas; logs a message based on `result`.
- `OnDestroy()`: cleans up listeners and timer subscription.

## End Conditions
- Finish: click `btnFinish` → calls `CompleteGame()` → result `1`.
- Fail: click `btnFail` → calls `FailGame()` → result `-1`.
- Timeout: `timer.OnTimeUp` → calls `FailGame()` → result `-1`.
- Exit: ESC (handled externally by `MiniGameManager`/`PlayerController`) → result `0`.

## Integration Notes
- Manager: Typically started via `MiniGameManager.StartRandomMiniGame(player, onSuccess)` which subscribes to end events and cleans up.
- Player state: `PlayerController.SetPlayingMiniGame(true/false)` is toggled automatically by the manager.
- Timer UI: `MiniGameTimer` controls text display; use `SetDisplayVisible(true/false)` to toggle visibility.

## Usage Steps
1. Create a prefab with `DemoMiniGame` and assign `miniGameCanvas`, `btnFinish`, `btnFail`, and optional `timer`.
2. Set `timeLimit` as needed.
3. Ensure the prefab is included in `MiniGameManager.availableMiniGamePrefabs` or start it manually.

## Result Messages
- `1`: "You completed the mini game!"
- `-1`: "Time's up! You failed."
- `0`: "You exited the mini game."
