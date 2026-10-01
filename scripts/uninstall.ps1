$ErrorActionPreference = 'Stop'
$appRoot = Join-Path $env:USERPROFILE 'ReiCast\app'
$exe = Join-Path $appRoot 'ReiCast.exe'
Remove-ItemProperty -LiteralPath 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run' -Name ReiCast -ErrorAction SilentlyContinue
$startupScript = Join-Path $appRoot 'scripts\startup.ps1'
if (Test-Path -LiteralPath $startupScript) { & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $startupScript -Action Disable }
Get-Process -Name ReiCast -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | ForEach-Object { Stop-Process -Id $_.Id }
$resolved = [System.IO.Path]::GetFullPath($appRoot)
$expected = [System.IO.Path]::GetFullPath((Join-Path $env:USERPROFILE 'ReiCast\app'))
if ($resolved -ne $expected -or -not $resolved.EndsWith('\ReiCast\app', [System.StringComparison]::OrdinalIgnoreCase)) { throw '安装目录校验失败。' }
if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
Write-Output '已关闭程序并取消两处自启动。个人配置保留在用户文件夹 ReiCast 中。'
