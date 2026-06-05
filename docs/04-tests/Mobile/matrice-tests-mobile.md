# Matrice de tests API Mobile SLI v1.3 strict

Cette matrice couvre le contrat mobile final en `schemaVersion` `1.3` strict pour le choix camion, le trajet et les kilométrages.

Elle documente aussi le contrôle du champ optionnel `pointLivraison.lienAdresseLivraison` ajouté au GET de chargement de tournée mobile, sans changement de `schemaVersion`.

## Contrat 1.3 strict

```text
schemaVersion doit être exactement "1.3".
schemaVersion "1.2" est refusé.
Toute autre schemaVersion est refusée.
trajet obligatoire.
trajet.camion obligatoire.
trajet.camion.idCamion obligatoire.
trajet.kilometrageDepart obligatoire.
trajet.kilometrageArrivee obligatoire.
trajet.dateDepartMobile obligatoire.
trajet.dateArriveeMobile obligatoire.
kilometrageDepart >= 0.
kilometrageArrivee >= 0.
kilometrageArrivee >= kilometrageDepart.
dateArriveeMobile >= dateDepartMobile.
```

## Couverture

```text
MOB-API-001 à MOB-API-018 : scénarios historiques conservés et adaptés au contrat 1.3.
MOB-API-019 : GET /api/camions/disponibles.
MOB-API-020 à MOB-API-031 : scénarios camion/trajet et refus de schemaVersion 1.2.
MOB-API-LIEN-001 à MOB-API-LIEN-002 : contrôles dédiés au champ optionnel lienAdresseLivraison sur GET /api/tournees/jour.
k6 : test de masse POST /api/synchronisations en schemaVersion 1.3 avec trajet camion.
```

Dernière exécution communiquée par test local/utilisateur :

```text
Tests API Mobile : 31/31 OK sur http://srvapi1.sli.local:5000.
Test k6 1.3 : 20/20 HTTP 200 SUCCESS sur http://srvapi1.sli.local:5000 avec trajet camion.
```

Les tests doivent être relancés après toute modification de code, de contrat ou de base.

## Scénarios POST synchronisation et GET camions

| ID | Scenario | Fichier | Type | HTTP attendu | Statut attendu | Code attendu |
|---|---|---|---|---:|---|---|
| MOB-API-001 | Synchronisation valide en 1.3 avec trajet valide | sync-valide.json | Valide | 200 | SUCCESS | |
| MOB-API-002 | Doublon technique idSynchronisation | sync-doublon.json | Conflit | 409 | CONFLICT | SYNCHRONISATION_ALREADY_EXISTS |
| MOB-API-003 | Double envoi metier date + tournee | sync-double-envoi-tournee.json | Conflit | 409 | CONFLICT | TOURNEE_ALREADY_SENT |
| MOB-API-004 | Quantite livree negative | sync-quantite-negative.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-005 | NON_FAIT sans commentaire | sync-non-fait-sans-commentaire.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-006 | ANOMALIE sans commentaire | sync-anomalie-sans-commentaire.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-007 | Ligne validee sans heureValidation | sync-validee-sans-heure.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-008 | estValidee false dans envoi final | sync-est-validee-false.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-009 | Statut A_FAIRE dans envoi final | sync-a-faire.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-010 | idLigneSource duplique | sync-idligne-duplique.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-011 | codeArticle duplique | sync-code-article-duplique.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-012 | schemaVersion invalide | sync-schema-version-invalide.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-013 | quantites vide | sync-quantites-vide.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-014 | quantiteLivreePrevue negative | sync-quantite-prevue-negative.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-015 | quantiteLivreePrevue zero acceptee en 1.3 avec trajet valide | sync-prevu-zero.json | Valide | 200 | SUCCESS | |
| MOB-API-016 | ROLLS_VIDES livre accepte en 1.3 avec trajet valide | sync-valide-rolls-vides-livree.json | Valide | 200 | SUCCESS | |
| MOB-API-017 | ROLLS_VIDES avec quantite prevue positive accepte en 1.3 avec trajet valide | sync-valide-rolls-vides-prevue.json | Valide | 200 | SUCCESS | |
| MOB-API-018 | ROLLS_VIDES recupere uniquement accepte en 1.3 avec trajet valide | sync-valide-rolls-vides.json | Valide | 200 | SUCCESS | |
| MOB-API-019 | GET camions disponibles valide avec schemaVersion 1.3 et camions[] | GET /api/camions/disponibles | Valide | 200 | | |
| MOB-API-020 | Synchronisation 1.3 valide avec trajet camion | sync-valide-v13-trajet-camion.json | Valide | 200 | SUCCESS | |
| MOB-API-021 | Synchronisation 1.3 sans trajet | sync-v13-sans-trajet.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-022 | Synchronisation 1.3 sans camion | sync-v13-sans-camion.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-023 | Synchronisation 1.3 sans idCamion | sync-v13-sans-id-camion.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-024 | Synchronisation 1.3 sans kilometrageDepart | sync-v13-sans-km-depart.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-025 | Synchronisation 1.3 sans kilometrageArrivee | sync-v13-sans-km-arrivee.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-026 | Synchronisation 1.3 kilometrageDepart negatif | sync-v13-km-depart-negatif.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-027 | Synchronisation 1.3 kilometrageArrivee negatif | sync-v13-km-arrivee-negatif.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-028 | Synchronisation 1.3 kilometrageArrivee inferieur au depart | sync-v13-km-arrivee-inferieur-depart.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-029 | Synchronisation 1.3 sans dateDepartMobile | sync-v13-sans-date-depart.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-030 | Synchronisation 1.3 sans dateArriveeMobile | sync-v13-sans-date-arrivee.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-031 | Synchronisation schemaVersion 1.2 refusee | sync-schema-version-12-refusee.json | Invalide | 400 | VALIDATION_ERROR | |

## Scénarios GET chargement tournée - lienAdresseLivraison

| ID | Scenario | Script | Type | HTTP attendu | Attendu |
|---|---|---|---|---:|---|
| MOB-API-LIEN-001 | GET tournée en mode Hardcoded | scripts/test-lien-adresse-livraison.ps1 | Valide | 200 | schemaVersion inchangé, JSON parseable, lienAdresseLivraison présent et URL absolue |
| MOB-API-LIEN-002 | GET tournée en mode Disabled | scripts/test-lien-adresse-livraison.ps1 | Valide | 200 | schemaVersion inchangé, JSON parseable, lienAdresseLivraison null |

Commande type en mode Hardcoded :

```powershell
.\docs\04-tests\Mobile\scripts\test-lien-adresse-livraison.ps1 `
  -ApiBaseUrl "http://srvapi1.sli.local:5000" `
  -CodeLivreur "2" `
  -CodeTournee "4001" `
  -ExpectedSchemaVersion "1.2" `
  -ExpectLienAdresseLivraison
```

Commande type en mode Disabled :

```powershell
.\docs\04-tests\Mobile\scripts\test-lien-adresse-livraison.ps1 `
  -ApiBaseUrl "http://srvapi1.sli.local:5000" `
  -CodeLivreur "2" `
  -CodeTournee "4001" `
  -ExpectedSchemaVersion "1.2"
```
