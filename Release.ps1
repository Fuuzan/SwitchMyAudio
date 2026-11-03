param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'release'), [switch]$SkipBuild, [switch]$RequireSignedApp)
$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
& (Join-Path $PSScriptRoot 'tools\Test-Privacy.ps1')
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') }
$appFile = Join-Path $PSScriptRoot 'dist\SwitchMyAudio.exe'
if ($RequireSignedApp -and (Get-AuthenticodeSignature -LiteralPath $appFile).Status -ne 'Valid') {
    throw 'A valid signed application is required. No unsigned fallback is allowed.'
}
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $PSScriptRoot 'dist\SwitchMyAudio.exe')).FileVersion
$version = ([Version]$version).ToString(3)
$label = if (([Version]$version).Major -eq 0) { "$version beta" } else { $version }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$setupFile = Join-Path $OutputDirectory ('SwitchMyAudio-' + $version + '-Setup.exe')
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /define:INSTALLER "/out:$setupFile" "/win32icon:$PSScriptRoot\assets\app.ico" "/win32manifest:$PSScriptRoot\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "/resource:$PSScriptRoot\dist\SwitchMyAudio.exe,payload.SwitchMyAudio.exe" "/resource:$PSScriptRoot\dist\SwitchMyAudio.exe.config,payload.SwitchMyAudio.exe.config" "/resource:$PSScriptRoot\LICENSE,payload.LICENSE" (Join-Path $PSScriptRoot 'installer\Setup.cs') (Join-Path $PSScriptRoot 'src\BuildInfo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$staging = Join-Path $OutputDirectory ('staging-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    $portable = Join-Path $staging 'portable'
    $source = Join-Path $staging 'SwitchMyAudio'
    New-Item -ItemType Directory -Path $portable,$source | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dist\SwitchMyAudio.exe'),(Join-Path $PSScriptRoot 'dist\SwitchMyAudio.exe.config'),(Join-Path $PSScriptRoot 'LICENSE') -Destination $portable
    $portableGuide = @"
Switch My Audio $label

GET STARTED
Extract all files into a folder and open SwitchMyAudio.exe. If it is already
running, click its icon in the system tray near the clock.

If Windows shows "Windows protected your PC", click "More info > Run anyway".
Requires Windows 10/11 (Intel/AMD) and .NET Framework 4.8+ (ARM64 is untested).

HOW TO USE
1. Switch: Open Playback or Microphones and click "Use" beside a device.
2. Shortcuts: Click "Set shortcut" and press a key combo. To remove it, click
   the shortcut again and choose "Clear shortcut".
3. Cycle: Check "Cycle" on 2+ devices in a tab, then set a shortcut beside
   "Cycle selected sources" to toggle through them.
4. Rename: Hover over a device name and click "Rename" (Enter saves, Esc cancels).
5. Pin: Click "Pin" for a small always-on-top window. Click "Unpin" or press
   Esc to return.
6. Reorder & Hide: In Settings > Devices, drag the grip or use the arrows to
   reorder devices (also sets the Cycle order), or use the toggles to hide them.

SETTINGS
- Start with Windows: Launches quietly in the tray when you sign in.
- Appearance & Pinning: Choose a theme and which sources appear when pinned.
- Level Meters: Shows live sound activity (no audio is ever recorded).
- Auto-switch Headphones: Prefers newly connected headphones. Note: Windows
  may still switch on its own, and USB wireless receivers often stay connected
  even when the headset is turned off.

CLOSE, UPDATE & PRIVACY
- Closing the window hides the app to the tray so shortcuts stay active.
- Right-click the tray icon and choose "Quit" to exit (always Quit before
  replacing files with an update).
- Settings stay in %LOCALAPPDATA%\SwitchMyAudio. Offline, no accounts, and
  no telemetry.

Free and open source under the included MIT license. Created by Fuuzan.
"@
    [IO.File]::WriteAllText((Join-Path $portable 'README.txt'), $portableGuide + "`r`n")
    foreach ($name in @('src','assets','installer','tests','tools','docs','.github')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $source $name) -Recurse }
    foreach ($name in @('README.md','Build.ps1','Test.ps1','Release.ps1','SwitchMyAudio.csproj','app.config','app.manifest','.gitignore')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $source }
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'LICENSE')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $source }
    Compress-Archive -Path (Join-Path $portable '*') -DestinationPath (Join-Path $OutputDirectory "SwitchMyAudio-$version-portable.zip") -Force
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $sourceZip = Join-Path $OutputDirectory "SwitchMyAudio-$version-source.zip"
    if (Test-Path -LiteralPath $sourceZip) { Remove-Item -LiteralPath $sourceZip }
    [IO.Compression.ZipFile]::CreateFromDirectory($source,$sourceZip,[IO.Compression.CompressionLevel]::Optimal,$true)
    & (Join-Path $PSScriptRoot 'tools\Update-ReleaseChecksums.ps1') -ReleaseDirectory $OutputDirectory
    & (Join-Path $PSScriptRoot 'tools\Test-Release.ps1') -ReleaseDirectory $OutputDirectory
    Write-Host ('Packaged Switch My Audio ' + $version)
} finally {
    $resolved = [IO.Path]::GetFullPath($staging)
    if (-not $resolved.StartsWith($OutputDirectory.TrimEnd('\') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Staging cleanup path is outside release output.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
