# KeyR Automation System

<div align="center">
  <img src="website/appIcon.png" alt="KeyR Logo" width="150"/>
</div>

**KeyR** is a robust, lightweight macro automation software tailored for the Windows ecosystem. Built entirely in C# WPF on top of native Win32 low-level hardware hooks, KeyR aims to offer flawless, sub-millisecond playback parity with zero dependency overhead.

## Features

- **Native Win32 Hook Engine**: Avoids generic unmanaged bindings. KeyR taps straight into `WH_KEYBOARD_LL` and `WH_MOUSE_LL` for lag-free recording.
- **Physical Precision Playback**: Inputs are simulated via the high-resolution `SendInput` API for strict reliability against anti-cheat and low-polling rate environments.
- **Advanced Timing**: KeyR sports a multi-tier precision wait algorithm utilizing `Thread.Sleep()`, `Thread.SpinWait()`, and `Stopwatch` heuristics natively for sub-millisecond delta timings.
- **Foreign Macro Integration**: Seamlessly import external files:
  - **InformaalTask**: We map zero-based relative splines dynamically to your physical absolute desktop center perfectly.
  - **TinyTask**: Fully native 65K Grid algorithm scaling for accurate interpolation.

## Documentation

Comprehensive usage documentation can be found online at our site. We document the GUI features, timeline configurations, hotkeys, and best practices.
[Read the Documentation](https://kriswn.github.io/KeyR/documentation)

## Version Availability

All versions are actively archived within the tags of this repository. Cloud-compilation provides direct binaries for all pre-release candidates attached via GitHub Actions directly in the **Releases** tab.

- `v0.1.x` - Initial KeyR MVP.
- `v0.1.1` - Fixed Persistence of Settings.
- `v0.1.2` - Greatly increased Recording and Playback efficiency. Reduced App Size.
- `v0.2.0` - Added Auto-Restart functionality.
- `v0.3.0` - Complete redesign of the Settings Panel.
- `v0.3.1` - Foreign Macro Imports. Cleaner UI.
- `v0.3.2` - Low-Level WH Native precision transition.
- `v0.3.3` - Startup Dialogs.
- `v0.3.4` - Foreign Macro Import Fixes. Added Responsive UI Scaling.
- `v0.3.5` - UI/UX Overhaul. Added Pause/Resume Functionality.
- `v0.3.6` - Further UI/UX Improvements. Added Font Scale and Bold Text options.

## Building from source

1. KeyR targets `.NET 9.0 Desktop Runtime`.
2. Execute `dotnet publish src\KeyR.csproj -c Release -r win-x64 -p:PublishSingleFile=true` to fetch the self-contained output binary without needing precompiled targets.

---

*Formerly known as SupTask.*
