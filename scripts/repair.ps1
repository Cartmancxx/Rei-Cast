$ErrorActionPreference = 'Stop'
$exe = Join-Path $env:USERPROFILE 'ReiCast\app\ReiCast.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw '请先安装 Rei Cast。' }
if (Get-Process -Name ReiCast -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) {
    Start-Process -FilePath $exe -ArgumentList '--recover' -WindowStyle Hidden -Wait
    Write-Output '已请求重新建立助手窗口，最多重试 30 秒。'
} else {
    Start-Process -FilePath $exe -ArgumentList '--startup' -WindowStyle Hidden
    Write-Output '已启动助手。'
}
Write-Output '如果小屏仍然黑屏，请在 DeepCreative 中关闭 D-CAST，等待 3 秒后重新开启。此脚本只修复助手窗口，不重启厂商服务。'
