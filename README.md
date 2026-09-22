# Voicemeeter Fancy OSD

![Voicemeeter Fancy OSD](https://i.imgur.com/hleMUFv.gif)

Voicemeeter Fancy OSD is a small on-screen display for Voicemeeter. It shows gain, routing and mute changes without needing to keep the main Voicemeeter window open, and it can stay visible over many fullscreen and borderless applications.

This repository is a maintained fork of the original [A-tG/VoicemeeterFancyOSD](https://github.com/A-tG/VoicemeeterFancyOSD) project.

## Download

Download the latest build from this fork's [Releases](https://github.com/ForthtuN/VoicemeeterFancyOSD/releases) page.

The program is portable. Extract the archive and run `VoicemeeterFancyOsdHost.exe`.

Windows 10 or newer is recommended. The current build uses .NET 10 Desktop Runtime. If it is not already installed, you can install it with:

`winget install Microsoft.DotNet.DesktopRuntime.10`

## What's improved in this fork

- Fixed an intermittent issue where the OSD could stop appearing even though the app was still running.
- Smoother gain movement and better visual feedback while changing volume continuously.
- Improved fade and control animations.
- Fixed the Very Fast polling option so it actually runs at 140 Hz.
- Reduced unnecessary work while using high polling rates.
- Improved configuration reloads, shutdown handling and general runtime reliability.
- Safer application updates with better rollback behavior.
- Improved fullscreen overlay cleanup so the render window is not left behind after the OSD hides.

See [CHANGELOG.md](CHANGELOG.md) for release history.

## Known limitations

- Fullscreen overlay support depends on Windows and the application being used. If Xbox Game Bar or the normal Windows volume overlay cannot appear above an application, this OSD may not be able to either.
- Some OpenGL applications may not show the OSD correctly.
- NVIDIA RTX HDR can still temporarily drop in some games while the OSD is visible. This is still being investigated.

## Usage

Once running, Fancy OSD reacts to Voicemeeter changes automatically. For example, changing gain through Voicemeeter Macro Buttons will show the updated value in the OSD.

The tray icon gives access to settings and pause controls.

## Troubleshooting

`VoicemeeterFancyOsdHost.exe`, `hostfxr.dll` and `DXGI.dll` are used for fullscreen overlay support. If the host executable does not start, try launching `VoicemeeterFancyOsd.exe` directly. The normal executable may work when the fullscreen host does not, but fullscreen overlay support can be reduced.

Avoid running the program from `Program Files`, since Windows permissions can interfere with configuration files and self-updates.

## Building

The main application is a .NET 10 WPF project.

- Select x64 or x86 as the target platform.
- Rebuild the solution.
- Build output is written to the matching platform folder in the solution directory.
- Use `VoicemeeterFancyOsdHost.exe` when testing fullscreen overlay support.

## Credits

Original project by [A-tG](https://github.com/A-tG/VoicemeeterFancyOSD).

The project also uses:

- [ini-parser](https://github.com/rickyah/ini-parser)
- [A-tG/voicemeeter-remote-api-extended](https://github.com/A-tG/voicemeeter-remote-api-extended)
- [WpfScreenHelper](https://github.com/micdenny/WpfScreenHelper)
- [Hardcodet NotifyIcon for WPF](https://github.com/hardcodet/wpf-notifyicon)
- code derived in part from [ModernFlyouts](https://github.com/ModernFlyouts-Community/ModernFlyouts)
