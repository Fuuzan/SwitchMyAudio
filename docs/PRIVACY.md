# Privacy and publication

The release source is a separate, deliberately assembled project. Earlier workspace screenshots, archives, crash/memory dumps, machine-specific audit reports, test outputs and local preferences are excluded.

## Runtime data

Windows audio device names, endpoint IDs and hardware metadata are read locally to discover and select sources. Optional meters read peak values, without opening recording or loopback streams. Saved names, IDs, aliases, key bindings, options and window bounds stay in the current user's local application-data folder. No Windows account name, personal endpoint ID, computer name or local absolute path is hardcoded into the application. The public GitHub handle **Fuuzan** is intentionally credited in the MIT notice, README and assembly copyright metadata, at the author's request.

There are no network clients, telemetry, automatic uploaders or embedded credentials. Windows account identity is obtained at runtime only to scope the single-instance mutex to the current user; it is not logged or transmitted. The installer registers its current user's installation path and shortcut with Windows. This is local installation state, not bundled personal information.

Public USB vendor/product recognition rules and Windows COM/property GUIDs are shared technical identifiers. They are not a particular person's endpoint IDs. Synthetic tests use example names and fake identifiers.

## Earlier QuietSwitch copies

Compatibility with the earlier app name is retained. If Switch My Audio has no settings file, first launch can copy readable preferences from the same user's local QuietSwitch folder. Existing Switch My Audio settings take priority, and the original file is kept. Startup registration removes the old QuietSwitch entry when applying the new entry. Setup asks you to quit an earlier running copy so two apps do not compete for shortcuts or audio switching. None of these checks contacts a server or bundles old user preferences.

## Publication checks

`tools/Test-Privacy.ps1` scans publishable text for personal absolute paths, email addresses, the current account name, common API-token/private-key patterns and accidentally bundled settings/key/dump files. `Release.ps1` uses explicit source and binary file lists, not the surrounding workspace. No debug symbols are shipped. PNG/ICO metadata and executable strings were also inspected for the current release.

This check reduces accidental disclosure; pattern matching cannot prove that arbitrary future edits contain no personal details. Review future screenshots, release notes and Git commit author details before publishing. This project has no Git history yet, so no personal commit metadata is included in the source ZIP.

Do not attach your settings XML or raw diagnostic output to a public issue without inspecting it. Device names can contain aliases chosen by the user.
