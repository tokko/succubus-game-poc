# Unity Setup

<!-- 2026-05-07 -->

## Versions

- **Unity Hub**: 3.17.3, installed at `C:\Program Files\Unity Hub\Unity Hub.exe`
- **Unity Editor**: 6000.0.74f1 (Unity 6 LTS), installed at
  `C:\Program Files\Unity\Hub\Editor\6000.0.74f1-x86_64\Editor\Unity.exe`
- **Render pipeline**: Universal Render Pipeline (URP) — "Universal 3D" Core template

## Install method

`winget install Unity.UnityHub` **fails** with hash mismatch (exit 17) because Unity updates
their installer without updating the winget manifest. Workaround: download directly from
`https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-x64.exe` and run with `/S`.

## Batch mode is not available on Personal plan

Unity 6 requires the `com.unity.editor.headless` Pro entitlement to use `-batchmode`.
Running `Unity.exe -batchmode -quit -createProject` fails with a licensing error on Personal.
**Workaround**: create projects through the Unity Hub GUI only.

## Unity Hub headless CLI

Output goes to stderr. Use `cmd /c '"path\Unity Hub.exe" -- --headless <cmd>' 2>&1` to
capture it in PowerShell. Direct `& $hub -- --headless ...` suppresses stderr output.
