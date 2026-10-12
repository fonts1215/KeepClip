# Shared by the packaging script and the portable diagnostic launcher.
function Write-KeepClipPackageManifest {
    param([Parameter(Mandatory=$true)][string]$Directory)
    $bundleRoot = (Resolve-Path -LiteralPath $Directory).Path.TrimEnd('\','/')
    $entries = @(Get-ChildItem -LiteralPath $bundleRoot -File -Recurse | Where-Object { $_.Name -ne 'KeepClip.package.json' } | Sort-Object FullName | ForEach-Object {
        [ordered]@{ Path = $_.FullName.Substring($bundleRoot.Length + 1).Replace('\','/'); Length = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    [ordered]@{ FormatVersion=1; Architecture='x64'; Files=$entries } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $bundleRoot 'KeepClip.package.json') -Encoding UTF8
}

function Test-KeepClipPackage {
    param([Parameter(Mandatory=$true)][string]$Directory)
    $bundleRoot = (Resolve-Path -LiteralPath $Directory).Path.TrimEnd('\','/')
    $manifestPath = Join-Path $bundleRoot 'KeepClip.package.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { return 'Manifest di verifica mancante: usa lo ZIP con diagnostica.' }
    try { $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -ErrorAction Stop }
    catch { return 'Manifest di verifica non leggibile.' }
    if ($manifest.FormatVersion -ne 1 -or $manifest.Architecture -ne 'x64' -or @($manifest.Files).Count -eq 0) { return 'Manifest di verifica non valido.' }
    $requiredFiles = @('KeepClip.exe','KeepClip.dll','KeepClip.pri','KeepClip.runtimeconfig.json','hostfxr.dll','hostpolicy.dll','coreclr.dll','Microsoft.WindowsAppRuntime.dll','Microsoft.Internal.FrameworkUdk.dll','Microsoft.ui.xaml.dll')
    foreach ($name in $requiredFiles) {
        if (@($manifest.Files | Where-Object { $_.Path -eq $name }).Count -ne 1) { Write-Output "Manifest incompleto: $name" }
    }
    foreach ($entry in $manifest.Files) {
        # A manifest can never direct file reads outside the extracted bundle.
        try { $entryPath = [IO.Path]::GetFullPath((Join-Path $bundleRoot $entry.Path)) }
        catch { Write-Output 'Percorso non valido nel manifest.'; continue }
        if (-not $entryPath.StartsWith($bundleRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { Write-Output 'Percorso esterno alla cartella nel manifest.'; continue }
        if (-not (Test-Path -LiteralPath $entryPath -PathType Leaf)) { Write-Output "File mancante: $($entry.Path)"; continue }
        if ((Get-Item -LiteralPath $entryPath).Length -ne $entry.Length -or (Get-FileHash -LiteralPath $entryPath -Algorithm SHA256).Hash -ne $entry.SHA256) { Write-Output "File diverso o danneggiato: $($entry.Path)" }
    }
}
