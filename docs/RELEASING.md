# Releasing an update

## First publication

1. Upload **this project's contents** as the root of your GitHub repository. Do not upload the surrounding workspace or its `work`/`outputs` folders.
2. The MIT license and Fuuzan credit are already included. Check the README and release notes before publishing.
3. Let the **Build and test** workflow pass. It produces downloadable release files as an Actions artifact.
4. Push the version tag `v0.7.0`, matching `src/BuildInfo.cs`. The release workflow tests both process architectures, packages and creates a draft GitHub prerelease.
5. Download and try its installer, review the notes, then click **Publish release** in GitHub.

The workflow uses the repository's built-in token. Do not put a personal access token in source. It requests write permission only in the release job. No repository URL, Windows account name or personal path is hardcoded in the app. Fuuzan is the intentionally public author credit.

## Every later version

1. Change the version constant in `src/BuildInfo.cs`, for example `0.7.1.0`.
2. Update `docs/RELEASE-NOTES.md` and the README's displayed version, then commit your changes.
3. Push the corresponding tag `v0.7.1`.
4. Review the automatically prepared draft and publish it.

Users run the new setup file over their existing installation. It closes the installed app, stages the app, runtime config and license, replaces them and preserves preferences. A failed payload replacement restores the earlier files. If replacement rollback itself fails because files are locked or the disk is unavailable, recovery copies remain beside the app. Shortcut/Windows registration errors are reported and can be retried with the same installer.

The app does not silently check for or download updates. Its GitHub Releases page is the download location. Portable users quit the tray app and replace the extracted files. If a portable copy is still running when Setup opens, quit it and retry.

Optional [SignPath signing](CODE-SIGNING.md) is prepared in the tag workflow and remains disabled until the public project is accepted and its repository variables/secret are configured. It signs the app first, embeds that signed app in Setup, then signs Setup and refreshes checksums. Do not claim signed downloads before both signatures have been verified.

## Local packaging

```powershell
./Build.ps1
./Test.ps1
./Test.ps1 -Platform x86 -Artifacts artifacts-x86 -Stress
./Test.ps1 -Stress -StressRounds 1000
./Release.ps1
```

`release/` contains a standalone Setup EXE, a portable ZIP, a clean source ZIP and SHA-256 checksums. The installer is offline and per user. Windows' .NET Framework is the only runtime dependency. No paid installer tooling is required. Code signing is not configured; downloads currently have an unknown publisher.

The portable help file is `README.txt`; the ZIP retains its existing flat layout. `Release.ps1 -SkipBuild -RequireSignedApp` is reserved for packaging an already signed app during the optional signing pipeline. Normal local packaging uses `Release.ps1`, which builds first.

The build workflow checks AnyCPU and x86 on Windows Server 2022 and 2025 runners. These runners provide regression coverage, not proof of every Windows desktop/audio-driver combination. The Actions workflow has been prepared locally, but cannot be considered remotely verified until it runs in the actual GitHub repository. The release workflow requires the license and matching tag before preparing its draft. Version 0.x tags are automatically marked as prereleases; 1.x tags prepare ordinary draft releases.

## First-upload checklist

- Extract the clean source ZIP and upload the contents of its `SwitchMyAudio` folder, including the `.github` folder. The top level should contain `README.md`, `LICENSE`, `src`, `tests` and the build scripts.
- Put the Setup EXE, portable ZIP and checksum text on the **Release**, rather than among the source files. The release workflow can do this for you after a matching tag is pushed.
- Set the release title to **Switch My Audio v0.7.0 beta** and mark it as a prerelease if uploading manually. The prepared workflow sets the prerelease flag automatically.
- Keep local `work`, test artifacts, saved settings, older builds and screenshots containing personal information out of the repository. The source ZIP already excludes them.

Workflow references: [GitHub workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax), [GitHub CLI release creation](https://cli.github.com/manual/gh_release_create).
