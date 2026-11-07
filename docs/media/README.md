# Media dimostrativi di KeepClip

`keepclip-overview.png` mostra il pannello completo. `keepclip-search.gif` alterna due catture reali dell'interfaccia: la cronologia iniziale e il risultato della ricerca “Ciao”. Ogni fotogramma rimane visibile per tre secondi; la GIF è una sequenza illustrativa, non una registrazione continua.

## Contenuti e privacy

Le immagini sono state preparate da una copia temporanea dell'app, separata dalla versione personale in uso:

- archivio creato da zero con cinque voci inventate: due testi, il logo KeepClip, un file dimostrativo e una lista per il weekend;
- nomi delle applicazioni di provenienza assegnati come esempi e icone ricavate dal logo del progetto;
- database e file dimostrativi confinati nella cartella temporanea della demo;
- monitor degli appunti, copia alla selezione, hook delle scorciatoie e avvio automatico non inizializzati;
- adattatore degli appunti sostituito con uno inerte, che non legge né scrive gli appunti di Windows;
- cattura limitata alla finestra dell'app: nessun desktop, barra delle applicazioni, finestra esterna o schermata delle impostazioni;
- immagini ricodificate da soli pixel, senza conservare i metadati della cattura.

Il layout e i controlli mostrati appartengono all'interfaccia WinUI reale. La copia temporanea usa un percorso dati dedicato e rimane visibile durante la cattura; queste modifiche non fanno parte dell'app distribuita.

## Aggiornare le immagini

Per una nuova cattura, prepara un'istanza separata con soli contenuti dimostrativi e disattiva prima l'accesso agli appunti reali. Controlla ogni fotogramma a grandezza naturale, soprattutto nomi utente, percorsi, titoli delle finestre e notifiche. Pubblica soltanto i media verificati, mai database, log, cartelle di publish o dati di lavoro.

Le modifiche limitate ai file Markdown e alla cartella `docs/` sono escluse dai trigger automatici del workflow Windows CI.
