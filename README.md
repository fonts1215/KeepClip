# KeepClip

MVP personale per la cronologia degli appunti, scritto in C# / WinUI 3. Windows 10 dalla build 17763 e Windows 11; eseguibile x64.

## Avvio rapido

La versione aggiornata pronta all'uso è in `artifacts/publish-keepclip-x64/KeepClip.exe`. Conserva l'intera cartella: l'eseguibile dipende dalle DLL incluse. È autonoma e non richiede di installare .NET o Windows App Runtime separatamente.

Per usare la versione portabile ZIP, estrai tutti i file in una cartella stabile prima di avviare l'eseguibile. Non avviarlo direttamente dentro lo ZIP.

Se una versione precedente è già nell'area di notifica, scegli **Esci** nel suo menu prima di aprire quella nuova. L'app permette una sola istanza per sessione Windows.

## Uso

Nelle impostazioni puoi abilitare **Copia alla selezione** e **Suono di conferma copia**, poi premere **Salva impostazioni**. Entrambe le opzioni sono inizialmente disattivate, anche aggiornando una configurazione precedente.

La copia alla selezione acquisisce testo al rilascio del mouse dopo un trascinamento, doppio clic o Shift+clic. Funziona nelle app che espongono il testo selezionato a Windows; esclude campi di input, editor, password e KeepClip, e si sospende insieme alla cattura. Selezionare testo da sostituire in un campo non sovrascrive gli appunti; per copiarlo usa Ctrl+C. La copia automatica richiede che il controllo dichiari il testo non modificabile; se lo stato è sconosciuto o la lettura fallisce, gli appunti restano invariati e puoi usare Ctrl+C. Se l'app non è compatibile, usa Ctrl+C. Le selezioni multiple separate e quelle oltre 100.000 caratteri restano affidate alla copia manuale.

Il suono dura circa 180 millisecondi e accompagna le copie salvate in cronologia, sia manuali sia automatiche. Le copie ignorate, i tentativi falliti e l'incollatura da KeepClip non producono un suono di conferma.

- Copia testo, immagini, file o cartelle con i normali comandi dell'app che stai usando.
- Premi **Ctrl+Shift+V**: la cronologia compare vicino al bordo inferiore del monitor.
- Fai doppio clic su una scheda o selezionala e premi **Invio**: KeepClip torna alla finestra precedente e invia Ctrl+V.
- **Esc** chiude il pannello senza incollare. La cattura continua in background.
- Un clic seleziona la scheda. Usa i pulsanti in basso per incollare, fissare o eliminare la voce selezionata.
- Cerca nel testo o nei nomi dei file; il filtro **★ Preferiti** mostra solo gli elementi fissati. Non è previsto OCR delle immagini.
- **Svuota…** conserva i preferiti salvo scelta esplicita di includerli.

## Impostazioni

Il toggle **Cattura degli appunti** acceso significa **Attiva**, spento significa **In pausa**. Le modifiche si applicano con **Salva impostazioni**. La pausa del menu nell'area di notifica invece si applica immediatamente.

Scorciatoia: Ctrl+Shift, Ctrl+Alt o Alt+Shift e una lettera A–Z. Una combinazione occupata viene segnalata e resta valida quella precedente.

**Avvia all'accesso a Windows** funziona sia con MSIX sia nella versione portabile. Con MSIX usa StartupTask; nella versione portabile registra l'eseguibile per il solo account corrente. Nessuna elevazione richiesta. All'accesso parte nell'area di notifica, senza aprire il pannello. Mantieni il percorso portabile stabile; se lo sposti, salva nuovamente l'impostazione di avvio. Prima di eliminare la cartella portabile, disabilita l'avvio automatico. Le impostazioni di Windows e le policy aziendali possono impedirne l'esecuzione.

## Dati e limiti

Testo Unicode e bitmap normalizzate in PNG. Niente file o formattazione HTML. Massimo 200 elementi e 250 MiB complessivi; massimo 20 MiB per PNG e 40 megapixel prima della decodifica. I preferiti non vengono espulsi automaticamente: quando impediscono l'acquisizione di nuovi elementi, compare un avviso.

Versione portabile: dati in `%LOCALAPPDATA%\ClipShelf`. Installazione MSIX: cartella LocalState del pacchetto. Il percorso effettivo compare nelle impostazioni. Le due modalità hanno cartelle dati separate.

Nessun account o invio di contenuti in rete. I dati non sono cifrati dall'app. Il log `diagnostic.log` contiene eventi, conteggi e codici d'errore, mai testo o immagini copiati. Il ripristino della cronologia crea prima un backup completo dei file, poi una cronologia vuota.

## Sviluppo

Requisiti: .NET SDK 10.0.401 e Windows SDK, con strumenti WinUI/XAML disponibili tramite Visual Studio. I pacchetti NuGet sono fissati; i lock file fanno parte del progetto.

```powershell
dotnet restore ClipShelf.sln
dotnet test tests/ClipShelf.Tests --no-restore
dotnet build src/ClipShelf.App/ClipShelf.App.csproj -c Release -p:Platform=x64
dotnet publish src/ClipShelf.App/ClipShelf.App.csproj -c Release -p:Platform=x64 -o artifacts/publish-x64
```

Chiudi l'app prima di ricompilare lo stesso percorso che stai eseguendo, per evitare file bloccati.

## MSIX

```powershell
./scripts/Build-Package.ps1
# Firma facoltativa con un certificato già presente in CurrentUser/My:
./scripts/Build-Package.ps1 -SkipBuild -CertificateThumbprint '<impronta del certificato>'
```

Output: `artifacts/KeepClip-0.1.0-x64.msix`. L'Identity Publisher del manifest è `CN=ClipShelf`: il certificato di firma deve avere lo stesso subject. Il pacchetto fornito è **non firmato**, quindi non è direttamente installabile. La firma e l'attendibilità del certificato sul dispositivo restano da configurare dall'utente. Lo script non crea certificati, non li installa e non cambia le policy di Windows. La versione portabile si può usare senza questa procedura.

## Verifica e limiti residui

Vedi `docs/manual-verification.md`. I test automatici verificano cronologia, persistenza, ricerca, controllo del destinatario e logica dell'avvio. Non certificano l'effettivo incolla in ogni app né l'accesso successivo a Windows.

Windows può impedire l'incolla verso un processo elevato: l'elemento resta copiato e l'app propone Ctrl+V manuale. La destinazione deve accettare il tipo di contenuto scelto. L'accettazione di SendInput non dimostra che l'app esterna abbia incollato il contenuto.

Le card mostrano nome e icona locale del programma proprietario degli appunti, quando Windows li rende disponibili. La provenienza viene salvata per testo e immagini, anche con la copia su selezione. Le vecchie voci mostrano Origine sconosciuta; ricopiando un contenuto si aggiorna la sua provenienza. Verifica manuale: copia da Blocco note e da un browser, controlla le card e riavvia KeepClip per verificare che nome e icona restino presenti.

Le card hanno un header con icona da 40 px e nome del programma, anteprima separata e data nel footer. Il suono opzionale è un chime morbido da 180 ms. La selezione usa il punto iniziale e finale del trascinamento, l'albero UIA completo e un fallback sul focus limitato allo stesso documento testuale; tre tentativi gestiscono la selezione tardiva, annullandosi se cambiano appunti, focus o gesto. Verifica manualmente trascinamento e doppio clic in ChatGPT e Teams desktop, anche terminando nel margine del messaggio. Se un controllo non espone la selezione a UI Automation, usa Ctrl+C: il log diagnostic.log contiene tentativi/esito senza testo.

Diagnostica selezione v2: registra request, nome processo, fase SetContent/Flush con HRESULT, motivi di annullamento (nuovo gesto, focus, appunti, pausa), numero di provider TextPattern, selezione vuota/discontinua e processi non coincidenti. Non registra testo copiato, titoli delle finestre o contenuti chat. Per raccogliere evidenza, avvia questa versione e ripeti 2–3 selezioni in ChatGPT desktop, annotando una riuscita e una fallita. I log portable si trovano in %LOCALAPPDATA%/ClipShelf/diagnostic.log. La precedente versione publish-diagnostics-x64 aggiungeva solo diagnostica; la versione attuale include la correzione descritta sotto.

Correzione appunti occupati: quando Flush restituisce 0x800401D0, vengono effettuati fino a tre ritentativi dopo 40, 100 e 200 ms. SetContent non viene ripetuto. Se la sequenza cambia, si verifica il token privato: il rendering della stessa copia non annulla il flush, mentre una nuova copia lo annulla per preservarla. Gli errori diversi e quelli persistenti restano visibili nel log. Prova manuale: ripeti la selezione che falliva in ChatGPT desktop e controlla card, suono e Ctrl+V; se copi altro durante l'attesa, deve essere mantenuta la copia più recente.

File e cartelle: Ctrl+C in Esplora file crea una card per l'intera selezione (fino a 1000 elementi), con nomi, conteggio e app di provenienza. Si conservano i riferimenti agli originali, senza duplicarli nella cartella di KeepClip: modifiche agli originali si riflettono nel successivo incolla. Spostamento, eliminazione o indisponibilità di uno degli originali bloccano l'intero incolla con un avviso; puoi ripristinare l'originale o eliminare la card. Preferiti, ricerca, persistenza e suono funzionano anche per i file. L'incolla pubblica file reali nei formati Windows, verso Esplora file o altre app che accettano file; è sempre una copia, anche per elementi acquisiti con Taglia. File virtuali senza percorso non sono supportati. Eliminare card o cronologia non elimina gli originali.


## Icona dell'app

Logo ufficiale: due fogli corallo e ambra con sfondo trasparente. Gli asset per eseguibile, finestre, area di notifica e MSIX sono inclusi nel progetto. Per rigenerarli dal master: `./scripts/Generate-AppIcons.ps1`. Vedi `branding/README.md` per dimensioni, provenienza e prompt.

Verifica manuale: dopo aver chiuso la versione precedente, avvia `artifacts/publish-keepclip-x64/KeepClip.exe` e controlla l'icona nell'area di notifica, nell'header della cronologia e nella barra del titolo delle impostazioni. In Esplora file verifica l'icona dell'eseguibile; prova anche un fattore di scala dello schermo diverso dal 100%.

## Cambio di nome

KeepClip è il nuovo nome di ClipShelf. Il nome compare in finestre, notifiche, metadati Windows e pacchetto; l'eseguibile è `KeepClip.exe`. L'icona corallo e ambra resta quella scelta.

Per conservare i dati e la compatibilità, restano la cartella portabile `%LOCALAPPDATA%\ClipShelf`, l'identità MSIX `ClipShelf.Personal` / `CN=ClipShelf`, gli identificatori di avvio e di istanza unica e i nomi interni dei progetti. La prenotazione di KeepClip e l'identità definitiva dello Store vanno configurate nella preparazione alla pubblicazione.

Chiudi la versione precedente prima di avviare KeepClip. Se usi l'avvio automatico portabile, attivalo nelle impostazioni di questa versione e premi Salva impostazioni: il percorso registrato deve puntare a `KeepClip.exe` nella cartella che conserverai.
