# ClientNetworkAnimator (Unity Netcode)

Client-authoritative `NetworkAnimator` allowing local clients to drive their own animation parameters. Ideal for player characters where input and animation state are controlled client-side.

## Overview
- Authority: Overrides server authority by returning `false` in `OnIsServerAuthoritative()`.
- Use-case: Attach to objects whose owner client should set animator triggers/parameters (e.g., outcome animations, pick/jump triggers).
- Behavior: Replicates animator parameter changes across the network while preserving client ownership rules.

## Integration
- Pair with a local `Animator` and player controller logic (e.g., `PlayerController` sets triggers/plays states).
- Ensure the object has a `NetworkObject` and is spawned with proper ownership.
- Prefer state names for looping animations; triggers for one-shots.

## Tips
- Keep parameter usage consistent; verify parameters exist via animator settings.
- Avoid server-only animation control for client-owned players to prevent desync.
