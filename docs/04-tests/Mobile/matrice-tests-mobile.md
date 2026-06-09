# Matrice de tests API Mobile SLI

## Commande de référence HTTPS

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force

.\docs\04-tests\Mobile\scripts\run-api-mobile-tests.ps1 `
  -ApiBaseUrl "https://srvapi1.sli.local" `
  -DateTournee "2026-06-09"
```

Adapter `DateTournee` à la date métier autorisée au moment du test.

Validation communiquée le 09/06/2026 :

```text
Total   : 31
OK      : 31
KO      : 0
SKIPPED : 0
```

## Couverture principale contrat mobile strict 1.3

| ID | Scénario | Type | HTTP attendu | Code attendu | Attendu principal |
|---|---|---|---:|---|---|
| MOB-API-001 | Synchronisation valide 1.3 avec trajet | Valide | 200 | SUCCESS | Enregistrement accepté |
| MOB-API-002 | Doublon technique idSynchronisation | Conflit | 409 | SYNCHRONISATION_ALREADY_EXISTS | Même idSynchronisation refusé |
| MOB-API-003 | Double envoi métier date + tournée | Conflit | 409 | TOURNEE_ALREADY_SENT | Même DateTournee + CodeTournee refusé |
| MOB-API-004 | Quantité livrée négative | Invalide | 400 | VALIDATION_ERROR | Quantité livrée négative refusée |
| MOB-API-005 | NON_FAIT sans commentaire | Invalide | 400 | VALIDATION_ERROR | Commentaire obligatoire |
| MOB-API-006 | ANOMALIE sans commentaire | Invalide | 400 | VALIDATION_ERROR | Commentaire obligatoire |
| MOB-API-007 | Ligne validée sans heureValidation | Invalide | 400 | VALIDATION_ERROR | Heure de validation obligatoire |
| MOB-API-008 | estValidee false dans envoi final | Invalide | 400 | VALIDATION_ERROR | Ligne non validée refusée |
| MOB-API-009 | Statut A_FAIRE dans envoi final | Invalide | 400 | VALIDATION_ERROR | A_FAIRE interdit |
| MOB-API-010 | idLigneSource dupliqué | Invalide | 400 | VALIDATION_ERROR | Doublon de ligne refusé |
| MOB-API-011 | codeArticle dupliqué | Invalide | 400 | VALIDATION_ERROR | Doublon article par ligne refusé |
| MOB-API-012 | schemaVersion invalide | Invalide | 400 | VALIDATION_ERROR | Seule la version 1.3 est acceptée |
| MOB-API-013 | quantites vide | Invalide | 400 | VALIDATION_ERROR | Au moins une quantité obligatoire |
| MOB-API-014 | quantiteLivreePrevue négative | Invalide | 400 | VALIDATION_ERROR | Prévision négative refusée |
| MOB-API-015 | quantiteLivreePrevue zéro acceptée | Valide | 200 | SUCCESS | Prévision à zéro acceptée |
| MOB-API-016 | ROLLS_VIDES livré accepté | Valide | 200 | SUCCESS | ROLLS_VIDES livré accepté |
| MOB-API-017 | ROLLS_VIDES avec quantité prévue positive accepté | Valide | 200 | SUCCESS | ROLLS_VIDES prévu positif accepté |
| MOB-API-018 | ROLLS_VIDES récupéré uniquement accepté | Valide | 200 | SUCCESS | ROLLS_VIDES récupéré accepté |
| MOB-API-019 | GET camions disponibles valide | Valide | 200 |  | JSON parseable, schemaVersion 1.3, camions[] présent |
| MOB-API-020 | Synchronisation 1.3 valide avec trajet camion | Valide | 200 | SUCCESS | Trajet camion complet accepté |
| MOB-API-021 | Synchronisation 1.3 sans trajet | Invalide | 400 | VALIDATION_ERROR | trajet obligatoire |
| MOB-API-022 | Synchronisation 1.3 sans camion | Invalide | 400 | VALIDATION_ERROR | trajet.camion obligatoire |
| MOB-API-023 | Synchronisation 1.3 sans idCamion | Invalide | 400 | VALIDATION_ERROR | trajet.camion.idCamion obligatoire |
| MOB-API-024 | Synchronisation 1.3 sans kilometrageDepart | Invalide | 400 | VALIDATION_ERROR | kilométrage départ obligatoire |
| MOB-API-025 | Synchronisation 1.3 sans kilometrageArrivee | Invalide | 400 | VALIDATION_ERROR | kilométrage arrivée obligatoire |
| MOB-API-026 | Synchronisation 1.3 kilometrageDepart négatif | Invalide | 400 | VALIDATION_ERROR | kilométrage départ positif ou nul |
| MOB-API-027 | Synchronisation 1.3 kilometrageArrivee négatif | Invalide | 400 | VALIDATION_ERROR | kilométrage arrivée positif ou nul |
| MOB-API-028 | Synchronisation 1.3 kilometrageArrivee inférieur au départ | Invalide | 400 | VALIDATION_ERROR | arrivée >= départ |
| MOB-API-029 | Synchronisation 1.3 sans dateDepartMobile | Invalide | 400 | VALIDATION_ERROR | date départ obligatoire |
| MOB-API-030 | Synchronisation 1.3 sans dateArriveeMobile | Invalide | 400 | VALIDATION_ERROR | date arrivée obligatoire |
| MOB-API-031 | Synchronisation schemaVersion 1.2 refusée | Invalide | 400 | VALIDATION_ERROR | schemaVersion 1.2 refusé sur POST |

La source structurée de cette matrice est le fichier :

```text
docs/04-tests/Mobile/manifest/tests-mobile.json
```

## Couverture lienAdresseLivraison final

Le champ `pointLivraison.lienAdresseLivraison` est alimenté depuis la vue SQL finale :

```sql
[lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
```

Colonnes attendues :

```text
NUM_CLI
CodePDL
AdresseLivraison
```

| ID | Scénario | Script | Type | HTTP attendu | Attendu |
|---|---|---|---|---:|---|
| MOB-API-LIEN-001 | GET tournée avec vue contenant des liens | scripts/test-lien-adresse-livraison.ps1 | Valide | 200 | schemaVersion inchangé, JSON parseable, au moins un lienAdresseLivraison non vide et URL absolue |
| MOB-API-LIEN-002 | GET tournée avec couple NUM_CLI + CodePDL absent de la vue | scripts/test-lien-adresse-livraison.ps1 | Valide | 200 | schemaVersion inchangé, JSON parseable, lienAdresseLivraison null sur les lignes non trouvées |

Commande type HTTPS :

```powershell
.\docs\04-tests\Mobile\scripts\test-lien-adresse-livraison.ps1 `
  -ApiBaseUrl "https://srvapi1.sli.local" `
  -CodeLivreur "2" `
  -CodeTournee "5001" `
  -ExpectedSchemaVersion "1.2" `
  -ExpectLienAdresseLivraison
```

Le champ `lienAdresseLivraison` ne fait pas partie du payload final de synchronisation.