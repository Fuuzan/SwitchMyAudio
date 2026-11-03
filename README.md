# Switch My Audio

Quickly switch between speakers, headphones and microphones on Windows.

**v0.7.0 public beta** · Free and open source · By [Fuuzan](https://github.com/Fuuzan) · [MIT license](LICENSE)

## Screenshots

Demo devices and example settings are shown; no personal device information is included.

![Main window in dark mode](docs/images/main-dark.png)

![Compact pinned window with playback and microphones](docs/images/pinned-both-dark.png)

## Get started

1. **Install:** download the **Setup.exe** file from this project's **Releases** page. Open it, click **Install**, then **Open app**.
2. **Run again:** open the Windows **Start** menu, search for **Switch My Audio** and select it.

**Portable version:** extract all files from **portable.zip** into a folder and run **SwitchMyAudio.exe**. Keep all files together; use that EXE to run it again.

If the app is already running, click its icon in the **system tray** beside the clock (check the hidden-icons arrow).

**Requirements:** Windows 10/11 (Intel/AMD) and .NET Framework 4.8 or later. Windows 11 ARM64 emulation is untested on physical ARM hardware. [Compatibility details](docs/COMPATIBILITY.md).

**Unsigned download warning:** Windows may show **Windows protected your PC** or **Unknown publisher**. Download only from this project's Releases page and check the included SHA-256 checksums. If you trust the download, **More info → Run anyway** may be available. [Code signing status](docs/CODE-SIGNING.md).

## How to use

- **Switch audio:** open **Playback** or **Microphones** and click **Use** beside a device. Playback and microphone choices are independent.
- **Direct shortcuts:** click **Set shortcut** and press a key or combination to switch to that device from anywhere. Click the shortcut field again and choose **Clear shortcut** to remove it.
- **Cycle devices:** check **Cycle** on at least two devices in the same tab, then set a shortcut beside **Cycle selected sources**. Each press selects the next available checked device.
- **Rename devices:** hover over a device name and click **Rename**. Press Enter to save or Escape to cancel. Names change only in this app.
- **Compact pin mode:** click **Pin** for a small, always-on-top window. Drag to move it or drag an edge to resize. Click **Unpin** or press Escape to return.
- **Reorder & hide:** in **Settings → Devices**, drag the grip or use the arrows to reorder devices. This also sets the cycling order. Use the toggles to show or hide devices.

## Settings

- **Start with Windows:** on by default; launches quietly when you sign in.
- **Appearance & pinning:** follows your Windows theme by default. Choose another theme and which sources appear when pinned.
- **Level meters:** off by default. Enable them to show live sound activity; no audio is recorded.
- **Auto-switch headphones:** off by default. Enable it to prefer newly detected headphones. Windows may still switch devices on its own when this is off. Some wireless USB receivers stay connected while the headset is off, so detection can vary. [Detection help and reports](docs/HEADPHONES.md).

## Closing, updates & privacy

- **Close or quit:** closing the window sends the app to the system tray so shortcuts keep working. To exit completely, right-click the tray icon and select **Quit**.
- **Updating:** run the new installer. For portable copies, quit from the tray before replacing files. Your settings are kept.
- **Privacy & storage:** preferences stay in `%LOCALAPPDATA%\SwitchMyAudio`. Works offline, with no accounts or telemetry.

Apps with their own audio-device selection may need their output changed separately.

[Privacy](docs/PRIVACY.md) · [Build and release guide](docs/RELEASING.md) · [Release notes](docs/RELEASE-NOTES.md) · [Tested behavior and limits](docs/VERIFICATION.md)
