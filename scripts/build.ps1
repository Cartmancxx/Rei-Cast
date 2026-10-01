$ErrorActionPreference = 'Stop'
$pluginRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '需要 Windows 自带的 .NET Framework 4.x 编译器。' }
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $pluginRoot 'src') -Filter '*.cs' | ForEach-Object FullName
$target = Join-Path $pluginRoot 'ReiCast.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/out:$target" @sourceFiles
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Get-Item -LiteralPath $target | Select-Object FullName, Length
