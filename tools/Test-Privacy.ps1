param([string]$Root = (Split-Path $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)
$excluded = '[\\/](dist|release|artifacts(?:-[^\\/]+)?|bin|obj|\.git|\.vs)[\\/]'
$files = @(Get-ChildItem -LiteralPath $Root -File -Recurse -Force | Where-Object { $_.FullName -notmatch $excluded })
$patterns = @(
    '(?i)[A-Z]:[\\/]Users[\\/][^\\/\s"''<>]+',
    '(?i)/(?:home|Users)/[A-Za-z0-9_.-]+/',
    '(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b',
    'gh[pousr]_[A-Za-z0-9]{20,}',
    'github_pat_[A-Za-z0-9_]{20,}',
    'sk-(?:proj-)?[A-Za-z0-9_-]{30,}',
    '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
)
if ($env:USERNAME -and $env:USERNAME.Length -ge 4) { $patterns += '(?i)\b' + [Regex]::Escape($env:USERNAME) + '\b' }
$problems = @()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($Root.Length).TrimStart('\','/')
    if ($file.Name -match '(?i)^(settings\.xml|\.env(?:\..*)?)$' -or $file.Extension -match '(?i)^\.(pfx|p12|pem|key|dmp|log|bak|pdb)$') { $problems += $relative; continue }
    if ($file.Extension -match '(?i)^\.(png|ico|exe|zip)$') { continue }
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($pattern in $patterns) { if ($text -match $pattern) { $problems += $relative; break } }
}
if ($problems.Count) { throw ('Publication privacy scan needs review in: ' + (($problems | Select-Object -Unique) -join ', ')) }
Write-Host ('Privacy scan passed for ' + $files.Count + ' source/asset files. No matched personal paths, email addresses, current account name, common tokens, private keys or settings files.')
