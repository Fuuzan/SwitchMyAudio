param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'), [ValidateSet('anycpu','x86','x64')][string]$Platform = 'anycpu')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler /nologo /target:winexe "/platform:$Platform" /optimize+ "/out:$OutputDirectory\SwitchMyAudio.exe" "/win32icon:$PSScriptRoot\assets\app.ico" "/win32manifest:$PSScriptRoot\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app.config') -Destination (Join-Path $OutputDirectory 'SwitchMyAudio.exe.config') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $OutputDirectory -Force
Write-Host "Built $OutputDirectory\SwitchMyAudio.exe"
