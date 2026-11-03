# Windows compatibility

The beta uses Windows Core Audio and the .NET Framework already supplied with modern Windows. The app and Setup are AnyCPU builds: they run as 64-bit processes on Intel/AMD 64-bit Windows and as 32-bit processes on 32-bit Windows. No architecture-specific download choice is needed.

| Environment | Intended behavior | Evidence / status |
|---|---|---|
| Windows 11, Intel/AMD x64 | Native 64-bit app and Setup | Local automated UI, Core Audio and resource tests; available host only |
| Windows 10 22H2, Intel/AMD x64 | Native 64-bit app and Setup | APIs/runtime reviewed; separate Windows 10 installation not tested locally |
| Windows 10 22H2, Intel/AMD x86 | Native 32-bit app and Setup | 32-bit tests run under WOW64 on the available x64 host; physical 32-bit Windows still needs a smoke test |
| Windows 11 ARM64 | Windows x64 emulation | Expected from Microsoft's documented .NET Framework AnyCPU behavior; physical ARM hardware untested |
| Windows 7/8/8.1, Windows 10 ARM, Windows S mode, Server Core, non-Windows | Outside this beta's support scope | No compatibility promise |

Requires .NET Framework 4.8 or later. Microsoft includes 4.8 in Windows 10 22H2 and 4.8/4.8.1 in Windows 11. Earlier Windows 10 releases with 4.8 may work, but are not additional tested targets. Normal Windows rules for Store-only S mode still apply; this desktop app is distributed through GitHub.

## What is portable between PCs

- Core Audio discovers local playback and recording devices; no personal endpoint IDs are built into the app. Saved settings belong to that user's Windows account.
- Pinning, dragging, keyboard shortcuts, optional meters, and light/dark themes use Windows APIs. Newer caption colors fall back to the standard frame when Windows does not support the attribute.
- The native paint/dropdown structures are checked at runtime by both 32-bit and 64-bit tests. Handles and pointers use pointer-sized types.
- The installer is per user, works without administrator rights, keeps settings on update, and ships its license. Portable users keep the extracted files together.
- Long or non-English device names do not change how Windows identifies the device. Aliases support Unicode, including Arabic and emoji.

## Hardware and display limits

Windows audio drivers must expose usable Core Audio devices. Reconnecting wired, USB and Bluetooth headphones can trigger automatic switching only when Windows reports a connection and supplies recognizable headset metadata. A USB receiver that stays connected while its headset powers off may not report a new event. Apps that explicitly select an output can ignore the default Windows route. Audio service failures are retried a limited number of times; the app can be refreshed after service recovery.

Global shortcuts can be reserved by Windows or another app. Those conflicts are reported and can be changed. No application can guarantee every key combination.

The layout uses DPI-aware sizing and vertical scrolling. Mixed-monitor DPI changes, touch/stylus dragging, very high text scaling, remote sessions and actual low-resolution displays need additional physical checks. The pinned view has a readable minimum size. A very small effective desktop may not fit the full window.

Default-device switching uses the isolated, undocumented Windows PolicyConfig interface. It works in the tested environment but can change after OS updates; this is a reason to start with a public beta.

## Before promoting to 1.0

Try installation, update, startup, playback/microphone switching, headphone reconnect, tray quit, pin/unpin, and uninstall on a second Windows 11 PC and on Windows 10. Add a physical ARM device if ARM support is advertised. Include 125–200% scaling, a mixed-DPI monitor move, sleep/resume, and a longer real-audio session. GitHub CI's Server-based runners add coverage but do not replace these desktop/hardware checks.

Sources: [Microsoft runtime requirements](https://learn.microsoft.com/en-us/dotnet/framework/get-started/system-requirements), [Microsoft .NET Framework AnyCPU on ARM](https://devblogs.microsoft.com/visualstudio/how-to-run-net-apps-natively-on-arm64-devices/), [Windows emulation on ARM](https://learn.microsoft.com/en-us/windows/arm/apps-on-arm-x86-emulation).
