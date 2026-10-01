param([string]$ReportPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $ReportPath) { $ReportPath = Join-Path $projectRoot 'artifacts\test-report.json' }
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
New-Item -ItemType Directory -Path (Split-Path -Parent $ReportPath) -Force | Out-Null
$exe = Join-Path $projectRoot 'ReiCast.exe'
if (-not (Test-Path -LiteralPath $exe)) { & (Join-Path $PSScriptRoot 'build.ps1') }
$test = Start-Process -FilePath $exe -ArgumentList @('--self-test',('"'+$ReportPath+'"')) -WindowStyle Hidden -Wait -PassThru
$report = Get-Content -LiteralPath $ReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($test.ExitCode -ne 0 -or -not $report.success) { throw 'Rei Cast self-tests failed. Inspect the report.' }
Write-Output ("Passed {0} checks." -f $report.tests.Count)
