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
| 22 — Esclusioni selezione | Seleziona nella ricerca ClipShelf; pausa cattura e seleziona in Blocco note | Nessuna copia automatica; cronologia invariata | Logica automatica superata; desktop affidato all'utente |

Riporta numero del test, applicazione destinataria, passaggi e risultato. Se la cattura attiva fallisce, controlla l'eventuale avviso e il log locale `diagnostic.log`, che non contiene i dati copiati.
