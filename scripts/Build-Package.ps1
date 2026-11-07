param(
    [switch]$SkipBuild,
    [string]$CertificateThumbprint,
    [string]$PublishDirectory,
    [ValidateSet('x64','arm64','x86')][string]$Architecture = 'x64',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$productRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $productRoot 'artifacts' }
$publishRoot = Join-Path $productRoot "artifacts/publish-$Architecture"
if ($PublishDirectory) { $publishRoot = [System.IO.Path]::GetFullPath($PublishDirectory) }
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
if (-not $SkipBuild) {
    & dotnet publish (Join-Path $productRoot 'src/KeepClip.App/KeepClip.App.csproj') -c Release "-p:Platform=$Architecture" -o $publishRoot --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Publish non riuscito.' }
}
if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'KeepClip.exe'))) { throw 'Eseguibile pubblicato non disponibile.' }
. (Join-Path $PSScriptRoot 'PortablePackage.ps1')
$actual = Get-KeepClipExecutableArchitecture -Path (Join-Path $publishRoot 'KeepClip.exe')
if ($actual -ne $Architecture) { throw "Architettura eseguibile $actual diversa da quella richiesta: $Architecture." }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $productRoot 'packaging/AppxManifest.xml') -Raw
$manifest.Package.Identity.ProcessorArchitecture = $Architecture
$manifest.Save((Join-Path $publishRoot 'AppxManifest.xml'))
$assetRoot = Join-Path $publishRoot 'Assets'
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null
foreach ($asset in @('StoreLogo.png', 'Square44x44Logo.png', 'Square150x150Logo.png', 'KeepClip.ico')) {
    if (-not (Test-Path -LiteralPath (Join-Path $assetRoot $asset))) {
        throw "Asset mancante nel publish: $asset. Genera gli asset con scripts/Generate-AppIcons.ps1 e ripeti il publish."
    }
}
$sdkBinRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
$sdk = Get-ChildItem -LiteralPath $sdkBinRoot -Directory | Where-Object { $_.Name -match '^10\.0\.\d+\.0$' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'x64/makeappx.exe')) } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $sdk) { throw 'MakeAppx non trovato. Installa Windows SDK tramite Visual Studio.' }
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$packagePath = Join-Path $artifactRoot "KeepClip-$($manifest.Package.Identity.Version)-$Architecture.msix"
& (Join-Path $sdk.FullName 'x64/makeappx.exe') pack /d $publishRoot /p $packagePath /o
if ($LASTEXITCODE -ne 0) { throw 'Creazione MSIX non riuscita.' }
if ($CertificateThumbprint) {
    # Sign with an existing certificate in CurrentUser/My; no private key or password is stored here.
    & (Join-Path $sdk.FullName 'x64/signtool.exe') sign /fd SHA256 /sha1 $CertificateThumbprint $packagePath
    if ($LASTEXITCODE -ne 0) { throw 'Firma non riuscita. Il subject deve essere CN=ClipShelf.' }
}
Write-Output "Pacchetto: $packagePath"
if (-not $CertificateThumbprint) { Write-Output 'Pacchetto non firmato: occorre firmarlo prima di installarlo.' }
