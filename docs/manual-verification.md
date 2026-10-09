# Verifica manuale dell'MVP

## Evidenza raccolta

- Primo build WinUI 3 Release x64: 0 errori, 0 avvisi.
- Verifica automatica aggiornata: **58/58 test superati**, inclusi recupero di database corrotto, backup fallito, pulizia dopo commit, generazione della cattura, sessione incolla, layout Win32 x64, selezioni consentite/escluse, avvisi di salvataggio e compatibilità delle impostazioni precedenti.
- Prima generazione MSIX: riuscita, non firmata.
- Applicazione avviata; l'utente ha mostrato le impostazioni in esecuzione.
- Test 1 dell'utente: schede assenti. Diagnosi confermata dal file di impostazioni: `IsPaused=true`. Corretto il toggle ambiguo (ora acceso = cattura attiva). Ripetizione del test dopo abilitazione ancora da confermare.
- Automazione dell'interfaccia interrotta dall'utente con Esc: nessuna prova di incolla reale dichiarata superata.
- Nuova prova desktop: Ctrl+Shift+V dallo Strumento di cattura apre il pannello; un clic sul pulsante Copia esterno chiude il pannello.
- PNG nativo dello Strumento di cattura: lettore effettivo verificato sul desktop, 24.003 byte e firma PNG valida; nessun contenuto stampato. Normalizzazione e acquisizione completa nella lista ancora da riprovare con cattura attiva: la configurazione corrente è in pausa.

## Checklist per l'utente

Prima di cominciare: usa la build aggiornata, imposta Cattura degli appunti su **Attiva** e premi **Salva impostazioni**.

| Test | Azioni | Risultato atteso | Stato |
|---|---|---|---|
| 1 — Testo | Copia due testi da Blocco note; Ctrl+Shift+V | Due schede, ultima copia per prima | Da ripetere dopo correzione toggle |
| 2 — Incolla | Cursore in Blocco note; Ctrl+Shift+V; un clic sulla scheda, poi doppio clic | Un clic seleziona senza chiudere; il doppio clic chiude e inserisce il testo nel punto del cursore | Da riprovare dopo correzione focus |
| 3 — Duplicati | Ricopia uno dei testi | Stessa voce in cima, nessun duplicato | Automatico; desktop non verificato |
| 4 — Ricerca | Cerca una parola presente in una scheda | Solo le corrispondenze | Automatico; desktop non verificato |
| 5 — Preferiti | Seleziona con frecce, ★ Preferito; filtro preferiti | Solo le voci fissate | Automatico; desktop non verificato |
| 6 — Immagine | Copia un'immagine da Paint; scegli la scheda tornando a Paint | Anteprima e immagine incollata | Non verificato |
| 7 — Esc/background | Chiudi con Esc; copia altro testo; riapri | Nessun incolla alla chiusura; nuova voce acquisita | Non verificato |
| 8 — Pausa | Pausa, copia un testo, riprendi, riapri | La copia durante la pausa non viene aggiunta | Automatico; desktop non verificato |
| 9 — Riavvio | Esci dal menu e riavvia | Cronologia e preferiti conservati | Persistenza automatica; desktop non verificato |
| 10 — Avvio automatico | Abilita e salva; disconnetti/accedi a Windows | Solo icona nell'area di notifica; hotkey disponibile | Non verificato |
| 11 — Conflitto hotkey | Scegli una combinazione già occupata | Avviso; vecchia hotkey resta attiva | Non verificato |
| 12 — Limiti | Cronologia oltre 200; alcune voci preferite | Espulsione dei soli non preferiti più vecchi | Automatico |
| 13 — App elevata | Tenta incolla in una finestra amministratore | Ripiego copia manuale quando input bloccato | Orchestrazione automatica; desktop non verificato |
| 14 — DPI | Apri da finestre su monitor con scale diverse | Pannello interamente visibile nell'area di lavoro | Non verificato |
| 15 — Explorer | Riavvia Explorer e controlla icona | Icona ricreata | Non verificato |
| 16 — Windows 10/11 | Ripeti testo/immagine/hotkey su entrambi | Flusso funzionante su entrambi | Matrice OS non verificata |
| 17 — Clic fuori | Ctrl+Shift+V; clic su un'altra app senza prima cliccare il pannello | Chiusura al primo clic esterno | Prova desktop superata; caso intermittente da confermare nell'uso quotidiano |
| 18 — Strumento di cattura | Cattura attiva; premi Copia nella toolbar | Scheda immagine con anteprima nella cronologia | Lettura PNG nativa superata; aggiunta alla lista da riprovare |
| 19 — Spazi card | Apri con avviso di pausa visibile; chiudi l'avviso e riapri | Card intera con data leggibile; altezza pannello adattata senza tagli | Build riuscita; verifica visiva affidata all'utente |
| 20 — Copia alla selezione | Cattura attiva; abilita la nuova opzione e salva; seleziona testo in Blocco note senza Ctrl+C, poi apri il pannello | Nuova voce con il testo selezionato; anche Ctrl+V in un documento di prova usa quel testo | Logica automatica superata; compatibilità desktop affidata all'utente |
| 21 — Suono | Abilita suono e salva; copia con Ctrl+C e con selezione; disabilita suono e salva | Un suono breve per copia acquisita; nessun suono con opzione disattivata | Risorsa audio inclusa; ascolto affidato all'utente |
| 22 — Esclusioni selezione | Seleziona nella ricerca KeepClip; pausa cattura e seleziona in Blocco note | Nessuna copia automatica; cronologia invariata | Logica automatica superata; desktop affidato all'utente |

Riporta numero del test, applicazione destinataria, passaggi e risultato. Se la cattura attiva fallisce, controlla l'eventuale avviso e il log locale `diagnostic.log`, che non contiene i dati copiati.

## File e cartelle
1. Copia un file da Esplora file con Ctrl+C. Apri una cartella di destinazione, poi Ctrl+Shift+V e doppio clic sulla card: il file deve comparire nella destinazione e restare nell'origine.
2. Ripeti con più file e una cartella: una sola card, nomi e conteggio corretti, tutta la selezione incollata.
3. Cerca il nome di un file e aggiungi la card ai preferiti. Riavvia KeepClip: card e riferimenti devono essere conservati.
4. Su file di prova, sposta o elimina un originale: la card deve indicare originali non disponibili e l'incolla deve avvisare senza copiare solo gli altri file.
5. Elimina la card: gli originali devono restare intatti.
6. Su un file di prova, usa Taglia, poi incolla dalla card di KeepClip: deve essere copiato senza rimuovere l'originale.
7. Ripeti una copia di testo e un'immagine per confermare i flussi precedenti.

## Copia alla selezione: protezione dei campi di input

Attiva la copia alla selezione e il suono nelle impostazioni. Queste prove manuali sono da eseguire dall'utente:

1. Copia con Ctrl+C il testo `NUOVO TESTO`. In un dialog nel browser scrivi `vecchio`, selezionalo con trascinamento o doppio clic, aspetta almeno un secondo e premi Ctrl+V: il campo deve diventare `NUOVO TESTO`, senza card o suono al momento della selezione.
2. Ripeti selezionando con Shift+clic, usando una textarea multilinea e un editor contenteditable. Ripeti in Blocco note e nel campo messaggio di Teams/ChatGPT: la selezione negli editor non deve alterare gli appunti.
3. Seleziona invece testo leggibile di una pagina o di un messaggio ricevuto in ChatGPT/Teams: deve ancora essere copiato automaticamente e salvato. Ripeti dopo avere dato focus al campo messaggio, per controllare che la presenza di un editor nella stessa pagina non blocchi i messaggi.
4. Seleziona testo in un campo e premi Ctrl+C: la copia manuale deve funzionare ed essere salvata normalmente.
5. Ripeti la prova 1 con un'immagine o un file negli appunti: la selezione nel campo non deve sostituirli; incollali poi in una destinazione compatibile.

Il log registra `uia-blocked reason=editing-control` oppure `editable-text-range` e `readOnly=False`, senza leggere o registrare il testo del campo bloccato. Quando lo stato non è disponibile, misto o la lettura genera un errore, la copia viene saltata (`unconfirmed-read-only`, `unknown-control` o `unknown-provider`, `readOnly=unknown`): gli appunti restano invariati e puoi usare Ctrl+C. La copia automatica richiede che il testo sia dichiarato non modificabile dal controllo; questa prova verifica anche eventuali limiti delle app destinatarie.

## Servizio asincrono e AsyncWindowsClipboard

Queste prove richiedono la versione `artifacts/publish-async-clipboard-x64/KeepClip.exe` e restano affidate all'utente:

1. Copia testo Unicode (accenti, emoji e caratteri non latini) da Blocco note e Teams. Controlla testo e provenienza nella card, quindi incolla in un documento di prova.
2. Copia insieme un file e una cartella da Esplora file. Controlla che la card contenga entrambi e che l'incolla li copi tutti nella destinazione.
3. Copia un'immagine con lo Strumento di cattura, poi incollala in un'app compatibile. Controlla anteprima e provenienza.
4. Ripeti copia alla selezione e incolla dal pannello. Ogni copia deve comparire una sola volta; l'incolla di una card non deve generare una nuova cattura o un suono aggiuntivo.
5. Durante l'acquisizione di un contenuto, usa hotkey, ricerca ed Esc: il pannello deve restare reattivo. Copia poi un altro contenuto: una scrittura precedente ancora in attesa deve essere annullata e lasciare gli appunti più recenti.
6. Chiudi l'app mentre un'acquisizione è in corso, poi riavviala: nessuna richiesta della vecchia istanza deve completare una scrittura dopo la chiusura.

Gli eventuali fallback della libreria sono registrati come `clipboard-library fallback`, solo con tipo di eccezione e HRESULT. I test del dispatcher STA usano delegati gestiti e non leggono, scrivono o svuotano gli appunti del PC.

## Volume del suono di copia

Usa `artifacts/publish-volume-x64/KeepClip.exe`, dopo aver chiuso la versione precedente. Le prove con riproduzione audio restano affidate all'utente:

1. Apri le impostazioni: sotto «Suono di conferma copia» trovi lo slider 0–100%, la percentuale e «Prova suono». Le preferenze esistenti devono partire dal 50%, mantenendo il livello precedente.
2. Prova 25%, 50% e 100% con il pulsante: il livello deve aumentare e il timbro restare uguale. Il pulsante consente l'anteprima anche quando il suono automatico è disattivato; trascinare lo slider non deve riprodurre audio.
3. Attiva il suono, scegli 75% e premi «Salva impostazioni». Copia un nuovo testo: deve comparire la card e suonare al livello scelto. Chiudi e riapri KeepClip: lo slider deve mostrare ancora 75%.
4. Salva 0%: copia un altro testo, poi un'immagine o un file; le card devono apparire senza suono. «Prova suono» a 0% deve indicare che il volume è silenzioso.
5. Disattiva il toggle e salva: le copie devono essere silenziose, mantenendo il livello dello slider. Riattiva e salva: il livello scelto deve tornare operativo.
6. Premi più volte «Prova suono» a livelli diversi e chiudi l'app durante la riproduzione: nessun suono deve continuare dopo l'uscita. Controlla anche che slider e pulsante siano raggiungibili con Tab e che i tasti freccia regolino il livello.
## Temi e palette

Dopo aver chiuso la versione precedente, avvia `artifacts/publish-themes-fixed-x64/KeepClip.exe`. Queste prove dell'interfaccia sono affidate all'utente:

1. Apri le impostazioni e scegli Chiaro e Scuro: pagina, testi, anteprima e controlli devono cambiare subito. Prova tutte le otto palette, in particolare Lime neon, Ciano e Fucsia neon. Testi, pulsanti, cursore del volume e interruttori devono restare leggibili anche al passaggio del mouse.
2. Attiva «Colora anche sfondo e header delle card»: l'anteprima deve mostrare sfondi con una tinta leggera e un header del colore scelto. Disattivandolo, gli sfondi tornano neutri e il colore principale rimane sui controlli.
3. Salva una combinazione Scuro / Fucsia neon / sfondi colorati. Apri la cronologia: card, header, selezione, ricerca e pulsante Incolla devono seguire la scelta. Controlla anche i dialog di svuotamento e ripristino, annullandoli. Riavvia: la scelta deve essere conservata, insieme a scorciatoia, volume, pausa e copia alla selezione.
4. Cambia palette e modalità nelle impostazioni, poi chiudi senza salvare: riaprendo cronologia e impostazioni deve restare la scelta precedentemente salvata.
5. Salva Sistema e cambia tra tema chiaro e scuro nelle impostazioni di Windows: KeepClip deve aggiornarsi senza riavvio. Salva invece una modalità esplicita: deve conservarla quando Windows cambia tema.
6. Attiva temporaneamente un tema a contrasto elevato in Windows: pagina, testi e controlli devono usare i colori di sistema. Disattivalo: deve tornare la palette scelta.
7. Usa «Ripristina tema predefinito», poi salva: tornano Sistema, Corallo e sfondi neutri. Verifica che Salva resti visibile quando scorri e che modalità, palette e opzione siano utilizzabili con Tab, Invio e Spazio. Ripeti a DPI 125%/150% e su Windows 10 e 11.