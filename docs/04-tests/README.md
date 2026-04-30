# Tests API mobile

Date de validation : 2026-04-30  
URL locale utilisée : `http://localhost:5120`

---

# Test GET /api

## Objectif

Vérifier que l’API répond correctement avant de tester les routes métier.

## Test de disponibilité de l’API

```powershell
curl.exe -i "http://localhost:5120"
```

Résultat attendu :

```http
HTTP/1.1 200 OK
```

Si la route racine n’existe pas dans le projet, ce test peut retourner `404 Not Found`. Dans ce cas, ce n’est pas bloquant si les routes métier fonctionnent correctement.

---

# Test GET /api/tournees/jour validé

## Objectif

Vérifier que l’API est capable de charger une tournée du jour depuis les données ABSSolute.

## Route testée

```http
GET /api/tournees/jour?dateTournee=2026-04-28&codeTournee=2001&codeLivreur=2
```

## Commande utilisée

```powershell
curl.exe -i "http://localhost:5120/api/tournees/jour?dateTournee=2026-04-28&codeTournee=2001&codeLivreur=2"
```

## Résultat attendu

L’API doit retourner un JSON contenant :

```text
schemaVersion
dateTournee
jourTournee
jourLibelle
codeTournee
libelleTournee
statutSynchronisation
livreur
chargement
lignes
```

## Exemple de structure attendue

```json
{
  "schemaVersion": "1.0",
  "dateTournee": "2026-04-28",
  "jourTournee": 2,
  "jourLibelle": "Mardi",
  "codeTournee": "2001",
  "libelleTournee": "MDR VENDEE",
  "statutSynchronisation": "NON_ENVOYEE",
  "livreur": {
    "codeLivreur": "2",
    "nomLivreur": "DAVID LEBAS"
  },
  "chargement": {
    "dateGenerationApi": "2026-04-28T07:30:00+02:00",
    "nombrePointsEnvoyes": 8
  },
  "lignes": [
    {
      "idLigneSource": "2026-04-28|2001|2|1058|1|1",
      "ordreArret": 1,
      "horaire": 1,
      "client": {
        "numClient": "1058",
        "nomClient": "EHPAD L EQUAIZIERE",
        "nomAffiche": "EHPAD EQUAIZIERE GARNACHE"
      },
      "pointLivraison": {
        "codePDL": "1",
        "descriptionPDL": "EHPAD EQUAIZIERE GARNACHE",
        "adresseLigne1": "7 RUE JAN ET JOEL MARTEL",
        "adresseLigne2": null,
        "adresseLigne3": "-",
        "ville": "LA GARNACHE",
        "codePostal": "85710"
      },
      "tournee": {
        "codeTournee": "2001",
        "libelleTournee": "MDR VENDEE",
        "jourTournee": 2,
        "schemaLivraison": "1W1"
      },
      "retour": {
        "jourTourneeRetour": 2,
        "jourRetourLibelle": "Mardi",
        "codeTourneeRetour": "2001",
        "libelleTourneeRetour": "MDR VENDEE"
      },
      "infosLivreur": {
        "instructions": null,
        "commentaireFiche": null,
        "zoneDechargement": "EHPAD",
        "zone": "EHPAD",
        "precision": null,
        "cle": null,
        "estFerme": false,
        "dateFermeture": null,
        "motifFermeture": null
      },
      "saisie": {
        "nbExpes": 0,
        "nbRolls": 0,
        "nbVetements": 0,
        "nbTapis": 0,
        "nbSacs": 0,
        "nbRecuperes": 0,
        "precisionLivreur": null,
        "statutPassage": "A_FAIRE",
        "commentaireLivreur": null,
        "heureValidation": null,
        "estValidee": false
      }
    }
  ]
}
```

## Points vérifiés

```text
La route GET retourne bien une tournée.
La tournée contient les informations générales.
Le livreur est présent.
Les lignes de tournée sont présentes.
Chaque ligne contient un client.
Chaque ligne contient un point de livraison.
Chaque ligne contient une structure saisie initialisée.
Le statut de synchronisation initial est NON_ENVOYEE.
Le statut de passage initial est A_FAIRE.
Les quantités initiales sont à 0.
```

---

# Test GET avec paramètres manquants ou invalides

## Date manquante

```powershell
curl.exe -i "http://localhost:5120/api/tournees/jour?codeTournee=2001&codeLivreur=2"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

## Code tournée manquant

```powershell
curl.exe -i "http://localhost:5120/api/tournees/jour?dateTournee=2026-04-28&codeLivreur=2"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

## Code livreur manquant

```powershell
curl.exe -i "http://localhost:5120/api/tournees/jour?dateTournee=2026-04-28&codeTournee=2001"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

## Date invalide

```powershell
curl.exe -i "http://localhost:5120/api/tournees/jour?dateTournee=date-invalide&codeTournee=2001&codeLivreur=2"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

---

# Test POST /api/synchronisations validé

Date du test : 2026-04-30

## Objectif

Vérifier que l’application mobile peut envoyer une tournée renseignée en fin de journée.

La route doit :

```text
insérer ou mettre à jour le livreur dans Mobile_Livreur ;
insérer la tournée dans Mobile_Tournee ;
insérer les lignes dans Mobile_TourneeLigne ;
calculer QuantiteLivree ;
calculer QuantiteReprise ;
insérer un log dans Mobile_LogSynchronisation ;
bloquer les erreurs métier ;
bloquer le double envoi.
```

## Route testée

```http
POST /api/synchronisations
```

## Exemple de commande utilisée

```powershell
curl.exe -X POST "http://localhost:5120/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync.json"
```

## Réponse obtenue

```json
{
  "statut": "SUCCESS",
  "message": "Synchronisation enregistrée avec succès."
}
```

---

# Test du double envoi

Si on essaye d’envoyer deux fois le même fichier avec le même `idSynchronisation`, l’API refuse le deuxième envoi.

## Commande utilisée

```powershell
curl.exe -X POST "http://localhost:5120/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync.json"
```

## Réponse obtenue

```json
{
  "statut": "CONFLICT",
  "message": "Une synchronisation avec l'ID 4e17a871-5fc5-4f49-8d01-4d791a6d9941 existe déjà."
}
```

## Code HTTP obtenu

```http
HTTP/1.1 409 Conflict
```

---

# Bloc PowerShell pour effectuer les tests POST

Copier-coller ce bloc pour effectuer les tests :

```powershell
$ApiUrl = "http://localhost:5120/api/synchronisations"

function Test-Synchronisation {
    param(
        [string]$NomTest,
        [string]$Fichier,
        [string]$ResultatAttendu
    )

    Write-Host ""
    Write-Host "============================================================"
    Write-Host "TEST : $NomTest"
    Write-Host "FICHIER : $Fichier"
    Write-Host "ATTENDU : $ResultatAttendu"
    Write-Host "============================================================"

    curl.exe -i -X POST $ApiUrl `
      -H "Content-Type: application/json" `
      --data-binary "@$Fichier"
}

Test-Synchronisation `
    -NomTest "1 - Synchronisation valide" `
    -Fichier "sync-valide.json" `
    -ResultatAttendu "SUCCESS"

Test-Synchronisation `
    -NomTest "2 - Doublon avec le même idSynchronisation" `
    -Fichier "sync-doublon.json" `
    -ResultatAttendu "CONFLICT"

Test-Synchronisation `
    -NomTest "3 - Quantité négative" `
    -Fichier "sync-quantite-negative.json" `
    -ResultatAttendu "VALIDATION_ERROR"

Test-Synchronisation `
    -NomTest "4 - NON_FAIT sans commentaire" `
    -Fichier "sync-non-fait-sans-commentaire.json" `
    -ResultatAttendu "VALIDATION_ERROR"

Test-Synchronisation `
    -NomTest "5 - ANOMALIE sans commentaire" `
    -Fichier "sync-anomalie-sans-commentaire.json" `
    -ResultatAttendu "VALIDATION_ERROR"

Test-Synchronisation `
    -NomTest "6 - Ligne validée sans heureValidation" `
    -Fichier "sync-validee-sans-heure.json" `
    -ResultatAttendu "VALIDATION_ERROR"

Test-Synchronisation `
    -NomTest "7 - estValidee false dans l’envoi final" `
    -Fichier "sync-est-validee-false.json" `
    -ResultatAttendu "VALIDATION_ERROR"
```

---

# Résultats POST validés

| Test | Fichier JSON | Résultat attendu | Résultat obtenu |
|---|---|---|---|
| Synchronisation valide | `sync-valide.json` | `SUCCESS` | `200 OK / SUCCESS` |
| Doublon `idSynchronisation` | `sync-doublon.json` | `CONFLICT` | `409 Conflict / CONFLICT` |
| Quantité négative | `sync-quantite-negative.json` | `VALIDATION_ERROR` | `400 Bad Request / VALIDATION_ERROR` |
| `NON_FAIT` sans commentaire | `sync-non-fait-sans-commentaire.json` | `VALIDATION_ERROR` | `400 Bad Request / VALIDATION_ERROR` |
| `ANOMALIE` sans commentaire | `sync-anomalie-sans-commentaire.json` | `VALIDATION_ERROR` | `400 Bad Request / VALIDATION_ERROR` |
| Ligne validée sans `heureValidation` | `sync-validee-sans-heure.json` | `VALIDATION_ERROR` | `400 Bad Request / VALIDATION_ERROR` |
| `estValidee = false` | `sync-est-validee-false.json` | `VALIDATION_ERROR` | `400 Bad Request` |

---

# Vérifications SQL réalisées après le POST

Après le test valide, les données ont été vérifiées dans :

```text
Mobile_Tournee
Mobile_TourneeLigne
Mobile_LogSynchronisation
```

## Requêtes SQL utilisées

```sql
SELECT TOP 10 *
FROM Mobile_Tournee
ORDER BY IdTourneeMobile DESC;
```

```sql
SELECT TOP 50 *
FROM Mobile_TourneeLigne
ORDER BY IdTourneeMobile DESC, OrdreArret;
```

```sql
SELECT TOP 10 *
FROM Mobile_LogSynchronisation
ORDER BY IdLog DESC;
```

## Résultat confirmé

```text
Mobile_Tournee :
- tournée insérée ;
- statut ENVOYEE ;
- verrouillage actif ;
- DateEnvoi renseignée ;
- NombrePointsPrevus renseigné ;
- NombrePointsSaisis renseigné.

Mobile_TourneeLigne :
- ligne insérée ;
- client inséré ;
- point de livraison inséré ;
- quantités détaillées insérées ;
- QuantiteLivree calculée ;
- QuantiteReprise calculée ;
- statut FAIT enregistré ;
- heureValidation enregistrée ;
- précision livreur enregistrée.

Mobile_LogSynchronisation :
- log ENVOI_REUSSI créé ;
- niveau INFO ;
- message de succès enregistré ;
- détail technique enregistré ;
- appareil mobile enregistré ;
- version application enregistrée.
```

---

# Test consultation admin des synchronisations envoyées

## Objectif

Vérifier que l’API permet de consulter les synchronisations déjà envoyées.

Ces routes serviront plus tard à l’interface admin Windows.

## Routes testées

```http
GET /api/synchronisations
GET /api/synchronisations/{idTourneeMobile}
GET /api/synchronisations?dateTournee=2026-04-28
GET /api/synchronisations?dateTournee=2026-04-28&codeTournee=2001&codeLivreur=2
```

## Liste complète

```powershell
curl.exe -i "http://localhost:5120/api/synchronisations"
```

## Filtre par date

```powershell
curl.exe -i "http://localhost:5120/api/synchronisations?dateTournee=2026-04-28"
```

## Filtre par date + tournée

```powershell
curl.exe -i "http://localhost:5120/api/synchronisations?dateTournee=2026-04-28&codeTournee=2001"
```

## Filtre par date + tournée + livreur

```powershell
curl.exe -i "http://localhost:5120/api/synchronisations?dateTournee=2026-04-28&codeTournee=2001&codeLivreur=2"
```

## Détail d’une synchronisation

Remplacer `9` par l’`IdTourneeMobile` vu en SQL.

```powershell
curl.exe -i "http://localhost:5120/api/synchronisations/9"
```

## Résultat attendu pour la liste

```json
{
  "statut": "SUCCESS",
  "count": 1,
  "synchronisations": [
    {
      "idTourneeMobile": 9,
      "idSynchronisation": "4e17a871-5fc5-4f49-8d01-4d791a6d9941",
      "dateTournee": "2026-04-28T00:00:00",
      "codeTournee": "2001",
      "libelleTournee": "MDR VENDEE",
      "idLivreur": 9,
      "codeLivreur": "2",
      "nomLivreur": "DAVID LEBAS",
      "statutSynchronisation": "ENVOYEE",
      "estVerrouillee": true,
      "nombrePointsPrevus": 1,
      "nombrePointsSaisis": 1,
      "nomAppareil": "Samsung A15",
      "versionApplication": "1.0.0",
      "nombreFaits": 1,
      "nombreNonFaits": 0,
      "nombreAnomalies": 0
    }
  ]
}
```

## Résultat attendu pour le détail

```json
{
  "statut": "SUCCESS",
  "synchronisation": {
    "idTourneeMobile": 9,
    "idSynchronisation": "4e17a871-5fc5-4f49-8d01-4d791a6d9941",
    "dateTournee": "2026-04-28T00:00:00",
    "codeTournee": "2001",
    "libelleTournee": "MDR VENDEE",
    "codeLivreur": "2",
    "nomLivreur": "DAVID LEBAS",
    "statutSynchronisation": "ENVOYEE",
    "lignes": [
      {
        "idTourneeLigne": 6,
        "idTourneeMobile": 9,
        "ordreArret": 1,
        "numClient": "1058",
        "nomClient": "EHPAD L EQUAIZIERE",
        "codePDL": "1",
        "descriptionPDL": "EHPAD EQUAIZIERE GARNACHE",
        "quantiteLivree": 4,
        "quantiteReprise": 2,
        "nbExpes": 0,
        "nbRolls": 3,
        "nbVetements": 0,
        "nbTapis": 1,
        "nbSacs": 0,
        "nbRecuperes": 2,
        "precisionLivreur": "2 rolls repris au local arrière",
        "statutPassage": "FAIT",
        "commentaireLivreur": null,
        "heureValidation": "2026-04-28T09:12:00",
        "estValidee": true
      }
    ],
    "logs": [
      {
        "typeEvenement": "ENVOI_REUSSI",
        "niveau": "INFO",
        "message": "Synchronisation enregistrée avec succès.",
        "detailTechnique": "Tournée 2001 du 2026-04-28 synchronisée avec 1 ligne(s).",
        "nomAppareil": "Samsung A15",
        "versionApplication": "1.0.0"
      }
    ]
  }
}
```

---

# Conclusion des tests

Les tests confirment que :

```text
Le GET de chargement permet de récupérer une tournée exploitable par le mobile.
Le POST de synchronisation enregistre correctement les données du soir.
Les règles métier principales sont bien appliquées.
Le double envoi avec le même idSynchronisation est bloqué.
Les données sont bien présentes dans SQL Server.
La consultation admin permet de relire les synchronisations envoyées.
```