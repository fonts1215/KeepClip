# KeepClip

MVP personale per la cronologia degli appunti, scritto in C# / WinUI 3. Windows 10 dalla build 17763 e Windows 11; eseguibile x64.

## Avvio rapido

La versione aggiornata è in `artifacts/publish-clean-pc-diagnostics-x64/KeepClip.exe`. Conserva l'intera cartella: l'eseguibile dipende dalle DLL incluse. Il pacchetto include .NET e Windows App Runtime; l'avvio su un PC pulito resta da verificare con il rapporto diagnostico.

Per usare la versione portabile ZIP, estrai tutti i file in una cartella stabile prima di avviare l'eseguibile. Non avviarlo direttamente dentro lo ZIP.

Se la finestra non compare su un altro PC, estrai il nuovo ZIP in una cartella vuota e fai doppio clic su **Avvia-con-diagnostica.cmd**. Questo controlla tutti i file estratti tramite SHA256, poi avvia KeepClip e raccoglie codice di uscita, versione Windows, architettura, eventi di avvio del log e dettagli pertinenti del registro Applicazione di Windows. Il rapporto viene salvato in `%TEMP%/KeepClip-avvio-*.txt`; la console mostra il percorso completo. Lo script non installa runtime o certificati, non modifica impostazioni di Windows e non cambia permanentemente la policy PowerShell. Non raccoglie contenuti degli appunti o della cronologia. Se KeepClip e gia aperta, occorre uscire dalla precedente istanza prima di ripetere la prova.

Se una versione precedente è già nell'area di notifica, scegli **Esci** nel suo menu prima di aprire quella nuova. L'app permette una sola istanza per sessione Windows.

## Uso

Nell'**Editor del tema** scegli Sistema, Chiaro o Scuro e una delle otto palette: Corallo, Blu elettrico, Viola, Fucsia neon, Ciano, Lime neon, Arancio acceso e Rosso pop. Attiva «Colora anche sfondo e header delle card» per un aspetto più colorato. Le modifiche sono visibili nell'anteprima delle impostazioni; «Salva impostazioni» le applica alla cronologia e le conserva al riavvio. Chiudere senza salvare lascia il tema precedente. «Ripristina tema predefinito» prepara Sistema e Corallo, da salvare. La modalità Sistema segue Windows anche durante l'uso; il contrasto elevato di Windows ha priorità sulle palette.

Nelle impostazioni puoi abilitare **Copia alla selezione** e **Suono di conferma copia**, poi premere **Salva impostazioni**. Entrambe le opzioni sono inizialmente disattivate, anche aggiornando una configurazione precedente.

La copia alla selezione acquisisce testo al rilascio del mouse dopo un trascinamento, doppio clic o Shift+clic. Funziona nelle app che espongono il testo selezionato a Windows; esclude campi di input, editor, password e KeepClip, e si sospende insieme alla cattura. Selezionare testo da sostituire in un campo non sovrascrive gli appunti; per copiarlo usa Ctrl+C. La copia automatica richiede che il controllo dichiari il testo non modificabile; se lo stato è sconosciuto o la lettura fallisce, gli appunti restano invariati e puoi usare Ctrl+C. Se l'app non è compatibile, usa Ctrl+C. Le selezioni multiple separate e quelle oltre 100.000 caratteri restano affidate alla copia manuale.

Nelle impostazioni puoi regolare il volume del suono da 0 a 100% e ascoltarlo con «Prova suono» prima di salvare. Il 50% mantiene il livello precedente; il 100% ne raddoppia l'ampiezza e lo 0% è silenzioso. Il livello viene conservato al riavvio. Il suono dura circa 180 millisecondi e accompagna le copie salvate in cronologia, sia manuali sia automatiche. Le copie ignorate, i tentativi falliti e l'incollatura da KeepClip non producono un suono di conferma.

- Copia testo, immagini, file o cartelle con i normali comandi dell'app che stai usando.
- Premi **Ctrl+Shift+V**: la cronologia compare vicino al bordo inferiore del monitor.
- Fai **un clic sull'anteprima o sull'header** di una scheda: KeepClip torna alla finestra da cui hai aperto il pannello e invia Ctrl+V. In alternativa, seleziona con le frecce e premi **Invio**.
- **Esc** chiude il pannello senza incollare. La cattura continua in background.
- Ogni card ha i propri pulsanti **☆ Preferito / ★ Fissato** ed **Elimina**: agiscono su quella copia senza incollare o chiudere il pannello. Anche Invio sul pulsante esegue solo la sua azione.
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

Requisiti: .NET SDK 10.0.401 e Windows SDK, con strumenti WinUI/XAML disponibili tramite Visual Studio. Apri la soluzione `KeepClip.slnx`. Le versioni NuGet sono centralizzate in `Directory.Packages.props`; i progetti dichiarano solo i pacchetti utilizzati e i lock file fanno parte del progetto.

```powershell
dotnet restore KeepClip.slnx --locked-mode
dotnet test tests/KeepClip.Tests --no-restore
dotnet test tests/KeepClip.Windows.Tests --no-restore
dotnet build src/KeepClip.App/KeepClip.App.csproj -c Release -p:Platform=x64
dotnet publish src/KeepClip.App/KeepClip.App.csproj -c Release -p:Platform=x64 -o artifacts/publish-x64
./scripts/Build-Portable.ps1 -SkipBuild -PublishDirectory ./artifacts/publish-x64
```

Per aggiornare una dipendenza, modifica la sua versione in `Directory.Packages.props`, esegui `dotnet restore KeepClip.slnx --force-evaluate` e includi nel commit i lock file aggiornati. Il restore in modalità bloccata verifica che le dipendenze corrispondano ai lock file.

`Build-Portable.ps1` genera lo ZIP con launcher diagnostico e `KeepClip.package.json`, il manifest di integrita dei file. Per ricompilare e generare insieme lo ZIP, ometti `-SkipBuild`. Il manifest segnala anche estrazioni incomplete o file alterati, senza dipendere dalle DLL presenti sul PC di sviluppo. La verifica locale delle DLL del publish corrente non ha rilevato importazioni dirette mancanti; questo non dimostra l'avvio su ogni versione di Windows. I sei test del verificatore si eseguono con `./scripts/Test-PortablePackage.ps1 -FixturesDirectory <cartella-di-prova>` e non avviano KeepClip.

Chiudi l'app prima di ricompilare lo stesso percorso che stai eseguendo, per evitare file bloccati.

### Servizio asincrono degli appunti

L'app usa un unico `AsyncClipboardService` per cattura, copia alla selezione e incolla. La coda serializza le operazioni e un dispatcher STA dedicato esegue le chiamate Windows fuori dal thread della finestra. Una scrittura in attesa viene annullata se gli appunti cambiano o la richiesta non è più valida (nuovo gesto, cambio di focus, pausa o chiusura del pannello). Il controllo viene ripetuto subito prima di pubblicare il contenuto, anche dopo la preparazione di immagini e file. La chiusura cancella le richieste pendenti; le chiamate Windows già in corso terminano prima di liberare la coda.

La libreria [AsyncWindowsClipboard](https://github.com/undergroundwires/AsyncWindowsClipboard), distribuita come NuGet `AsyncClipboardService` 1.7.1 e fissata in `Directory.Packages.props`, legge testo e liste di file. Se la lettura fallisce, non restituisce dati o produce una lista non valida, il servizio usa la lettura Windows. Le immagini restano gestite dalle API Windows. Per testo e immagini, le scritture WinRT pubblicano contenuto e identificatore privato con ritentativi di Flush. Per i file, la stessa coda STA pubblica direttamente `CF_HDROP` Unicode, `Preferred DropEffect=Copy` e un identificatore privato in memoria Windows: la lista completa resta disponibile dopo l'uscita dall'app, senza un Flush WinRT. La libreria non supporta direttamente immagini o questa scrittura di più formati.

I test del servizio usano backend simulati, senza leggere o modificare gli appunti del PC. La verifica nelle applicazioni esterne resta manuale.

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

Le card mostrano nome e icona locale del programma da cui proviene la copia. Il proprietario degli appunti ha priorità; se è assente o è un intermediario di Windows, si usa l'app in primo piano rilevata alla notifica della copia, abbinata alla stessa sequenza degli appunti. Il percorso dell'eseguibile si legge con permessi limitati, anche nella versione portable. Per le app con pacchetto, un errore nel caricamento del logo non elimina il nome già riconosciuto. Il riconoscimento resta una stima nei casi intermediati: una copia in background senza proprietario può essere attribuita all'app attiva. Se non ci sono informazioni utilizzabili, resta «Origine sconosciuta».

La provenienza viene salvata per testo, immagini e file, anche con la copia su selezione. Le voci già salvate non vengono corrette retroattivamente: ricopiando un contenuto si aggiorna la sua provenienza. Il log `diagnostic.log` contiene `source-identity`, `source-attribution` ed eventuali `source-error`, con PID, nome processo, disponibilità del percorso/logo e codici di errore; non registra testo copiato, percorsi o titoli delle finestre.

Le card hanno un header con icona da 40 px e nome del programma, anteprima separata e data nel footer. Il suono opzionale è un chime morbido da 180 ms. La selezione usa il punto iniziale e finale del trascinamento, l'albero UIA completo e un fallback sul focus limitato allo stesso documento testuale; tre tentativi gestiscono la selezione tardiva, annullandosi se cambiano appunti, focus o gesto. Verifica manualmente trascinamento e doppio clic in ChatGPT e Teams desktop, anche terminando nel margine del messaggio. Se un controllo non espone la selezione a UI Automation, usa Ctrl+C: il log diagnostic.log contiene tentativi/esito senza testo.

Diagnostica selezione v2: registra request, nome processo, fase SetContent/Flush con HRESULT, motivi di annullamento (nuovo gesto, focus, appunti, pausa), numero di provider TextPattern, selezione vuota/discontinua e processi non coincidenti. Non registra testo copiato, titoli delle finestre o contenuti chat. Per raccogliere evidenza, avvia questa versione e ripeti 2–3 selezioni in ChatGPT desktop, annotando una riuscita e una fallita. I log portable si trovano in %LOCALAPPDATA%/ClipShelf/diagnostic.log. La precedente versione publish-diagnostics-x64 aggiungeva solo diagnostica; la versione attuale include la correzione descritta sotto.

Correzione appunti occupati: quando Flush restituisce 0x800401D0, vengono effettuati fino a tre ritentativi dopo 40, 100 e 200 ms. SetContent non viene ripetuto. Se la sequenza cambia, si verifica il token privato: il rendering della stessa copia non annulla il flush, mentre una nuova copia lo annulla per preservarla. Gli errori diversi e quelli persistenti restano visibili nel log. Prova manuale: ripeti la selezione che falliva in ChatGPT desktop e controlla card, suono e Ctrl+V; se copi altro durante l'attesa, deve essere mantenuta la copia più recente.

File e cartelle: Ctrl+C in Esplora file crea una card per l'intera selezione (fino a 1000 elementi), con nomi, conteggio e app di provenienza. Si conservano i riferimenti agli originali, senza duplicarli nella cartella di KeepClip: modifiche agli originali si riflettono nel successivo incolla. Spostamento, eliminazione o indisponibilità di uno degli originali bloccano l'intero incolla con un avviso; puoi ripristinare l'originale o eliminare la card. Preferiti, ricerca, persistenza e suono funzionano anche per i file. L'incolla pubblica la lista nativa Windows [CF_HDROP](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard#cf_hdrop), utilizzabile da Esplora file e dalle app che accettano file dagli appunti. Il trasferimento preferito è sempre una copia, anche per elementi acquisiti con Taglia. File virtuali senza percorso non sono supportati. Eliminare card o cronologia non elimina gli originali.

Correzione dell'incolla dei file nelle chat: il percorso passa dalla pubblicazione WinRT di StorageItems a una lista `CF_HDROP` esplicita. La preparazione avviene prima di aprire gli appunti; le verifiche della sequenza e della richiesta vengono ripetute dopo OpenClipboard, immediatamente prima di sostituire i dati. Se gli appunti sono occupati, si ritenta dopo 40, 100 e 200 ms. Il marcatore privato evita di riacquisire l'incolla come una nuova copia da origine sconosciuta. I log riportano backend, numero di file, tipo di contenuto ed eventuali errori, senza nomi o percorsi dei file. La compatibilità effettiva con Teams/ChatGPT deve essere verificata dall'utente con lo stesso file per cui Ctrl+V diretto funziona.


## Icona dell'app

Logo ufficiale: due fogli corallo e ambra con sfondo trasparente. Gli asset per eseguibile, finestre, area di notifica e MSIX sono inclusi nel progetto. Per rigenerarli dal master: `./scripts/Generate-AppIcons.ps1`. Vedi `branding/README.md` per dimensioni, provenienza e prompt.

Verifica manuale: dopo aver chiuso la versione precedente, avvia `artifacts/publish-async-clipboard-x64/KeepClip.exe` e controlla l'icona nell'area di notifica, nell'header della cronologia e nella barra del titolo delle impostazioni. In Esplora file verifica l'icona dell'eseguibile; prova anche un fattore di scala dello schermo diverso dal 100%.

## Cambio di nome

KeepClip è il nuovo nome di ClipShelf. Il nome compare in finestre, notifiche, metadati Windows e pacchetto; l'eseguibile è `KeepClip.exe`. L'icona corallo e ambra resta quella scelta.

Per conservare i dati e la compatibilità, restano la cartella portabile `%LOCALAPPDATA%\ClipShelf`, l'identità MSIX `ClipShelf.Personal` / `CN=ClipShelf`, gli identificatori di avvio e di istanza unica. Soluzione, progetti, cartelle e namespace ora usano KeepClip. La prenotazione di KeepClip e l'identità definitiva dello Store vanno configurate nella preparazione alla pubblicazione.

Chiudi la versione precedente prima di avviare KeepClip. Se usi l'avvio automatico portabile, attivalo nelle impostazioni di questa versione e premi Salva impostazioni: il percorso registrato deve puntare a `KeepClip.exe` nella cartella che conserverai.
