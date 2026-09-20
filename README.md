# 4sibi

A compact Windows macro and autoclicker utility with customizable hotkeys, profiles, and a minimal status overlay.

**Version:** 1.3.2 · **Platform:** Windows 10 / 11, x64 · **Status:** Beta

## Download

Open the **Releases** section of this repository and download `4sibi.exe` from the release assets. GitHub's automatically generated source archives are not the ready-to-run application.

No installer or separate .NET installation is required. Close any previous instance before starting a new version.

The current executable is **not digitally signed**. Windows may display an unknown-publisher or SmartScreen warning. GitHub hosting does not add a Windows code signature.

## Features

- Left-click, right-click, or single-key actions.
- Adjustable target rate from **1 to 1,000 actions per second**.
- Custom keyboard or mouse activation binding.
- Toggle and hold-to-activate modes.
- Configurable emergency-stop hotkey.
- Roblox desktop-client detection and foreground restrictions.
- Create, save, and load macro profiles.
- Import and export settings and profiles.
- Dedicated test area with measured results.
- Compact split overlay with adjustable size, opacity, position, and displayed information.
- Six accent colors and English / German interfaces.
- Optional startup, minimize-to-tray, sound, and always-on-top settings.

## Quick start

1. Run `4sibi.exe`. The macro starts paused.
2. Select the action and target APS in **Macro**.
3. Choose an activation binding. The default is **F6**.
4. Select **Toggle** or **Hold to activate**.
5. If Roblox-only mode is enabled, switch to the Roblox desktop client before activating.
6. Press **F12** to stop immediately, unless you have changed the emergency-stop binding.

Drag the title bar to move the main window. Drag the unlocked overlay to reposition it; right-click it for options. The tray menu provides access to the app, overlay settings, macro stop, and exit.

## Roblox mode and timing

Roblox detection identifies the desktop player process. It does not identify a specific experience, detect balls, or react to gameplay. Browser tabs and Roblox Studio are not treated as the player client.

APS is a requested target, not a guaranteed rate. Windows scheduling and the target application's handling of input can affect the result. The app uses normal Windows `SendInput` events; it does not modify the game client.

## Local data

Settings and profiles are stored in `%LOCALAPPDATA%\4sibi`. Keep a settings export if you want a portable backup.

The application operates locally and does not require a network connection or send telemetry. If you enable Windows startup, keep the executable at the same location, or disable and re-enable that setting after moving it.

## Validation

Version 1.3.2 passed **102 automated checks** covering application logic and UI behavior. These checks do not emit operating-system input. Separate Windows 10 / 11 machine testing and a live Roblox test have not been completed; compatibility across all setups is not guaranteed.

## Feedback

Use the repository's **Issues** section to report problems. Include your Windows version, 4sibi version, steps to reproduce, and a screenshot if helpful. Do not upload personal data or signing credentials.

4sibi is an independent project and is not affiliated with Roblox.
