# VectoringTargetHUD Validation

## Functional checks
- One target in list: line points from HUD center to this target.
- Multiple targets: line tracks the target closest to HUD center.
- Temporary loss: line stays for 1 second in hold mode and uses tracking fallback.
- After hold expires: line hides or switches to nearest visible target.
- No target: line is hidden.

## Smoothness checks
- Endpoint movement is smooth during fast camera pans.
- No abrupt jumps on target switch near center due to hysteresis.
- Line angle and length update smoothly frame-to-frame.

## Debug checks
- Enable `DebugMode` and verify target name, mode (`live` / `hold`), age and confidence output.

## Stability checks
- No null-reference errors when target is destroyed.
- No duplicate HUD line objects after respawn or scene transitions.
