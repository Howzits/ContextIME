# Read-only, interactive focus probe; never types or changes an IME.
param([string]$Label = 'manual-check')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$helper = Join-Path $projectRoot 'bin\InputHelper.exe'
if (!(Test-Path -LiteralPath $helper)) { throw 'Run build.ps1 first.' }
Write-Host 'Click the target chat input / terminal / editor within 5 seconds.'
Start-Sleep -Seconds 5
$result = & $helper --probe
if ($LASTEXITCODE -ne 0) { throw 'Focus probe failed' }
$record = [PSCustomObject]@{ Timestamp = (Get-Date -Format o); Label = $Label; Probe = ($result -join ' '); TypedResult = 'Not tested (read-only probe)' }
$destination = Join-Path $projectRoot '.validation'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$record | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $destination 'compatibility.jsonl') -Encoding UTF8
$record | Format-List
