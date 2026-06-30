# Prism HUD Pointer Validation

## Mode checks
- `ShapeMode=Prism`: prism-style pointer is visible and line fallback is hidden.
- `ShapeMode=Line`: legacy line renderer works and prism is hidden.

## Visual checks
- Pointer starts at target center and points to target nose-dot direction.
- Prism appears as multi-segment pseudo-3D shape (center spine, side edges, base, tip).
- Live mode uses live color palette; hold mode uses hold palette.
- Alpha gradient from base to tip is visible.

## Stability checks
- No flicker in width/depth during quick camera motion.
- Hold state (1s) preserves pointer without abrupt jumps.
- Target switching near HUD center respects hysteresis (no rapid oscillation).
- No null errors when target disappears or becomes invalid.

## Performance checks
- Frame update remains smooth in combat with multiple targets.
- No duplicated HUD objects after scene reload/respawn.
