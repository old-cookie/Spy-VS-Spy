# ScoreZone (Unity)

Trigger zone for delivering flags to score team points.

## Overview
- Team-gated: Only players on `scoreTeam` can score here.
- Flag requirement: Player must be carrying a flag (`TeamMember.HasFlag`).
- Points: Awards `pointsPerScore` via the game controller.

## Logic
- `OnTriggerEnter(Collider other)`: if `other` is tagged `Player`, obtain `TeamMember` (component or parent), check `IsOnTeam(scoreTeam)`, then call `TryScoreFlag()`.
- On success: call `GameController.Instance.AddScoreRpc(scoreTeam, pointsPerScore)`.

## Integration Notes
- Ensure players have `TeamMember` and are tagged `Player`.
- Place zones for each team’s base; configure `scoreTeam` appropriately.
