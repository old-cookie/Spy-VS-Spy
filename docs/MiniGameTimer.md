# MiniGameTimer (Unity)

Simple countdown timer component for mini games, with optional UI `Text` display and an `OnTimeUp` event.

## Overview
- Displays remaining time via an optional `Text` field (`timeText`).
- Emits `OnTimeUp` when the timer reaches zero.
- Exposes `RemainingTime`, `IsRunning` state, and basic control methods.

## Public API
- `void StartTimer(float seconds)`: set limit, reset remaining time, start countdown, update display.
- `void StopTimer()`: stop countdown.
- `void ResetTimer()`: reset remaining time to the initial limit, stop, update display.
- `void SetDisplayVisible(bool visible)`: show/hide the `timeText` if assigned.

## Properties & Events
- `float RemainingTime`: remaining seconds (decreases each frame when running).
- `bool IsRunning`: whether countdown is active.
- `event Action OnTimeUp`: fired once when remaining time reaches zero.

## Update Loop
- Decrements `RemainingTime` by `Time.deltaTime` when `IsRunning`.
- Clamps to `0`, stops, updates display, then invokes `OnTimeUp`.

## Integration Notes
- Assign `timeText` to a UI Text to show seconds; the format is `"Time: {ceil}s"`.
- Combine with `MiniGame` logic: start/stop/reset timer in `OnGameStart()`/`OnGameEnd()`.
- Use `OnTimeUp` to call `FailGame()` on the active mini game or to trigger other outcomes.
