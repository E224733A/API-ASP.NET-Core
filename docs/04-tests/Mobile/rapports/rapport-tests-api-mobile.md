# Rapport tests API Mobile SLI v1.2

Date execution : 2026-06-02 11:52:18

API testee : http://192.168.1.233:5000/api/synchronisations

Date tournee utilisee : 2026-06-02

RunId : 20260602115216

## Synthese

| Indicateur | Valeur |
|---|---:|
| Total | 18 |
| OK | 18 |
| KO | 0 |
| SKIPPED | 0 |

## Detail

| ID | Scenario | HTTP attendu | HTTP obtenu | Statut attendu | Statut obtenu | Code attendu | Code obtenu | Resultat |
|---|---|---:|---:|---|---|---|---|---|
| MOB-API-001 | Synchronisation valide | 200 | 200 | SUCCESS | SUCCESS | - | - | OK |
| MOB-API-002 | Doublon technique idSynchronisation | 409 | 409 | CONFLICT | CONFLICT | SYNCHRONISATION_ALREADY_EXISTS | SYNCHRONISATION_ALREADY_EXISTS | OK |
| MOB-API-003 | Double envoi metier date plus tournee | 409 | 409 | CONFLICT | CONFLICT | TOURNEE_ALREADY_SENT | TOURNEE_ALREADY_SENT | OK |
| MOB-API-004 | Quantite livree negative | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-005 | NON_FAIT sans commentaire | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-006 | ANOMALIE sans commentaire | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-007 | Ligne validee sans heureValidation | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-008 | estValidee false dans envoi final | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-009 | Statut A_FAIRE dans envoi final | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-010 | idLigneSource duplique | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-011 | codeArticle duplique | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-012 | schemaVersion invalide | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-013 | quantites vide | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-014 | quantiteLivreePrevue negative | 400 | 400 | VALIDATION_ERROR | VALIDATION_ERROR | - | - | OK |
| MOB-API-015 | quantiteLivreePrevue zero acceptee | 200 | 200 | SUCCESS | SUCCESS | - | - | OK |
| MOB-API-016 | ROLLS_VIDES livre accepte | 200 | 200 | SUCCESS | SUCCESS | - | - | OK |
| MOB-API-017 | ROLLS_VIDES avec quantite livree positive accepte | 200 | 200 | SUCCESS | SUCCESS | - | - | OK |
| MOB-API-018 | ROLLS_VIDES avec quantite prevue positive accepte | 200 | 200 | SUCCESS | SUCCESS | - | - | OK |

## Fichiers generes

- CSV : C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\API\API-ASP.NET-Core\docs\04-tests\Mobile\rapports\resultats-tests-api-mobile.csv
- JSON : C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\API\API-ASP.NET-Core\docs\04-tests\Mobile\rapports\resultats-tests-api-mobile.json
- Payloads envoyes : C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\API\API-ASP.NET-Core\docs\04-tests\Mobile\rapports\payloads-envoyes
- Reponses API : C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\API\API-ASP.NET-Core\docs\04-tests\Mobile\rapports\responses

