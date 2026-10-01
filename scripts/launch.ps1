param([ValidateSet('Run','ScheduledTask','Manual')][string]$Source='Manual')
$ErrorActionPreference = 'Stop'
$appRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$exe = Join-Path $appRoot 'ReiCast.exe'
$stateRoot = Split-Path -Parent $appRoot
$started = [DateTime]::UtcNow
function Save-LaunchResult($result) {
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stateRoot 'startup-launch.json') -Encoding UTF8
}
try {
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing application: $exe" }
    $existing = Get-Process -Name ReiCast -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Select-Object -First 1
    if ($existing) {
        Save-LaunchResult @{source=$Source; timestamp=$started.ToString('o'); success=$true; alreadyRunning=$true; processId=$existing.Id}
        exit 0
    }
    $child = Start-Process -FilePath $exe -ArgumentList '--startup' -WorkingDirectory $appRoot -WindowStyle Hidden -PassThru
    # Check a fresh heartbeat once at login; no background supervisor remains.
    for ($attempt=0; $attempt -lt 15; $attempt++) {
        Start-Sleep -Seconds 1
        $running = Get-Process -Name ReiCast -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Select-Object -First 1
        $heartbeat = $null
        try { $heartbeat = Get-Content -LiteralPath (Join-Path $stateRoot 'runtime.json') -Raw -Encoding UTF8 | ConvertFrom-Json } catch {}
        if ($running -and $heartbeat -and $heartbeat.processId -eq $running.Id -and ([DateTimeOffset]$heartbeat.timestamp).UtcDateTime -ge $started) {
            Save-LaunchResult @{source=$Source; timestamp=[DateTime]::UtcNow.ToString('o'); success=$true; alreadyRunning=$false; processId=$running.Id; version=$heartbeat.version; visible=$heartbeat.visible; display=$heartbeat.display}
            exit 0
        }
    }
    throw 'No fresh Rei Cast heartbeat was received within 15 seconds.'
} catch {
    Save-LaunchResult @{source=$Source; timestamp=[DateTime]::UtcNow.ToString('o'); success=$false; error=$_.Exception.Message}
    exit 1
}
