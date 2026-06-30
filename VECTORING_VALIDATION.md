# VectoringTargetHUD Validation

## Selection
- One target in list: line points to that target.
- Multiple targets: line chooses nearest visible target to HUD center.
- Hysteresis prevents rapid target oscillation near center.

## Hold and fallback
- On brief loss, hold keeps line for 1 second.
- During hold, fallback uses `TrackingInfo.GetPosition()`.
- After hold expiry with no recovery, line hides.

## Smoothness
- Endpoint smoothing reduces frame-to-frame jitter.
- Angle and line length transition smoothly.
- No large jumps during quick camera pans or target maneuvers.

## Visibility
- No valid target: line hidden.
- Target behind camera and no fallback point: line hidden.
- Minimum line length threshold respected.

## Debug and logs
- Debug mode shows target name and state.
- No errors in BepInEx logs while switching targets.
