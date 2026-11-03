param([string]$Artifacts = (Join-Path $PSScriptRoot 'artifacts'), [switch]$Native, [switch]$SwitchDevices, [switch]$Stress, [switch]$UiOnly,
    [ValidateSet('anycpu','x86','x64')][string]$Platform = 'anycpu', [ValidateRange(100,5000)][int]$StressRounds = 100)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $Artifacts -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$testBinary = Join-Path $Artifacts 'SwitchMyAudio.Tests.exe'
$testSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
$testSources += Join-Path $PSScriptRoot 'installer\Setup.cs'
& $compiler /nologo /target:exe "/platform:$Platform" /optimize+ /main:TestRunner "/out:$testBinary" "/win32manifest:$PSScriptRoot\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll @sources @testSources
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app.config') -Destination ($testBinary + '.config') -Force
$testArgs = @($Artifacts)
if ($Native) { $testArgs += '--native' }
if ($SwitchDevices) { $testArgs += '--switch' }
if ($Stress) { $testArgs += @('--stress',"--rounds=$StressRounds") }
if ($UiOnly) { $testArgs += '--ui' }
& $testBinary @testArgs
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
