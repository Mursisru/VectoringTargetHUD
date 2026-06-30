**Developer:** Mursisru

# Vectoring Target HUD (Nuclear Option, BepInEx 5)

[![Nuclear Option](https://img.shields.io/badge/Game-Nuclear%20Option-blue)](https://store.steampowered.com/app/2168680/Nuclear_Option/) [![BepInEx 5](https://img.shields.io/badge/Loader-BepInEx%205-orange)](https://docs.bepinex.dev/) [![Version](https://img.shields.io/badge/Version-1.0.1-green)]() [![License](https://img.shields.io/badge/License-MIT-lightgrey)](LICENSE)


`VectoringTargetHUD_Engine` is a BepInEx 5 plugin for Nuclear Option that draws a direction marker for selected air targets on the Flight HUD.

## Features

- Wireframe prism-style direction marker with light fill.
- Target filtering for airborne targets with minimum movement speed.
- Anti-cheat style behavior:
  - marker requires active observation,
  - short hold window uses frozen last-known state,
  - no live tracking through lost visibility.
- Delayed marker appearance with damped blink settle effect.
- Live color sampling from HUD theme, hold color for lost target state.
- Configurable behavior through BepInEx config file.

## Requirements

- Nuclear Option (Steam)
- BepInEx 5 installed for the game
- .NET Framework 4.8 (for local build environment)
- Visual Studio (for source build)

## Install (prebuilt DLL)

1. Download release package from GitHub Releases.
2. Extract `VectoringTargetHUD_Engine.dll`.
3. Copy it to:

   `Nuclear Option\BepInEx\plugins\`

4. Launch the game once to generate/update config.

## Config location

Plugin config is stored at:

`Nuclear Option\BepInEx\config\com.at747.nuclearoption.vectoringtargethud.cfg`

## Build from source

1. Open `VectoringTargetHUD_Engine.slnx` in Visual Studio.
2. Ensure reference paths in `VectoringTargetHUD_Engine.csproj` match your local game install.
3. Build `Release`.
4. Use `bin\Release\VectoringTargetHUD_Engine.dll`.

## Notes on project references

This project uses local reference paths to game assemblies and BepInEx core files, for example:

- `NuclearOption_Data\Managed\Assembly-CSharp.dll`
- `NuclearOption_Data\Managed\UnityEngine*.dll`
- `BepInEx\core\BepInEx.dll`
- `BepInEx\core\0Harmony.dll`

If your game is installed in a different location, update `HintPath` entries in `VectoringTargetHUD_Engine.csproj`.

## Known limitations

- Marker visualization depends on target tracking state provided by game systems.
- HUD colors can vary by game mode/cockpit HUD implementation.
- Config values from previous versions may override new defaults until edited manually.

---

## Keywords

nuclear-option, bepinex, harmony, mod, vectoringtargethud, csharp, unity
