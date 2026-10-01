param([switch]$NoStart, [switch]$NoAutostart)
$ErrorActionPreference = 'Stop'
$pluginRoot = Split-Path -Parent $PSScriptRoot
$stateRoot = Join-Path $env:USERPROFILE 'ReiCast'
$appRoot = Join-Path $stateRoot 'app'
$exe = Join-Path $appRoot 'ReiCast.exe'
$legacyRoot = Join-Path $env:LOCALAPPDATA 'ReiCast'
$legacyExe = Join-Path $legacyRoot 'app\ReiCast.exe'
if (-not (Test-Path -LiteralPath (Join-Path $pluginRoot 'ReiCast.exe'))) { & (Join-Path $PSScriptRoot 'build.ps1') }
# Only close this application's own installed process before upgrading it.
Get-Process -Name ReiCast -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe -or $_.Path -eq $legacyExe } | ForEach-Object { $ownProcess = $_; Stop-Process -Id $ownProcess.Id; if (-not $ownProcess.WaitForExit(5000)) { throw 'Rei Cast did not exit before the update.' } }
New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
# Preserve the old settings/cache; leave the legacy directory for recovery.
foreach ($name in @('config.json','weather-cache.json','external-task.json')) {
    $oldFile = Join-Path $legacyRoot $name
    $newFile = Join-Path $stateRoot $name
    if ((Test-Path -LiteralPath $oldFile) -and -not (Test-Path -LiteralPath $newFile)) { Copy-Item -LiteralPath $oldFile -Destination $newFile }
}
New-Item -ItemType Directory -Path (Join-Path $appRoot 'assets') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $appRoot 'scripts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $pluginRoot 'ReiCast.exe') -Destination $exe -Force
Copy-Item -LiteralPath (Join-Path $pluginRoot 'assets/companion.png') -Destination (Join-Path $appRoot 'assets/companion.png') -Force
# Keep an existing private custom portrait when upgrading from the personal build.
$configFile = Join-Path $stateRoot 'config.json'
$privatePortrait = Join-Path $appRoot 'assets\rei.png'
if ((Test-Path -LiteralPath $configFile) -and (Test-Path -LiteralPath $privatePortrait)) {
    $oldConfig = Get-Content -LiteralPath $configFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$oldConfig.avatarPath)) {
        $oldConfig | Add-Member -NotePropertyName avatarPath -NotePropertyValue $privatePortrait -Force
        $configTemporary = $configFile + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
        [IO.File]::WriteAllText($configTemporary,($oldConfig | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
        [IO.File]::Replace($configTemporary,$configFile,[System.Management.Automation.Language.NullString]::Value)
    }
}
Copy-Item -LiteralPath (Join-Path $pluginRoot 'scripts/startup.ps1') -Destination (Join-Path $appRoot 'scripts/startup.ps1') -Force
Copy-Item -LiteralPath (Join-Path $pluginRoot 'scripts/launch.ps1') -Destination (Join-Path $appRoot 'scripts/launch.ps1') -Force
Copy-Item -LiteralPath (Join-Path $pluginRoot 'scripts/publish-dot.ps1') -Destination (Join-Path $appRoot 'scripts/publish-dot.ps1') -Force
$startupOption = if ($NoAutostart) { '--disable-startup' } else { '--enable-startup' }
$setup = Start-Process -FilePath $exe -ArgumentList $startupOption -WindowStyle Hidden -Wait -PassThru
if ($setup.ExitCode -ne 0) { throw "自启动配置失败（退出码 $($setup.ExitCode)），请查看 $stateRoot\diagnostic.log。" }
if (-not $NoStart) { Start-Process -FilePath $exe -ArgumentList '--startup' -WindowStyle Hidden }
Write-Output "已安装到 $appRoot。配置和天气缓存在其上级目录保留。"
