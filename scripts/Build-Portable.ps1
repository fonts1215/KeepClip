param([switch]$SkipBuild, [string]$PublishDirectory)
$ErrorActionPreference = 'Stop'
$productRoot = Split-Path -Parent $PSScriptRoot
if (-not $PublishDirectory) { $PublishDirectory = Join-Path $productRoot 'artifacts/publish-portable-x64' }
$publishRoot = [IO.Path]::GetFullPath($PublishDirectory)
if (-not $SkipBuild) {
    & dotnet publish (Join-Path $productRoot 'src/KeepClip.App/KeepClip.App.csproj') -c Release -p:Platform=x64 -o $publishRoot --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Publish non riuscito.' }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PortablePackage.ps1') -Destination (Join-Path $publishRoot 'KeepClip.Package.ps1')
foreach ($name in @('Diagnose-Portable.ps1','Avvia-con-diagnostica.cmd')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $publishRoot $name) }
. (Join-Path $PSScriptRoot 'PortablePackage.ps1')
Write-KeepClipPackageManifest -Directory $publishRoot
$problems = @(Test-KeepClipPackage -Directory $publishRoot)
if ($problems.Count -gt 0) { throw ($problems -join [Environment]::NewLine) }
$archive = Join-Path $productRoot 'artifacts/KeepClip-portable-x64.zip'
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $archive -Force
Write-Output ('ZIP portable: ' + $archive)
