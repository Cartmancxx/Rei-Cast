$root = Join-Path $env:USERPROFILE 'ReiCast'
Get-Content -LiteralPath (Join-Path $root 'runtime.json') -Raw -Encoding UTF8
Get-Process -Name ReiCast -ErrorAction SilentlyContinue | Select-Object Id, CPU, WorkingSet64, PrivateMemorySize64
