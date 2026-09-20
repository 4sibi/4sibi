param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $Dotnet publish (Join-Path $PSScriptRoot 'src/4sibi/4sibi.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false --source 'https://api.nuget.org/v3/index.json' -o (Join-Path $PSScriptRoot 'artifacts/win-x64')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
