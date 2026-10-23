$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'DXVPrivateTweek.csproj'
dotnet publish $project -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o (Join-Path $PSScriptRoot 'publish')
Write-Host "Built: $PSScriptRoot\publish\DXVPrivateTweek.exe"
