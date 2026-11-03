param([string]$ReleaseDirectory = (Join-Path (Split-Path $PSScriptRoot) 'release'), [switch]$RequireSigned)
$ErrorActionPreference = 'Stop'
$ReleaseDirectory = [IO.Path]::GetFullPath($ReleaseDirectory)
$root = Split-Path $PSScriptRoot
$version = ([Version][Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root 'dist\SwitchMyAudio.exe')).FileVersion).ToString(3)
$expected = @("SwitchMyAudio-$version-Setup.exe", "SwitchMyAudio-$version-portable.zip", "SwitchMyAudio-$version-source.zip")
$lines = @(Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'SHA256SUMS.txt'))
if ($lines.Count -ne $expected.Count) { throw 'Expected three release checksums.' }
foreach ($line in $lines) {
    $parts = $line -split '  ',2
    if ($parts.Count -ne 2 -or $parts[1] -notin $expected) { throw 'Unexpected checksum entry.' }
    if ((Get-FileHash -LiteralPath (Join-Path $ReleaseDirectory $parts[1]) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $parts[0]) { throw ('Checksum mismatch: ' + $parts[1]) }
}
$setup = $null
foreach ($file in @((Join-Path $root 'dist\SwitchMyAudio.exe'),(Join-Path $ReleaseDirectory "SwitchMyAudio-$version-Setup.exe"))) {
    if ($RequireSigned -and (Get-AuthenticodeSignature -LiteralPath $file).Status -ne 'Valid') {
        throw ('A valid Authenticode signature is required: ' + [IO.Path]::GetFileName($file))
    }
    $bytes = [IO.File]::ReadAllBytes($file)
    foreach ($encoding in @([Text.Encoding]::ASCII,[Text.Encoding]::Unicode)) {
        $value = $encoding.GetString($bytes)
        if ($value -match '(?i)[A-Z]:[\\/]Users[\\/]|github_pat_|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----' -or
            ($env:USERNAME -and $env:USERNAME.Length -ge 4 -and $value -match [Regex]::Escape($env:USERNAME))) { throw 'Personal path, local account or credential marker found in release binary.' }
    }
    $assembly = [Reflection.Assembly]::Load($bytes)
    $peKind = [Reflection.PortableExecutableKinds]::NotAPortableExecutableImage
    $machine = [Reflection.ImageFileMachine]::I386
    $assembly.ManifestModule.GetPEKind([ref]$peKind,[ref]$machine)
    if ($machine -ne [Reflection.ImageFileMachine]::I386 -or $peKind -ne [Reflection.PortableExecutableKinds]::ILOnly) { throw 'Release executable is not architecture-neutral IL.' }
    if ($assembly.GetName().Version.ToString(3) -ne $version) { throw 'App and installer versions differ.' }
    $setup = $assembly
}
$sha = [Security.Cryptography.SHA256]::Create()
try {
    foreach ($name in @('SwitchMyAudio.exe','SwitchMyAudio.exe.config','LICENSE')) {
        $stream = $setup.GetManifestResourceStream('payload.' + $name)
        if ($null -eq $stream) { throw ('Missing installer payload: ' + $name) }
        try { $embedded = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally { $stream.Dispose() }
        $source = if ($name -eq 'LICENSE') { Join-Path $root $name } else { Join-Path $root ('dist\' + $name) }
        if ($embedded -ne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) { throw ('Installer payload does not match built file: ' + $name) }
    }
} finally { $sha.Dispose() }
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($kind in @('source','portable')) {
    $zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $ReleaseDirectory "SwitchMyAudio-$version-$kind.zip"))
    try {
        $names = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
        if ($kind -eq 'source') {
            if ($names -notcontains 'SwitchMyAudio/LICENSE' -or $names -notcontains 'SwitchMyAudio/.github/workflows/release.yml') { throw 'Source ZIP lacks license or GitHub workflow.' }
            if ($names | Where-Object { $_ -match '(?i)(^|/)(work|dist|release|artifacts[^/]*|bin|obj|\.git)(/|$)|settings\.xml|\.(exe|pdb|log|dmp|bak|zip)$' }) { throw 'Source ZIP contains a local/build artifact.' }
        } else {
            $wanted = @('SwitchMyAudio.exe','SwitchMyAudio.exe.config','README.txt','LICENSE')
            if ($names.Count -ne $wanted.Count -or ($names | Where-Object { $_ -notin $wanted })) { throw 'Unexpected portable ZIP contents.' }
            $sha = [Security.Cryptography.SHA256]::Create()
            try {
                foreach ($name in @('SwitchMyAudio.exe','SwitchMyAudio.exe.config','LICENSE')) {
                    $stream = $zip.GetEntry($name).Open()
                    try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally { $stream.Dispose() }
                    $source = if ($name -eq 'LICENSE') { Join-Path $root $name } else { Join-Path $root ('dist\' + $name) }
                    if ($actual -ne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) { throw ('Portable payload does not match built file: ' + $name) }
                }
            } finally { $sha.Dispose() }
        }
    } finally { $zip.Dispose() }
}
Write-Host ('Verified release ' + $version + ': checksums, architecture-neutral executables, embedded payloads, license and clean ZIP contents.')
