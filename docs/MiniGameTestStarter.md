# MiniGameTestStarter (Unity)

Simple runtime harness to spawn a `MiniGame` prefab for testing and optionally expose its timer UI.

## Overview
- Prefab: `prefabToSpawn` must derive from `MiniGame`.
- Start options: `startOnSceneLoad` auto-spawns/starts; `ensureDisplayVisible` toggles timer UI visibility.
- Timer wiring: searches spawned prefab and children for `MiniGameTimer`.

## Flow
1. On `Start()`, instantiate the prefab under the starter.
2. Enable the instance if disabled; call `StartGame(null)` (no player context).
3. If timer found, call `SetDisplayVisible(true)` when configured.

## Tips
- Use in non-networked test scenes to validate mini game UI/logic.
- Let the mini game control `StartTimer()` internally in `OnGameStart()`.
