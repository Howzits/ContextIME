$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$bin = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
$exe = Join-Path $bin 'InputHelper.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'native') -Filter '*.cs' | ForEach-Object FullName)
& (Join-Path $framework 'csc.exe') /nologo /target:exe /optimize+ "/out:$exe" "/reference:$framework\WPF\UIAutomationClient.dll" "/reference:$framework\WPF\UIAutomationTypes.dll" @sources
if ($LASTEXITCODE -ne 0) { throw 'Helper compilation failed' }
& $exe --self-test
if ($LASTEXITCODE -ne 0) { throw 'Helper self-test failed' }
python (Join-Path $PSScriptRoot 'package.py')
if ($LASTEXITCODE -ne 0) { throw 'VSIX packaging failed' }
