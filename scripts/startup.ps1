param([ValidateSet('Enable','Disable','Status')][string]$Action='Enable')
$ErrorActionPreference = 'Stop'
$taskName = 'ReiCast Delayed Startup'
$appRoot = [IO.Path]::GetFullPath((Join-Path $env:USERPROFILE 'ReiCast\app'))
$exe = Join-Path $appRoot 'ReiCast.exe'
$launcher = Join-Path $appRoot 'scripts\launch.ps1'
$hostExe = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ($Action -eq 'Disable') {
    if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) { Unregister-ScheduledTask -TaskName $taskName -Confirm:$false }
    Remove-ItemProperty -LiteralPath $runKey -Name ReiCast -ErrorAction SilentlyContinue
    Write-Output 'disabled'
    exit 0
}
if ($Action -eq 'Status') {
    if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) { Write-Output 'enabled'; exit 0 }
    Write-Output 'disabled'; exit 0
}
if (-not (Test-Path -LiteralPath $exe)) { throw "Rei Cast was not found at $exe" }
if (-not (Test-Path -LiteralPath $launcher)) { throw "Startup launcher was not found at $launcher" }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$arguments = '-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $launcher + '" -Source ScheduledTask'
$taskAction = New-ScheduledTaskAction -Execute $hostExe -Argument $arguments
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $identity
$trigger.Delay = 'PT30S'
$principal = New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 1) -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $taskName -Action $taskAction -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
$runCommand = '"' + $hostExe + '" ' + $arguments.Replace('-Source ScheduledTask','-Source Run')
New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -LiteralPath $runKey -Name ReiCast -Value $runCommand
Write-Output "enabled: $taskName (30-second delay, current user, standard permissions)"
