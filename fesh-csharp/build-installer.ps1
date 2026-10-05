# Builds the Windows release of Fesh.
#   dist\Fesh-Setup.msi  installer (Start menu + desktop shortcuts, uninstall from Settings > Apps)
#   dist\Fesh.exe        portable copy you can run without installing
# Run from PowerShell:  .\build-installer.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$version = ([xml](Get-Content Fesh.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

dotnet publish Fesh.csproj -c Release -r win-x64 --self-contained true -o publish `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }

New-Item -ItemType Directory -Force dist | Out-Null
dotnet tool run wix build installer\Fesh.wxs -arch x64 -d "PublishDir=$PSScriptRoot\publish" -d "Version=$version" -o dist\Fesh-Setup.msi
if ($LASTEXITCODE -ne 0) { throw 'wix build failed' }

Copy-Item publish\Fesh.exe dist\Fesh.exe -Force
Remove-Item dist\*.wixpdb -ErrorAction SilentlyContinue
Write-Host "Done: $PSScriptRoot\dist\Fesh-Setup.msi (version $version)"
