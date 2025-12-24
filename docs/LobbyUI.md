# LobbyUI (UI Toolkit, UXML + USS)

Lobby menu for game mode selection, single-player/multi-player setup, hosting, joining, and level selection. Uses UI Toolkit with stylesheet for responsive cards and button animations.

## Overview
- **File pair**: `LobyUI.uxml` (structure) + `LobyUI.uss` (styles).
- **Root**: `root` class `lobby-root` (column, centered, full height).
- **Container**: 720px max-width `container` for responsive layout.

## Panels

### Main Panel (mainPanel)
- **Elements**:
  - Title: "Spy V.S. Spy", 28px bold.
  - `startGameButton`: "Start Game", primary button.
- **Class**: `card start-panel`.

### Mode Selector Panel (modePanel, hidden by default)
- **Elements**:
  - Title: "Game Mode", 28px bold.
  - Two buttons in row: `singlePlayButton` ("Single Play"), `multiPlayButton` ("Multi Play").
  - `backFromModeButton` ("Back").
- **Class**: `card start-panel`.

### Single-Play Panel (singlePlayPanel, hidden by default)
- **Elements**:
  - Title: "Single Player", 28px bold.
  - `singleLevelDropdown`: level selection dropdown.
  - `singleStartButton` ("Start Game").
  - `backFromSingleButton` ("Back").
- **Class**: `card start-panel`.

### Multi-Play Panel (multiPlayPanel, hidden by default)
- **Elements**:
  - Title: "Multi Player", 28px bold.
  - `ipInput` (TextField): host IP input (default: "127.0.0.1").
  - Two buttons in row: `hostButton` ("Host"), `joinButton` ("Join").
  - `backFromMultiButton` ("Back").
- **Class**: `card start-panel`.

### Host Waiting Panel (hostWaitingPanel, hidden by default)
- **Elements**:
  - Title: "Host Waiting for Players", 28px bold.
  - `playerNumText`: shows joined player count (e.g., "Joined Players: 0").
  - `levelDropdown`: level selection (host only).
  - `startButton` ("Start Game", hidden until players join).
  - `backFromHostButton` ("Back").
- **Class**: `card waiting-panel`.

### Client Waiting Panel (clientWaitingPanel, hidden by default)
- **Elements**:
  - Title: "Waiting for Host", 28px bold.
  - `clientPlayerNumText`: shows joined player count.
  - `backFromClientButton` ("Back").
- **Class**: `card waiting-panel`.

## Stylesheet (LobyUI.uss) Classes & Styling

### Utility Classes
- `.lobby-root`: main layout (column, centered, 100% height, 24px padding).
- `.container`: 720px max-width container.
- `.header`: 28px bold text.
- `.card`: 20px padding, 8px border-radius, dark background, border on hover.
- `.row`: flex-direction row for side-by-side layouts.
- `.input`, `.dropdown`: full width, 18px font size.
- `.btn`: 18px font, 10px top/bottom padding, flexes to fill space.

### Button Variants
- `.primary`: blue background (`rgb(52, 112, 224)`), white text, hover darkens.
  - **Float animation**: `.btn-float-up` applies `translate: 0 -8px`; `.btn-float-down` resets.
  - **Disabled**: opacity 0.6.
- `.secondary`: similar to primary; used for back buttons.

### State Classes
- `.hidden`: `display: none` (used to toggle panel visibility).

### Spacing Helpers
- `.spaced` (on `.row` child): `margin-left: 8px`.
- `.spaced` (on `.card` child): `margin-top: 12px`.

## Animation
- **Float animation**: applies transition on `.primary` buttons (0.8s ease-in-out).
- Use script to toggle `.btn-float-up` class when "Start Game" animates.

## Integration (LobbyUIManager)

### Binding at Runtime
- Fetch all named elements (`Query` or direct reference).
- Subscribe button clicks: `startGameButton`, `singlePlayButton`, `multiPlayButton`, `hostButton`, `joinButton`, `startButton`, etc.
- Show/hide panels by toggling `.hidden` class on container elements.
- Update dropdown options from available levels (extracted from `levelScenes` in inspector).

### Flow
1. Show `mainPanel` on start.
2. Click "Start Game" → show `modePanel`.
3. Click "Single Play" → show `singlePlayPanel`; populate `singleLevelDropdown`.
4. Click "Multi Play" → show `multiPlayPanel`.
5. Click "Host" → show `hostWaitingPanel`; populate `levelDropdown`; update `playerNumText` each frame.
6. Click "Join" → show `clientWaitingPanel`; update `clientPlayerNumText` each frame.
7. Click "Back" → return to previous panel.
8. On all players ready/host click "Start Game" → load game scene.

### Notes
- Use `AddToClassList("hidden")` / `RemoveFromClassList("hidden")` to toggle visibility.
- Update player count labels every frame while waiting (check `NetworkManager` player count).
- Apply float animation to start button for visual feedback.
