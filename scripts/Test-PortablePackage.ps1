param([Parameter(Mandatory=$true)][string]$FixturesDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PortablePackage.ps1')
$fixtureRoot = Join-Path ([IO.Path]::GetFullPath($FixturesDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$required = @('KeepClip.exe','KeepClip.dll','KeepClip.pri','KeepClip.runtimeconfig.json','hostfxr.dll','hostpolicy.dll','coreclr.dll','Microsoft.WindowsAppRuntime.dll','Microsoft.Internal.FrameworkUdk.dll','Microsoft.ui.xaml.dll')
foreach ($name in $required) { Set-Content -LiteralPath (Join-Path $fixtureRoot $name) -Value ('fixture ' + $name) -Encoding UTF8 }
Write-KeepClipPackageManifest -Directory $fixtureRoot
if (@(Test-KeepClipPackage -Directory $fixtureRoot).Count -ne 0) { throw 'FAIL: pacchetto integro rifiutato.' }
Write-Output 'PASS: integrita dei file'
Set-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.dll') -Value 'danneggiato' -Encoding UTF8
if (@(Test-KeepClipPackage -Directory $fixtureRoot) -notcontains 'File diverso o danneggiato: KeepClip.dll') { throw 'FAIL: alterazione non rilevata.' }
Write-Output 'PASS: file alterato rilevato'
Set-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.dll') -Value 'fixture KeepClip.dll' -Encoding UTF8
Remove-Item -LiteralPath (Join-Path $fixtureRoot 'KeepClip.pri')
if (@(Test-KeepClipPackage -Directory $fixtureRoot) -notcontains 'File mancante: KeepClip.pri') { throw 'FAIL: file mancante non rilevato.' }
Write-Output 'PASS: estrazione incompleta rilevata'
$manifest = Get-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.package.json') -Raw | ConvertFrom-Json
$manifest.Files += [pscustomobject]@{ Path='../outside.txt'; Length=1; SHA256='00' }
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.package.json') -Encoding UTF8
if (@(Test-KeepClipPackage -Directory $fixtureRoot) -notcontains 'Percorso esterno alla cartella nel manifest.') { throw 'FAIL: percorso esterno non rifiutato.' }
Write-Output 'PASS: percorso esterno rifiutato'
Set-Content -LiteralPath (Join-Path $fixtureRoot 'KeepClip.package.json') -Value '[' -Encoding UTF8
if (@(Test-KeepClipPackage -Directory $fixtureRoot) -notcontains 'Manifest di verifica non leggibile.') { throw 'FAIL: JSON non valido non rilevato.' }
Write-Output 'PASS: manifest non valido rilevato'
$diagnosticScript = Join-Path $fixtureRoot 'Diagnose-Portable.ps1'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Diagnose-Portable.ps1') -Destination $diagnosticScript
$reportPath = Join-Path $fixtureRoot 'missing-helper-report.txt'
& $diagnosticScript -OutputPath $reportPath | Out-Null
if (-not (Test-Path -LiteralPath $reportPath) -or (Get-Content -LiteralPath $reportPath -Raw) -notmatch 'Modulo di verifica mancante') { throw 'FAIL: manca il rapporto se il modulo di verifica e assente.' }
Write-Output 'PASS: rapporto salvato anche con modulo di verifica assente'
Write-Output '6 verifiche superate; nessun avvio o accesso agli appunti.'
