param([string]$ReleaseDirectory = (Join-Path (Split-Path $PSScriptRoot) 'release'))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$version = ([Version][Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root 'dist\SwitchMyAudio.exe')).FileVersion).ToString(3)
$names = @("SwitchMyAudio-$version-Setup.exe", "SwitchMyAudio-$version-portable.zip", "SwitchMyAudio-$version-source.zip")
$checksums = foreach ($name in $names) {
    $hash = Get-FileHash -LiteralPath (Join-Path $ReleaseDirectory $name) -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + $name
}
$checksums | Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'SHA256SUMS.txt') -Encoding ascii
