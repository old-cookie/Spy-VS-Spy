# GameUI (UI Toolkit, UXML)

Game scene UI layout for score display, pause menu, and item info panel. Structured with absolute positioning and row/column flexbox for responsive score comparison and menu overlays.

## Overview
- Root: `GameUI` full-screen container (`width: 100%; height: 100%`).
- Sections: score display (top center), end-game button (bottom center), pause menu and item info (floating overlays).

## Elements

### Score Display (ScoreContainer)
- **Layout**: Centered horizontal row at `top: 60px`.
- **Left side** (`OwnTeamContainer`):
  - `OwnFlagParent`: 150×150px visual space for team flag sprite.
  - `OwnScoreText`: 108px bold white text with black outline; player's team score.
- **Center** (`vsLabel`): "VS" separator, 72px bold white text with black outline.
- **Right side** (`OtherTeamContainer`):
  - `OtherScoreText`: 108px bold white text with black outline; opposing team score.
  - `OtherFlagParent`: 150×150px visual space for opposing team flag sprite.

### End Game Button (EndGameContainer)
- **Position**: Bottom center, `bottom: 150px`.
- **State**: Hidden by default (`display: none`).
- **Button** (`BtnEnd`): 60px font, padding 45×90px, blue background (`rgba(0, 120, 215, 0.8)`), white text, 15px border-radius.
- **Text**: "Go back to lobby (30s)" (countdown updated via script).

### Pause Menu (PauseMenu)
- **Position**: Full-screen overlay with `rgba(0, 0, 0, 0.65)` backdrop.
- **State**: Hidden by default.
- **Panel** (`PauseMenuPanel`):
  - Background: dark semi-transparent (`rgba(18, 26, 36, 0.95)`), 18px border-radius, shadow.
  - Title: "Game Paused", 72px bold white text with outline.
  - Buttons (48px font, 28×48px padding, 14px border-radius):
    - `BtnPauseContinue`: green (`rgba(0, 168, 107, 0.9)`), continues game.
    - `BtnPauseEnd`: red (`rgba(204, 36, 36, 0.92)`), returns to lobby.

### Item Info Panel (ItemInfoPanel)
- **Position**: Top-right, `top: 24px; right: 24px`.
- **State**: Hidden by default; shown when player holds item info key.
- **Content**:
  - `ItemNameText`: 36px bold white text; item name.
  - `ItemDescriptionText`: 24px white text; item description (wrap support).
- **Style**: Dark background (`rgba(18, 26, 36, 0.92)`), min-width 420px, max-width 720px, 12px border-radius, shadow.

## Bindings (Script Reference)
`GameController` must bind the following elements at runtime:
- `ownFlagParent`, `otherFlagParent`: assign flag sprites.
- `ownScoreText`, `otherScoreText`: update scores.
- `vsLabel`: separator label.
- `endGameContainer`: show/hide on match end.
- `btnEnd`: subscribe to back-to-lobby click.
- `pauseMenu`: show/hide on pause toggle.
- `btnPauseContinue`, `btnPauseEnd`: subscribe to click events.
- `itemInfoPanel`: show/hide on item hold.
- `itemNameText`, `itemDescriptionText`: update when holding item.

## Integration Notes
- All text uses black outline (`-unity-text-outline-width`, `-unity-text-outline-color`) for readability.
- Score display is centered and symmetrical; positions adjust with window size via `translate: -50% 0` and flexbox.
- Pause menu is modal; disable input handling when visible (guarded by `GameController.IsPauseMenuOpen`).
- Item info updates only when player has a held item; use `UpdateItemInfo(itemName, itemDescription)` pattern.
