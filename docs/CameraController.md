# CameraController (Unity)

Follows the local player using a configurable offset and smoothing. Periodically searches for the local player by tag and `NetworkObject` ownership.

## Overview
- Offset: `offset` sets the camera position relative to the target.
- Smoothing: `smoothSpeed` controls `Lerp` strength in `LateUpdate()`.
- Target discovery: `InvokeRepeating` calls `TryFindLocalPlayer()` until a local player is found.

## Logic
- `LateUpdate()`: If `target` present, compute `desiredPosition = target.position + offset` and interpolate.
- `TryFindLocalPlayer()`: Find GameObjects with tag `Player`, pick the one whose `NetworkObject` is spawned and `IsLocalPlayer`.

## Public API
- `SetTarget(Transform newTarget)`: Manually assign target and snap immediately.

## Integration Notes
- Ensure players are tagged `Player` and have `NetworkObject` components.
- Tune `offset` for scene composition; negative Z for 3D follow behind.
