param([string]$Version = '1.0.0')

$ErrorActionPreference = 'Stop'

$modRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$stage   = Join-Path ([System.IO.Path]::GetTempPath()) ('kkblocks-release-' + [guid]::NewGuid().ToString('N'))
$target  = Join-Path $stage 'KKBlocks'

New-Item -ItemType Directory -Path $target -Force | Out-Null
# Ship the mod payload only: no git metadata, no CKAN tooling, no archives.
& robocopy $modRoot $target /E /XD .git CKAN /XF *.zip *.log netkan.exe ckan.exe .gitignore /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }

$zip = Join-Path $PSScriptRoot "KKBlocks-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $target -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

Write-Host "Created $zip"
