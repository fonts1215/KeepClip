param([switch]$Launch, [string]$OutputPath)
$ErrorActionPreference = 'Stop'
if (-not $OutputPath) { $OutputPath = Join-Path $env:TEMP ('KeepClip-avvio-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt') }
$report = New-Object 'System.Collections.Generic.List[string]'
function Add-Report([string]$Line) { $report.Add($Line); Write-Output $Line }
try {
    Add-Report 'KeepClip - diagnostica avvio portable v1'
    Add-Report ('Windows: ' + [Environment]::OSVersion.Version)
    Add-Report ('Architettura sistema: ' + $env:PROCESSOR_ARCHITECTURE + ' / emulazione: ' + $env:PROCESSOR_ARCHITEW6432)
    Add-Report ('PowerShell: ' + $PSVersionTable.PSVersion)
    $helperPath = Join-Path $PSScriptRoot 'KeepClip.Package.ps1'
    if (-not (Test-Path -LiteralPath $helperPath -PathType Leaf)) {
        Add-Report 'Modulo di verifica mancante: KeepClip.Package.ps1. Estrai tutti i file dello ZIP in una cartella vuota.'
        throw [IO.FileNotFoundException]::new('Modulo di verifica mancante.')
    }
    . $helperPath
    $problems = @(Test-KeepClipPackage -Directory $PSScriptRoot)
    if ($problems.Count -gt 0) {
        foreach ($problem in $problems) { Add-Report $problem }
        Add-Report 'Avvio saltato: estrai nuovamente tutti i file dello ZIP in una cartella vuota.'
    }
    else {
        Add-Report 'Integrita pacchetto: OK (tutti i file e SHA256 corrispondono).'
        if ($Launch) {
            if (Get-Process -Name KeepClip -ErrorAction SilentlyContinue) { Add-Report 'KeepClip e gia in esecuzione. Esci dal menu della tray e ripeti la diagnostica.' }
            else {
                $start = Get-Date
                try {
                    # This branch is only run by the user through Avvia-con-diagnostica.cmd.
                    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
                    $startInfo.FileName = Join-Path $PSScriptRoot 'KeepClip.exe'
                    $startInfo.WorkingDirectory = $PSScriptRoot
                    $startInfo.UseShellExecute = $false
                    $process = New-Object System.Diagnostics.Process
                    $process.StartInfo = $startInfo
                    try {
                        if (-not $process.Start()) { throw [InvalidOperationException]::new('Avvio non riuscito.') }
                        $finished = $process.WaitForExit(8000)
                        if ($finished) {
                            $exitCode = $process.ExitCode
                            if ($null -eq $exitCode) { Add-Report 'Processo terminato: codice di uscita non disponibile.' }
                            else { Add-Report ('Processo terminato: codice={0}, hex=0x{0:X8}' -f [int]$exitCode) }
                        }
                        else { Add-Report 'Processo ancora in esecuzione dopo 8 secondi. Controlla finestra, area di notifica e Ctrl+Shift+V.' }
                    }
                    finally { $process.Dispose() }
                }
                catch {
                    $errorCause = $_.Exception
                    for ($depth = 0; $errorCause -and $depth -lt 8; $depth++) {
                        Add-Report ('Avvio fallito: tipo=' + $errorCause.GetType().FullName + ', HRESULT=0x{0:X8}' -f $errorCause.HResult)
                        if ($errorCause -is [ComponentModel.Win32Exception]) { Add-Report ('Errore Win32=' + $errorCause.NativeErrorCode) }
                        $errorCause = $errorCause.InnerException
                    }
                }
                $logPath = Join-Path $env:LOCALAPPDATA 'KeepClip/diagnostic.log'
                if (Test-Path -LiteralPath $logPath -PathType Leaf) {
                    $startupLines = @(Get-Content -LiteralPath $logPath -Tail 120 | Where-Object { $_ -match '^([0-9T:.+Z-]+)\s' -and [DateTimeOffset]::Parse($matches[1]).LocalDateTime -ge $start.AddSeconds(-2) -and $_ -match 'app-start|app-ready|startup-failure|shelf-start|startup-stage|startup-error' })
                    Add-Report 'Log di avvio (solo eventi avvio, senza contenuti copiati):'
                    if ($startupLines.Count -eq 0) { Add-Report 'Nessun nuovo evento di avvio nel log.' }
                    foreach ($line in $startupLines) { Add-Report $line }
                }
                else { Add-Report 'Log app non creato: il problema puo precedere la costruzione della finestra.' }
                try {
                    $events = @(Get-WinEvent -FilterHashtable @{ LogName='Application'; Id=1000,1001,1026; StartTime=$start.AddSeconds(-2) } -MaxEvents 20 -ErrorAction SilentlyContinue)
                    foreach ($event in $events) {
                        [xml]$xml = $event.ToXml()
                        if ($xml.InnerText -notmatch 'KeepClip(?:\.exe|\.dll)') { continue }
                        Add-Report ('Evento Windows: ID=' + $event.Id + ', ora=' + $event.TimeCreated.ToString('O'))
                        foreach ($data in $xml.Event.EventData.Data) {
                            if ($data.Name -in @('AppName','ModuleName','ExceptionCode','AppVersion','ModuleVersion')) { Add-Report ($data.Name + '=' + $data.InnerText) }
                        }
                        foreach ($match in [regex]::Matches($xml.InnerText, '(?:System|Microsoft|WinRT|KeepClip)\.[A-Za-z0-9_.]+Exception|0x[0-9a-fA-F]{8}')) { Add-Report ('Dettaglio=' + $match.Value) }
                    }
                }
                catch { Add-Report ('Eventi Windows non leggibili: ' + $_.Exception.GetType().Name) }
            }
        }
        else { Add-Report 'Solo verifica file: nessun avvio effettuato.' }
    }
}
catch { Add-Report ('Diagnostica interrotta: tipo=' + $_.Exception.GetType().FullName + ', HRESULT=0x{0:X8}' -f $_.Exception.HResult) }
finally {
    $report | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    Write-Output ('Rapporto: ' + $OutputPath)
}
