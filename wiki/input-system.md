# Input System

<!-- 2026-05-07 -->

## Unity 6 URP template uses New Input System by default

The "Universal 3D" template sets **Active Input Handling = Input System Package (New)**.
Our scripts use `UnityEngine.Input` (the old/legacy system: `Input.GetAxisRaw`,
`Input.GetKeyDown`, `Input.GetAxis`). Running with only the new system active throws:

```
InvalidOperationException: You are trying to read input using the UnityEngine.Input class,
but you have switched active Input handling to Input System package in Player Settings.
```

## Fix applied

Changed **Active Input Handling** to **Both** via:
`Edit > Project Settings > Player > Other Settings > Active Input Handling → Both`

Unity prompts for an editor restart; click Apply. After restart, legacy `UnityEngine.Input`
works alongside the new Input System.

## Where the setting lives on disk

`ProjectSettings/ProjectSettings.asset` — field `activeInputHandler`.
- `0` = Input Manager (Old)
- `1` = Input System Package (New)
- `2` = Both  ← current setting

## Consideration for the future

If the project later adopts the new Input System fully, rewrite the controllers to use
`InputAction` assets instead of `UnityEngine.Input`, and switch back to `1`.
