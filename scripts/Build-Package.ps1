param([switch]$SkipBuild, [string]$CertificateThumbprint, [string]$PublishDirectory)
$ErrorActionPreference = 'Stop'
$productRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $productRoot 'artifacts'
$publishRoot = Join-Path $artifactRoot 'publish-x64'
if ($PublishDirectory) { $publishRoot = [System.IO.Path]::GetFullPath($PublishDirectory) }
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
if (-not $SkipBuild) {
    & dotnet publish (Join-Path $productRoot 'src/KeepClip.App/KeepClip.App.csproj') -c Release -p:Platform=x64 -o $publishRoot --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Publish non riuscito.' }
}
if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'KeepClip.exe'))) { throw 'Eseguibile pubblicato non disponibile.' }
Copy-Item -LiteralPath (Join-Path $productRoot 'packaging/AppxManifest.xml') -Destination (Join-Path $publishRoot 'AppxManifest.xml')
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
$packagePath = Join-Path $artifactRoot 'KeepClip-0.1.0-x64.msix'
& (Join-Path $sdk.FullName 'x64/makeappx.exe') pack /d $publishRoot /p $packagePath /o
if ($LASTEXITCODE -ne 0) { throw 'Creazione MSIX non riuscita.' }
if ($CertificateThumbprint) {
    # Sign with an existing certificate in CurrentUser/My; no private key or password is stored here.
    & (Join-Path $sdk.FullName 'x64/signtool.exe') sign /fd SHA256 /sha1 $CertificateThumbprint $packagePath
    if ($LASTEXITCODE -ne 0) { throw 'Firma non riuscita. Il subject deve essere CN=ClipShelf.' }
}
Write-Output "Pacchetto: $packagePath"
if (-not $CertificateThumbprint) { Write-Output 'Pacchetto non firmato: occorre firmarlo prima di installarlo.' }
