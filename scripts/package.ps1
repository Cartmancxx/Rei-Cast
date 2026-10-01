param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'dist' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$stage = Join-Path $OutputDirectory ('stage-'+[Guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $stage 'rei-cast'
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
foreach ($name in @('ReiCast.exe','assets','scripts','skills','src','.codex-plugin','README.md','README.zh-CN.md','LICENSE','ASSET.md','EXTENSIONS.md','COMPATIBILITY.md','UPSTREAM.md','VALIDATION.md','CONTRIBUTING.md','RELEASE_NOTES.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $packageRoot -Recurse -Force
}
$zip = Join-Path $OutputDirectory 'rei-cast-windows-x64.zip'
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zip -Force
$hash = Get-FileHash -LiteralPath $zip -Algorithm SHA256
($hash.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ASCII
# The temporary stage is our own newly created directory within the output directory.
$resolvedStage = [IO.Path]::GetFullPath($stage)
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')+'\'
if (-not $resolvedStage.StartsWith($resolvedOutput,[StringComparison]::OrdinalIgnoreCase)) { throw 'Package stage path validation failed.' }
Remove-Item -LiteralPath $resolvedStage -Recurse -Force
Get-Item -LiteralPath $zip | Select-Object FullName,Length
