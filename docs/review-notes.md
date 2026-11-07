# Revisione e decisioni di implementazione

Una revisione indipendente in sola lettura ha individuato cinque problemi importanti. Le correzioni sono state implementate dall'autore:

1. Letture degli appunti attraverso pausa/ripresa: notifiche in pausa ignorate e token generazione per scartare letture obsolete. Test di regressione osservato fallire e poi passare.
2. Ripristino non atomico: backup completo prima di alterare i file originali; reset e pubblicazione della cronologia vuota sotto la stessa esclusione del servizio. Test con immagine bloccata e database corrotto, osservati fallire e poi passare.
3. Incolla immagini dopo cambio sessione del pannello: destinatario salvato prima del primo await, sessione invalidata a chiusura/riapertura, completamenti obsoleti scartati. Test della sessione osservati fallire e poi passare; integrazione desktop da verificare.
4. DPI dipendente dalla finestra destinataria: spostamento della finestra KeepClip sul monitor scelto e lettura del suo DPI per-monitor prima del ridimensionamento. Build verificata; prova su DPI misti ancora non eseguita.
5. Errori di pulizia dopo commit: l'intera operazione di pulizia, inclusa enumerazione, è best effort per IOException/UnauthorizedAccessException. Test di errore dopo commit osservato fallire e poi passare.

Il commento sulla firma PFX è stato corretto: lo script firma mediante certificato già presente in CurrentUser/My, senza presumere un prompt SignTool per password PFX. Nessun problema minore della revisione lasciato intenzionalmente irrisolto.

Decisioni rispetto al piano:

- Nuovo repository autonomo del progetto, branch feat/clipboard-mvp: non esisteva un repository da isolare. Nessun merge o push previsto.
- Comandi PowerShell e registro di avanzamento sostituiscono gli helper Bash del workflow.
- Build portabile autonoma come prima consegna, più MSIX generato separatamente: si può provare l'app prima di configurare firma e attendibilità. Rischio: installazione MSIX ancora da verificare dopo firma.
- Massimo 40 megapixel prima di decodificare, oltre ai limiti salvati concordati: evita allocazioni enormi. Rischio: immagini eccezionalmente grandi vengono rifiutate con un avviso.
- Avvio portabile aggiunto su richiesta dell'utente; percorso stabile necessario. Il codice di registrazione è pronto ma l'agente non abilita l'avvio automatico sul dispositivo.
- Diagnostica limitata a eventi/tipi/codici/conteggi, senza contenuti copiati, per indagare la cattura durante il collaudo.

La verifica dell'interfaccia è stata interrotta dall'utente con Esc; i risultati automatici non vengono presentati come prove di incolla reale su Windows 10/11.
