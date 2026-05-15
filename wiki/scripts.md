# Scripts

<!-- 2026-05-15 -->

## PlayerController.cs

Ported verbatim from `unity-game-poc/Assets/Scripts/PlayerController.cs`. Owns:

- WASD physics movement, camera-relative direction
- Jump (Space) with ground check raycast
- Animator `speed` parameter driven by XZ velocity magnitude
- Speed thresholds: `moveSpeed = 6f`, Walk→Run blend triggered at 4.5 u/s

Constraints:
- `Rigidbody.freezeRotation = true` — rotation driven entirely by script
- `applyRootMotion = false` on the Animator — script owns translation
- Pivot must be at feet (Y=0) for the 0.15f groundCheckDistance to work; the
  cylinder fallback uses 1.1f because Unity cylinders have pivot at center.

## CameraController.cs

Started from `unity-game-poc/Assets/Scripts/CameraController.cs`, extended with:

### Over-the-shoulder offset
`shoulderOffset = 0.4f` — shifts the look-at point along the camera's right vector,
which moves the camera position laterally too. Net effect: character appears in the
LEFT third of the frame, leaving the right ~2/3 visible for what they're looking
at.

### Auto-recenter on forward motion
Triggered when ALL of:
- not currently MMB-dragging
- not currently holding Q or E (rotation keys)
- `Time.time - _lastDragEndTime >= recenterGracePeriod` (default 1.0s)
- `Input.GetAxis("Vertical") > forwardThreshold` (default 0.5)

Target yaw = `target.eulerAngles.y + defaultYawOffset`. Because PlayerController
already rotates the character to face the move direction (camera-relative
forward), recentering yaw onto the character's facing direction puts the camera
behind them again.

Backward motion (S) and pure strafe (A/D only) do NOT trigger recenter, by spec.

### Default yaw convention
`yaw = 0f` means camera is behind a character at default rotation (facing +Z).
This differs from `unity-game-poc/` which used `yaw = 180f` and depended on the
character snapping to face -Z on first W press. The new default avoids that
visible snap.

### Public tunables (Inspector)
- `shoulderOffset` (0.4f): OTS lateral shift; set to 0 for centered framing.
- `recenterGracePeriod` (1.0f): seconds after MMB release before recenter starts.
- `recenterSpeed` (2.5f): lerp rate for the recenter.
- `forwardThreshold` (0.5f): minimum `Input.GetAxis("Vertical")` value to trigger.
- `defaultYawOffset` (0f): offset added to target yaw — set to 90 for over-LEFT-shoulder.
- `defaultPitch` (15f): pitch the recenter returns to.
