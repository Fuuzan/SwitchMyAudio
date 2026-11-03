# System requirements

- Windows 10 22H2 or Windows 11 on an Intel/AMD PC.
- .NET Framework 4.8 or later.
- Audio devices supported by Windows Core Audio.

One download supports 32-bit and 64-bit Intel/AMD Windows. Installation is per user and does not require administrator access.

Windows 11 ARM64 may run the app through Windows emulation, but ARM hardware is untested. Windows 7/8/8.1, Windows S mode, Server Core and non-Windows systems are not supported.

## Known limitations

- Apps with their own audio-device selection may ignore changes to the Windows default device. Change their output separately if needed.
- Some shortcuts are reserved by Windows or another app. Choose a different combination if a conflict is reported.
- Automatic headphone switching depends on Windows reporting a connection and recognizing the device. Wireless USB receivers may stay connected while the headset is off. See [headphone help](HEADPHONES.md).
- Display scaling, multiple monitors and audio drivers can affect behavior. Compatibility with every Windows device is not guaranteed.

If something does not work, report the app version, Windows version, device model and steps to reproduce it through the project's GitHub Issues page.
