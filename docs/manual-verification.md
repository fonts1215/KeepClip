# Verifica manuale dell'MVP

## Evidenza raccolta

- Primo build WinUI 3 Release x64: 0 errori, 0 avvisi.
- Verifica automatica finale: **37/37 test superati**, inclusi recupero di database corrotto, backup fallito, pulizia dopo commit, generazione della cattura, sessione incolla e logica dell'avvio portabile.
- Prima generazione MSIX: riuscita, non firmata.
- Applicazione avviata; l'utente ha mostrato le impostazioni in esecuzione.
- Test 1 dell'utente: schede assenti. Diagnosi confermata dal file di impostazioni: `IsPaused=true`. Corretto il toggle ambiguo (ora acceso = cattura attiva). Ripetizione del test dopo abilitazione ancora da confermare.
- Automazione dell'interfaccia interrotta dall'utente con Esc: nessuna prova di incolla reale dichiarata superata.

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

Riporta numero del test, applicazione destinataria, passaggi e risultato. Se la cattura attiva fallisce, controlla l'eventuale avviso e il log locale `diagnostic.log`, che non contiene i dati copiati.
