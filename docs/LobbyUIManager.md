# LobbyUIManager (UI Toolkit, Netcode)

Manages lobby UI flows for single/multi-play, hosting and joining, and level selection. Uses UI Toolkit and optional Unity Transport for networking.

## Overview
- Panels: organizes multiple views (main/mode/single/multi/host waiting/client waiting).
- Controls: buttons for hosting/joining/starting, text fields for IP, dropdowns for level selection.
- Runtime names: extracts scene names for dropdown consumption.

## Behaviour
- Start: binds UI references, wires button events, and animates the start button.
- Host/Join: transitions to waiting panels; supports shutdown on back actions.
- Player count: updates labels while waiting.
- Level selection: maintains selected level name used before game start.

## Integration
- `NetworkManager` (and UTP) to start/stop listening for connections.
- `LevelSelectionState` to store the selected level for the upcoming game.
- `GameController` as the target scene controller after lobby.

## Tips
- Ensure `UIDocument` is present (required) and controls exist in the UXML.
- Keep UI state transitions guarded to avoid inconsistent panels.
