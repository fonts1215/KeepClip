param(
    [ValidateSet('x64','arm64','x86')][string]$Architecture = 'x64',
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [string]$VersionFile,
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Versioning.ps1')
$productRoot = Split-Path -Parent $PSScriptRoot
$publishRoot = [IO.Path]::GetFullPath($PublishDirectory)
$version = if ($VersionFile) {
    Assert-KeepClipVersion -Version (Get-Content -LiteralPath $VersionFile -Raw | ConvertFrom-Json)
} else { & (Join-Path $PSScriptRoot 'Get-Version.ps1') }
$headCommit = & git -C $productRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $headCommit -ne $version.Sha) { throw 'La versione fornita appartiene a un commit diverso dal checkout.' }
$publishArguments = @(
    'publish', (Join-Path $productRoot 'src/KeepClip.App/KeepClip.App.csproj'),
    '-c', 'Release', "-p:Platform=$Architecture", '-o', $publishRoot,
    "-p:Version=$($version.SemVer)", "-p:AssemblyVersion=$($version.AssemblySemVer)",
    "-p:FileVersion=$($version.AssemblySemFileVer)", "-p:InformationalVersion=$($version.InformationalVersion)",
    '-p:IncludeSourceRevisionInInformationalVersion=false', '--verbosity', 'minimal'
)
if ($NoRestore) { $publishArguments += '--no-restore' }
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'Publish non riuscito.' }
$version | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $publishRoot 'KeepClip.version.json') -Encoding utf8
Get-KeepClipPublishedVersion -Directory $publishRoot | Out-Null
Write-Output "Publish $Architecture : $($version.SemVer), file/MSIX $($version.AssemblySemFileVer), commit $($version.Sha)"
