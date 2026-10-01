param([Parameter(Mandatory=$true)][string]$InputFile)
$ErrorActionPreference = 'Stop'
if ((Get-Item -LiteralPath $InputFile).Length -gt 16384) { throw 'Dialogue input exceeds 16 KB.' }
$inputData = Get-Content -LiteralPath $InputFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace([string]$inputData.reply)) { throw 'A visible reply is required.' }
if (([string]$inputData.reply).Length -gt 4000 -or ([string]$inputData.userMessage).Length -gt 600) { throw 'Maximum lengths: reply 4000, userMessage 600 characters.' }
$ttl = 300
if ($null -ne $inputData.ttlSeconds) { $ttl = [Math]::Max(10,[Math]::Min(1800,[int]$inputData.ttlSeconds)) }
$source = if ($inputData.source -eq 'demo') { 'demo' } else { 'dot' }
$snapshot = @{id=[Guid]::NewGuid().ToString('N');source=$source;userMessage=[string]$inputData.userMessage;reply=[string]$inputData.reply;updatedUtc=[DateTime]::UtcNow.ToString('o');ttlSeconds=$ttl}
$root = Join-Path $env:USERPROFILE 'ReiCast'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$target = Join-Path $root 'dot-dialogue.json'
$temporary = Join-Path $root ('dot-dialogue.'+[Guid]::NewGuid().ToString('N')+'.tmp')
$json = $snapshot | ConvertTo-Json -Depth 3
if ([Text.Encoding]::UTF8.GetByteCount($json) -gt 16384) { throw 'Encoded dialogue exceeds 16 KB.' }
try {
    [IO.File]::WriteAllText($temporary,$json,[Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $target) { [IO.File]::Replace($temporary,$target,[System.Management.Automation.Language.NullString]::Value) } else { [IO.File]::Move($temporary,$target) }
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
Write-Output 'Published dialogue locally. Rei Cast will show it at the next status check (default: 2 seconds).'
