# Switch My Audio 0.7.0 beta

Remove Map and Forget from disconnected devices. Keep their saved preferences for reconnection and reduce the rows' empty space. Reordering and visibility toggles remain available.

Publication preparation for the same 0.7.0 beta: clarify unsigned-download warnings, include demo screenshots, rename portable help to README.txt, document headphone receiver reports and retained QuietSwitch migration, and prepare optional SignPath signing. Current downloads remain unsigned pending public release and provider approval.

First-time users now start with Start with Windows on, System theme, level meters off and auto-switch headphones off. Existing saved choices are retained. Startup is initialized once on the first normal launch, not by demos or custom-config runs.

- Correct the Devices footer by removing the empty label that covered Refresh sources and the unnecessary divider. Disconnected devices use compact status rows without Map or Forget actions.
- Let native scrolling move existing rows without redundant layout during scrolling. Keep arrows, grip dragging and vertical scrolling.
- Show device names once, with the original name underneath only when an alias differs. Clarify that Windows may switch to headphones independently of the app's setting.
- Use one app and installer for 32-bit and 64-bit Intel/AMD Windows PCs. ARM and untested Windows combinations are documented explicitly.
- Stop meter animation when no connected sources are visible. Meter sampling remains limited to visible source rows.
- Bound settings XML parsing so an oversized damaged file cannot trigger unlimited document parsing; normal settings and backups are retained.
- Extend testing with architecture checks, empty/many-device states, Unicode names, longer mixed interaction runs and complete window/controller lifetime checks.
- Prepare GitHub build checks for two Windows runner versions and both process architectures, plus automatic draft prereleases for 0.x tags.
- Include the MIT license, Fuuzan credit, privacy guidance, compatibility matrix and bug-report form. The license ships with installed and portable copies.

## Earlier in 0.6.1

- Prepare Settings while hidden by the compositor, then reveal its painted theme to address the white opening flash.
- Keep the pinned-source dropdown's face and popup backgrounds themed throughout painting. The buffered face avoids native light hover redraws while preserving native keyboard and accessibility behavior.
- Restore up/down arrow buttons alongside drag-and-drop source ordering. Both methods retain rows and scroll position, and arrows update at group boundaries.
- Include native mouse-message, F4/Escape/arrow-key, dark/light repaint, modal opening and reorder-arrow regression checks.

## Earlier in 0.6.0

- Restore pinned/full windows through a prepared themed surface, with native window transitions disabled and no white background erase.
- Reuse Settings pages and source controls during navigation and refresh.
- Drag source grips to reorder devices; keep the same rows and scroll position. Keyboard reordering is also available.
- Remove horizontal scrollbars from full, pinned and Settings source lists while preserving vertical scrolling.
- Use the selected speaker-and-arrows icon and Switch My Audio name.
- Add an offline per-user installer, update rollback, portable/source downloads, privacy scan and GitHub draft-release workflows.

Saved preferences from QuietSwitch are copied on first launch when the new settings file does not exist. Originals are kept.

Downloads are unsigned. Wireless detection still depends on connection events and Windows hardware metadata. See the verification report for tested behavior and limits.
