param(
    [switch]$SkipBuild,
    [string]$PublishDirectory,
    [ValidateSet('x64','arm64','x86')][string]$Architecture = 'x64',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$productRoot = Split-Path -Parent $PSScriptRoot
if (-not $PublishDirectory) { $PublishDirectory = Join-Path $productRoot "artifacts/publish-portable-$Architecture" }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $productRoot 'artifacts' }
$publishRoot = [IO.Path]::GetFullPath($PublishDirectory)
if (-not $SkipBuild) {
    & dotnet publish (Join-Path $productRoot 'src/KeepClip.App/KeepClip.App.csproj') -c Release "-p:Platform=$Architecture" -o $publishRoot --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Publish non riuscito.' }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PortablePackage.ps1') -Destination (Join-Path $publishRoot 'KeepClip.Package.ps1')
foreach ($name in @('Diagnose-Portable.ps1','Avvia-con-diagnostica.cmd')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $publishRoot $name) }
. (Join-Path $PSScriptRoot 'PortablePackage.ps1')
if (Test-Path -LiteralPath (Join-Path $publishRoot 'AppxManifest.xml')) { throw 'Usa un publish pulito: la cartella contiene gia un manifest MSIX.' }
Write-KeepClipPackageManifest -Directory $publishRoot -Architecture $Architecture
$problems = @(Test-KeepClipPackage -Directory $publishRoot)
if ($problems.Count -gt 0) { throw ($problems -join [Environment]::NewLine) }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$archive = Join-Path $OutputDirectory "KeepClip-portable-$Architecture.zip"
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $archive -Force
Write-Output ('ZIP portable: ' + $archive)
