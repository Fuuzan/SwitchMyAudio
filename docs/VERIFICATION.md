# Verification

## 0.7.0 public beta

### Final GitHub upload build

The final same-version build removes Map and Forget, including the unused mapping dialog and layout branches. Disconnected rows now use the same compact two-line height as other rows with details. Preferences are retained, and a reconnection check confirms that the same device keeps its alias, shortcut, cycle choice and order. The portable README.txt matches the author's exact approved text.

- **208 passed, zero failures** in the final 64-bit native/regression/resource run, with real audio enumeration/meters and temporary global shortcuts. No audio routes were changed.
- **192 passed, zero failures** in the final 32-bit regression/resource run.
- Both full runs include 100 mixed UI cycles and 50 complete disposal lifetimes. Owners became collectible and resource usage remained within the bounded test limits. The 64-bit 100-cycle process handles changed from 513 to 491; GDI/USER changed from 118/120 to 116/120. The 32-bit 50-lifetime GDI/USER/process counts stayed at 81/8/470. These observations do not establish universal absence of leaks.
- **53 focused UI checks passed**, covering the removed actions, compact row height, reconnection preferences, native scrolling, footer access, themes and dropdown behavior.
- A separate critic inspected all four current dark/light scrolled captures at normal/minimum sizes: **8.7/10, PASS**. The disconnected row is visible in the normal-size captures; narrow captures independently verify footer/scrolling layout. This is still-image review, not frame-by-frame desktop verification.

The final source/build keeps version 0.7.0.0, MIT licensing and Fuuzan credit. A fresh Publish handoff separates repository contents from the four release downloads and excludes extracted test copies. Packaging verifies matching installer/portable payloads, checksums, archive contents, license, neutral executable architecture and the publication privacy scan. The extracted clean source is also compiled independently. Existing physical-hardware and Windows-version limits in COMPATIBILITY.md remain applicable; no universal compatibility claim is made.

GitHub Actions has not run in the actual repository, and SignPath enrollment/signing is still pending publication and approval. Current downloads are unsigned. Earlier sections below record prior build evidence separately.

### Selected publication changes and first-run defaults

The same 0.7.0 version now documents unsigned-download warnings, headphone receiver recognition/reporting and retained QuietSwitch migration. The portable help file is README.txt. The app keeps automatic metadata-based detection and the verified Maxwell model rule; no extra headphone-selection UI or unverified hardware identifiers were added.

New users start with Start with Windows on, System theme, meters off and auto-switch headphones off. The first normal launch saves preferences before requesting startup registration, once only. Existing settings, recovered files and migrated preferences are not treated as new installations. Demo and custom-config entry points do not initialize Windows startup. Isolated tests use a registration callback rather than changing the tester's startup entry, and cover repeat launches, saved choices, failed registration, failed preference writes and the displayed Settings defaults.

- Final 64-bit native/regression/resource run: **207 passed, zero failures**, including real audio enumeration/meters and temporary global shortcut checks, 100 mixed UI cycles and 50 full disposal lifetimes. No audio routes were changed.
- Final 32-bit regression/resource run: **191 passed, zero failures**, including the defaults, UI, migration-format and resource checks, 100 cycles and 50 full disposal lifetimes.
- An initial sandboxed native run could enumerate audio but Windows denied shortcut registration with error 5. The final native run with normal Windows access passed; this was an execution-permission limitation, not a waived test.
- Release checks validate the renamed portable guide, matching installer/portable payloads, checksums, neutral executable architecture and MIT license. Negative checks reject both unsigned signing-required paths and an incorrect portable payload even after its archive checksum is regenerated.
- All five embedded PowerShell blocks in the release workflow parse, as do the project scripts. Document/image links and the 51-file publication privacy scan pass. Hosted GitHub execution and real SignPath signing remain unverified until the public project is published and approved.
- Independent screenshot critic: **9/10, PASS**. The two images show synthetic demo devices and example settings, including optional meters. The main image renders application client content without a simulated native titlebar; the compact image renders the borderless view. Native desktop screenshot access was unavailable, so these are app-rendered stills, not live desktop captures or transition recordings. PNG metadata contains only color/DPI data.

The 64-bit 100-cycle run retained bounded resources: managed memory 0.84 MB to 2.50 MB, private memory 43.04 MB to 44.09 MB, process handles 525 to 512, GDI 118 to 122 and USER 120 to 121. Across 50 subsequent disposal lifetimes, owners became collectible and process handles stayed at 512; GDI/USER remained within the test bounds. The 32-bit disposal run kept GDI/USER/process counts at 81/8/471. These finite tests do not prove universal absence of leaks or hardware-specific bugs.

SignPath is optional and disabled by default. The prepared flow signs the app before packaging, then signs Setup and refreshes checksums; enabled signing fails closed. The current supplied downloads remain unsigned. No signing account, token, approval or certificate is included, and no signing/network component was added to the installed app. The earlier compatibility and long-run evidence below remains a separate baseline.

### Devices scrolling refresh (same version)

The supplied scrolling screenshot exposed an empty footer label covering the center of Refresh sources. A native child hit-test regression failed before the fix. The footer now creates no empty label or divider; its button is sized using the existing DPI scale. Disconnected-source descriptions also stop above Map/Forget rather than covering them. Ordinary device names appear once; renamed sources retain their original name underneath.

Scrolling now lets AutoScroll move existing child windows without a second Arrange call in the scroll handler. Checks cover native wheel, line, page and end scrolling, unchanged row document positions, two themes and normal/minimum window sizes. The vertical bar is retained, the horizontal bar stays absent, the footer action is fully reachable and refreshing still works. Headphone help explains that Windows may switch independently of the app's toggle. No new timer or animation was added.

- Focused UI checks: **52 passed, zero failures**.
- Refreshed 64-bit native/regression/resource run: **199 passed, zero failures**, including real enumeration/meters and temporary shortcut checks, 100 mixed UI cycles and 50 complete disposal lifetimes. No real audio routes were changed.
- Refreshed 32-bit regression/resource run: **183 passed, zero failures**, including the scrolling checks, 100 mixed UI cycles and 50 complete disposal lifetimes.
- Separate visual critic: **8.7/10 — PASS**, with no material or blocking defect after inspecting scrolled dark/light layouts at both sizes and the retained main/pinned layouts.

The isolated live preview was also checked with actual mouse-wheel scrolling to the bottom, a click on the repaired Refresh sources action, and dragging the vertical scrollbar back to the top. The button remained whole and the source list refreshed. The preview used synthetic devices and did not write user preferences. The desktop observations are not a frame-by-frame recording or a physical mixed-DPI test.

Across the refreshed 64-bit 100-cycle run, managed memory changed from 0.83 MB to 2.44 MB, private memory from 42.55 MB to 43.86 MB, process handles from 516 to 499, GDI handles from 114 to 130 and USER handles from 119 to 123. Across the following 50 disposal lifetimes, weak references to owners became collectible, process handles stayed at 499, GDI changed from 81 to 95 and USER from 23 to 28. The refreshed 32-bit run retained bounded resources as well; its 50-lifetime GDI/USER/process counts stayed at 81/8/466. These are bounded workload observations, not proof that every driver or future workload is leak-free.

All downloads and checksums are regenerated together because the public version remains **0.7.0**. MIT licensing and Fuuzan credit are unchanged. Earlier results below describe the original compatibility audit rather than a repeated 1,000-cycle run of this scrolling refresh.

### Original 0.7.0 compatibility audit

Local host: Windows 11 build 26200, tested in real 64-bit and 32-bit processes. The app and Setup now use architecture-neutral IL. This is process-architecture coverage on one machine, not testing on every Windows PC.

- The extended 64-bit native run passed **370 assertions**, including **1,000 mixed UI cycles**, real Core Audio enumeration/meters, temporary hotkey conflicts/lifetimes, connection-event simulations, pin/unpin, resize, theme changes, rename, Settings navigation, and disabled/hidden meter behavior. It did not change real audio routes.
- A separate final 64-bit native regression run passed **163 assertions** with the license payload and current source. A foreground-dependent popup assertion in an earlier repeat exposed a test setup issue: the popup test now explicitly activates Settings and focuses the combo before sending its mouse messages. The final run observed an active, focused, open popup. No application change was needed for that test precondition.
- The final **32-bit native** run also passed **163 assertions**, including actual Core Audio enumeration/meters and temporary global-shortcut lifetime checks. Its dropdown opened with focus in an active Settings window.
- The 32-bit regression/resource run passed **176 assertions**, including **100 mixed UI cycles** and **50 complete application/window lifetimes**. Weak references confirmed the disposed controllers, windows, Settings dialogs and pinned windows became collectible; GDI/USER/process handle counts stayed bounded.
- The final 64-bit regression/resource run also passed **176 assertions**, including 100 mixed UI cycles and 50 complete lifetimes. GDI/USER counts stayed at 76/7 across those lifetimes, and process handles stayed at 448. All final runs completed with zero failures.
- Native structure sizes, empty-start device discovery, hiding the last source, viewport-only work with 62 devices, Unicode/emoji aliases, oversized XML rejection, install/update staging, replacement rollback and license deployment are covered by tests.

Across the extended 1,000-cycle 64-bit run, managed memory changed from 0.83 MB to 2.48 MB, private memory from 42.61 MB to 43.89 MB, process handles from 513 to 496, GDI handles from 122 to 130, and USER handles from 121 to 124. Cached source/window controls remained the same objects. No unbounded growth was detected in this workload. The 32-bit 50-lifetime check ended with GDI/USER counts unchanged at 81/8 and process handles falling from 470 to 468. These bounded tests cannot prove universal absence of bugs or leaks.

The independent critic opened all 15 current renders, reviewed compatibility/lifetime/release changes and scored **8.6/10 — PASS for public beta**, with no blocking defect found. The suggested release-guide example was corrected.

Release packaging checks validate checksums, neutral executable flags, matching installer payloads, the MIT notice, clean ZIP contents, and absence of matched local account names/paths or credential markers. Local raw native reports and user preferences are kept out of publication packages. The code and captions retain the existing minimalist design; no new permanent timer was added. Empty/hidden/disabled meters stop their shared timer.

The GitHub workflows are prepared for AnyCPU/x86 on Windows Server 2022 and 2025, with an extended release test, but have not been run in the real GitHub repository. See [compatibility limits and remaining physical checks](COMPATIBILITY.md): a separate Windows 10 installation, physical 32-bit Windows, ARM hardware, mixed-DPI monitor moves, real USB/Bluetooth reconnects, sleep/resume and longer real-driver sessions remain manual beta checks. No frame-by-frame recording establishes zero transient frames on every display/driver.

## 0.6.1

The Settings reveal path paints its buffered content while the form is cloaked, then reveals it in a finally-protected path. Tests observe painting while cloaked, verify the form is not left cloaked, reopen it and exercise the actual modal path.

The native dropdown now owns WM_PAINT/WM_ERASEBKGND/WM_PRINT drawing, uses a buffered pointer surface and themes its native popup backdrop. Checks cover dark/light/dark transitions, native mouse down/up messages, F4 opening, Escape closing, arrow-key selection, native accessibility role and model updates. Tooltip help is shared with the buffered face. No new permanent animation or polling timer was added.

Arrow and drag ordering share the same retained-row move operation. Tests cover enabled/disabled boundaries, click movement in both directions, updated boundaries after dragging, unchanged row handle/visibility and no overlap at minimum width.

Separate critics scored 7.5/10 and **8.5/10**. The first review found clipped arrow text; glyph-only arrow buttons corrected it. Still renders and native paint/input-message checks are not a high-frame-rate recording of physical pointer interaction, and do not prove zero transient frames on every Windows/display configuration.

Live desktop checks with the isolated demo opened the actual modal Settings window and its dark native dropdown, moved a playback device with the down arrow, then restored the order with a complete grip drag-and-drop. The main source list reflected both reorder operations. Native popup selection and keyboard behavior are covered by the automated input-message tests; the desktop automation activates the owner before each action, which interrupts an already-open popup. No high-frame-rate transition recording was captured.

The final combined native and 100-cycle run passed **181 assertions** with zero failures. The focused UI run passed **45 assertions**. Settings content was observed painting while cloaked; the modal/nonmodal reveal and dropdown paint tests passed. Across the native stress run, process handles changed from 527 to 512, GDI handles from 126 to 128 and USER handles from 122 to 123. Managed memory changed from about 0.81 MB to 2.39 MB and private memory from about 39.37 MB to 39.76 MB. These bounded-workload measurements do not prove universal absence of leaks. Hidden meter work remained stopped.

## 0.6.0 baseline

The test suite covers theme/background painting, repeated pin/unpin with preserved bounds, cached controls and pages, reorder position/flow isolation, vertical scroll position, horizontal native scrollbar suppression at narrow sizes, meter suspension while hidden, settings recovery and update transaction rollback. Existing audio, hotkey, rename and connection-event regression checks are retained.

The combined native and stress run passed **156 assertions** with no failures, including real audio enumeration/meters and temporary hotkey conflict/lifetime checks. It did not change real audio routes. Across 100 mixed UI cycles, process handles changed from 528 to 520, GDI handles from 129 to 121, and USER handles from 123 to 121. Managed memory changed from about 0.8 MB to 2.35 MB; private memory from about 39.0 MB to 39.6 MB. These are bounded-workload observations, not a universal leak guarantee.

A five-second synthetic visible-meter sample recorded 156 ms CPU over 5.011 seconds, about 3.1% of one CPU core, with 126 peak reads and no meter-target re-preparation. It is a short test-process measurement, not a native-driver CPU ceiling or a direct comparison against an earlier build. Hidden-window reads were zero.

Three separate design critics scored 8.2/10, 8.6/10 and finally **8.7/10**. The reviews passed after cached Settings save-failure rollback, the Settings icon, group-edge drop targets and marker updates during auto-scrolling were improved. Three additional focused drag-handler assertions pass, including a changed release position that must replace a stale marker and rejection of external drag data. Full live pointer drag verification was attempted but app-access approval timed out. Reordering handlers, preserved rows and scroll position were verified by automated tests; the complete live gesture and frame-by-frame motion remain unverified.

The new display changes add no permanent animation timer. A DWM flush is used at a mode transition only. Device reordering saves once on drop; while dragging, the only timer scrolls near the viewport edge and stops on completion/cancel. Level meters retain the existing shared visible-only animation/sampling cadence.

The native erase test checks the actual theme color rather than white. Mode tests verify that the main window is painted before the pin disappears and is not left cloaked. Still images and UI interaction checks do not prove there is no transient frame on every Windows/display configuration. Frame timing under other GPUs, mixed-DPI monitors, remote sessions and display drivers remains a practical manual check.

No test proves universal absence of bugs or leaks. Resource checks cover a bounded synthetic workload. Physical USB/Bluetooth reconnects, sleep/resume, long-duration soak tests, signed downloads and a real GitHub Actions run remain separate release validation work.

Implementation references: [Microsoft DWM attributes](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute), [DwmFlush](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush), [WM_SETREDRAW](https://learn.microsoft.com/en-us/windows/win32/gdi/wm-setredraw).
