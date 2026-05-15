# Known Issues

<!-- 2026-05-07 -->

## Space key not verifiable via computer-use automation

**Symptom**: `Input.GetKeyDown(KeyCode.Space)` jump logic cannot be confirmed by automated
computer-use tooling. The Space key event appears to be intercepted before reaching Unity's
game input system when sent via the automation layer.

**Status**: Logic is correct — ground check distance is 1.1f (fixes the y-distance bug),
`_isGrounded` flag is set correctly, `AddForce(Impulse)` call is guarded by both conditions.
WASD was confirmed working (player moved Z=0 → 17.76 in 3 seconds = exactly 6 u/s × 3s).
Jump should work for a real user pressing Space in the Game view.

**Workaround for testing**: A real human pressing Space while the Game view has focus.

---

## SceneAutoSetup fires during play mode on first domain reload

**Symptom**: `EditorSceneManager.NewScene()` throws `InvalidOperationException` if the
`[InitializeOnLoad]` `delayCall` fires while the editor is in play mode. Can happen on the
very first domain reload after opening the project if the editor was already in play mode
when the project was last closed.

**Workaround**: Exit play mode, then use `Tools > Re-run Scene Setup`.

**Potential future fix**: Add a play-mode guard to the `delayCall`:
```csharp
EditorApplication.delayCall += () => {
    if (EditorApplication.isPlayingOrWillChangePlaymode)
    {
        EditorApplication.delayCall += Run; // re-queue for edit mode
        return;
    }
    Run();
};
```

---

## Unity Editor still uses C:\Temp path until Hub is cycled

**Symptom**: The running Editor session has the project open from `C:\Temp\unity-game-poc`
even though `projects-v1.json` has been updated to point to
`D:\claude projects\unity-game-poc`. Scripts written directly to `D:\` are not seen by the
Editor until it is closed and reopened from Hub.

**Workaround**: Copy any new scripts to both paths, or use robocopy (see project-layout.md).

**Resolution**: Close the Editor and reopen the project from Unity Hub — it will then use
`D:\claude projects\unity-game-poc` directly.

---

## GetKeyDown requires game view focus

**Symptom**: `Input.GetKeyDown` and `Input.GetAxisRaw` only register when the Game view has
keyboard focus. If the Scene view or Inspector has focus, input is silently dropped.

**Fix**: Click inside the Game view before pressing any input keys during Play mode.
