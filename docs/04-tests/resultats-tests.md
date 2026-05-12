# Résultats des tests API Mobile SLI v1.2

Date de validation : 2026-05-12  
URL locale : `http://localhost:5120`  
Dossier de test : `docs/04-tests`

Les tests POST du contrat JSON v1.2 ont été exécutés avec succès.

| N° | Test | Fichier | Résultat attendu | Résultat obtenu |
|---:|---|---|---|---|
| 01 | Synchronisation valide | `sync-valide.json` | `200 OK / SUCCESS` | `HTTP/1.1 200 OK` |
| 02 | Doublon technique `idSynchronisation` | `sync-doublon.json` | `409 Conflict / SYNCHRONISATION_ALREADY_EXISTS` | `HTTP/1.1 409 Conflict` |
| 03 | Double envoi métier date + tournée + livreur | `sync-double-envoi-tournee.json` | `409 Conflict / TOURNEE_ALREADY_SENT` | `HTTP/1.1 409 Conflict` |
| 04 | Quantité négative | `sync-quantite-negative.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |
| 05 | `NON_FAIT` sans commentaire | `sync-non-fait-sans-commentaire.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |
| 06 | `ANOMALIE` sans commentaire | `sync-anomalie-sans-commentaire.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |
| 07 | Ligne validée sans `heureValidation` | `sync-validee-sans-heure.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |
| 08 | `estValidee = false` | `sync-est-validee-false.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |
| 09 | `A_FAIRE` dans l’envoi final | `sync-a-faire.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |
| 10 | `idLigneSource` dupliqué | `sync-idligne-duplique.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |
| 11 | `codeArticle` dupliqué | `sync-code-article-duplique.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |
| 12 | `schemaVersion` non supportée | `sync-schema-version-invalide.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |
| 13 | `quantites[]` vide | `sync-quantites-vide.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |
| 14 | `quantiteLivreePrevue` négative | `sync-quantite-prevue-negative.json` | `400 Bad Request / VALIDATION_ERROR` | `HTTP/1.1 400 Bad Request` |

## Conclusion

Les contrôles suivants sont validés :

```text
- envoi valide ;
- anti-doublon technique ;
- anti-doublon métier ;
- refus des quantités négatives ;
- refus des statuts NON_FAIT et ANOMALIE sans commentaire ;
- refus d’une ligne validée sans heure ;
- refus d’une ligne non validée dans le POST final ;
- refus du statut A_FAIRE dans le POST final ;
- refus des idLigneSource dupliqués ;
- refus des codeArticle dupliqués ;
- refus d’une version de schéma non supportée ;
- refus d’un tableau quantites[] vide ;
- refus de quantiteLivreePrevue négative.
```
