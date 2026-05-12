# Swagger — API Mobile SLI v1.2

Date de mise à jour : 2026-05-12  
Contrat JSON : `schemaVersion = "1.2"`

Swagger est l’interface de documentation interactive de l’API ASP.NET Core. Il permet de consulter les routes disponibles, les paramètres attendus, les exemples de réponses et les modèles JSON utilisés par le mobile.

Swagger documente le contrat technique de l’API. Le cahier des charges reste la référence fonctionnelle.

---

## 1. Accès local

Démarrer l’API :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"
dotnet build
dotnet run
```

Ouvrir Swagger :

```text
http://localhost:5120/swagger/index.html
```

Si l’API est lancée sur un autre port, utiliser l’adresse affichée par Kestrel dans le terminal.

---

## 2. Points importants pour Swagger

Swagger lit les commentaires XML des contrôleurs et des modèles. Les commentaires doivent rester alignés avec le contrat v1.2.

À vérifier dans le code :

```text
Controllers/SynchronisationsController.cs
Controllers/TourneesController.cs
Models/TourneeMobileDto.cs
Models/TourneeDisponibleDto.cs
Models/SynchronisationTourneeRequest.cs
Models/SynchronisationLigneRequest.cs
Models/SynchronisationSaisieRequest.cs
Models/SynchronisationQuantiteRequest.cs
```

Les anciennes mentions `schemaVersion = 1.1` doivent être remplacées par `schemaVersion = 1.2`.

Les exemples de quantités doivent inclure :

```json
{
  "codeArticle": "ROLLS",
  "libelle": "Rolls",
  "quantiteLivreePrevue": null,
  "quantiteLivree": 1,
  "quantiteRecuperee": 2
}
```

---

## 3. Routes exposées

| Méthode | Route | Rôle |
|---|---|---|
| `GET` | `/api/health` | Vérifier que l’API répond |
| `GET` | `/api/health/abssolute` | Vérifier la connexion aux vues ABSSolute |
| `GET` | `/api/health/mobile` | Vérifier la connexion à la base mobile |
| `GET` | `/api/livreurs` | Consulter les livreurs |
| `GET` | `/api/tournees/disponibles` | Lister les tournées disponibles pour une date |
| `GET` | `/api/tournees/jour` | Charger une tournée complète |
| `POST` | `/api/synchronisations` | Enregistrer l’envoi final du mobile |
| `GET` | `/api/synchronisations` | Consulter les synchronisations reçues |
| `GET` | `/api/synchronisations/{idTourneeMobile}` | Consulter le détail d’une synchronisation |
| `GET` | `/api/debug/sql/*` | Routes de debug SQL en développement |

---

## 4. Tester depuis Swagger

### 4.1 Tournées disponibles

Route :

```http
GET /api/tournees/disponibles
```

Paramètres :

```text
dateTournee = 2026-05-07
codeLivreur = 2
```

Réponse attendue :

```json
{
  "schemaVersion": "1.2",
  "dateTournee": "2026-05-07",
  "dateModifiable": false,
  "livreur": {
    "codeLivreur": "2",
    "nomLivreur": "DAVID LEBAS"
  },
  "tournees": [
    {
      "codeTournee": "4006",
      "libelleTournee": "BOUAYE",
      "nombrePoints": 16
    }
  ]
}
```

---

### 4.2 Tournée du jour

Route :

```http
GET /api/tournees/jour
```

Paramètres :

```text
dateTournee = 2026-05-07
codeTournee = 4006
codeLivreur = 2
```

La réponse doit contenir :

```text
schemaVersion = 1.2
dateModifiable = false
articlesSaisissables
lignes[]
saisie.quantites[]
quantiteLivreePrevue
zoneDechargementAffichee
```

---

### 4.3 Synchronisation finale

Route :

```http
POST /api/synchronisations
```

Exemple de corps JSON v1.2 :

```json
{
  "schemaVersion": "1.2",
  "idSynchronisation": "11111111-1111-1111-1111-111111111111",
  "dateTournee": "2026-05-07",
  "codeTournee": "4006",
  "libelleTournee": "BOUAYE",
  "livreur": {
    "codeLivreur": "2",
    "nomLivreur": "DAVID LEBAS"
  },
  "mobile": {
    "nomAppareil": "Postman Test",
    "versionApplication": "1.0.0",
    "dateChargementMobile": "2026-05-07T07:30:00+02:00",
    "dateEnvoiMobile": "2026-05-07T16:45:00+02:00"
  },
  "commentaireGlobal": null,
  "lignes": [
    {
      "idLigneSource": "2026-05-07|4006|4|333|341|1",
      "ordreArret": 1,
      "horaire": 1,
      "client": {
        "numClient": "333",
        "nomClient": "HOTEL LE MARTINET",
        "nomAffiche": "HOTEL LE MARTINET"
      },
      "pointLivraison": {
        "codePDL": "341",
        "descriptionPDL": "HOTEL LE MARTINET",
        "adresseLigne1": "PLACE DU GENERALE CHARRETTE",
        "adresseLigne2": null,
        "adresseLigne3": "SARL HOTEL LE MARTINET",
        "ville": "BOUIN",
        "codePostal": "85230"
      },
      "tournee": {
        "codeTournee": "4006",
        "libelleTournee": "BOUAYE",
        "jourTournee": 4,
        "jourLibelle": "Jeudi",
        "schemaLivraison": "1W1"
      },
      "retour": {
        "jourTourneeRetour": 4,
        "jourRetourLibelle": "Jeudi",
        "codeTourneeRetour": "4006",
        "libelleTourneeRetour": "BOUAYE"
      },
      "infosLivreur": {
        "instructions": "CODE 8578 *",
        "commentaireExceptionnel": null,
        "zoneDechargement": null,
        "zoneDechargementAffichee": "4",
        "zone": null,
        "precision": null,
        "cle": null,
        "estFerme": false,
        "dateFermeture": null,
        "motifFermeture": null
      },
      "saisie": {
        "precisionLivreur": "Test API v1.2",
        "statutPassage": "FAIT",
        "commentaireLivreur": null,
        "heureValidation": "2026-05-07T09:12:00+02:00",
        "estValidee": true,
        "quantites": [
          {
            "codeArticle": "ROLLS",
            "libelle": "Rolls",
            "quantiteLivreePrevue": null,
            "quantiteLivree": 1,
            "quantiteRecuperee": 2
          },
          {
            "codeArticle": "TAPIS",
            "libelle": "Tapis",
            "quantiteLivreePrevue": 0,
            "quantiteLivree": 0,
            "quantiteRecuperee": 0
          },
          {
            "codeArticle": "SACS",
            "libelle": "Sacs",
            "quantiteLivreePrevue": 2,
            "quantiteLivree": 3,
            "quantiteRecuperee": 1
          }
        ]
      }
    }
  ]
}
```

Réponse attendue :

```json
{
  "statut": "SUCCESS",
  "message": "Synchronisation enregistrée avec succès."
}
```

---

## 5. Réponses d’erreur documentées

### Validation métier

```json
{
  "statut": "VALIDATION_ERROR",
  "errors": [
    "Ligne 1, article ROLLS : QuantiteLivree ne peut pas être négative."
  ]
}
```

### Doublon technique

```json
{
  "statut": "CONFLICT",
  "code": "SYNCHRONISATION_ALREADY_EXISTS",
  "message": "Cette synchronisation a déjà été reçue."
}
```

### Doublon métier

```json
{
  "statut": "CONFLICT",
  "code": "TOURNEE_ALREADY_SENT",
  "message": "Cette tournée a déjà été envoyée pour ce livreur et cette date."
}
```

### Non trouvé

```json
{
  "statut": "NOT_FOUND",
  "message": "Livreur ou tournée introuvable."
}
```

---

## 6. Cas à documenter dans Swagger UI

| Cas | Résultat attendu |
|---|---|
| `schemaVersion = 1.2` | accepté |
| `schemaVersion = 1.0` | `400 VALIDATION_ERROR` |
| `quantiteLivreePrevue = null` | accepté, signifie non renseigné |
| `quantiteLivreePrevue = 0` | accepté, signifie zéro volontaire |
| `quantiteLivreePrevue < 0` | `400 VALIDATION_ERROR` |
| `quantiteLivree < 0` | `400 VALIDATION_ERROR` |
| `quantiteRecuperee < 0` | `400 VALIDATION_ERROR` |
| `NON_FAIT` sans commentaire | `400 VALIDATION_ERROR` |
| `ANOMALIE` sans commentaire | `400 VALIDATION_ERROR` |
| `A_FAIRE` dans le POST final | `400 VALIDATION_ERROR` |
| `estValidee = false` dans le POST final | `400 VALIDATION_ERROR` |
| `heureValidation = null` avec `estValidee = true` | `400 VALIDATION_ERROR` |
| même `idSynchronisation` | `409 SYNCHRONISATION_ALREADY_EXISTS` |
| même date + tournée + livreur | `409 TOURNEE_ALREADY_SENT` |

---

## 7. Points de vigilance avant production

Les routes suivantes sont utiles en développement mais doivent être désactivées, protégées ou réservées à un rôle administrateur avant production :

```text
/api/debug/sql/*
```

Les secrets de connexion ne doivent jamais être placés dans le dépôt Git. En développement, utiliser `dotnet user-secrets`. En déploiement IIS, utiliser une configuration serveur non versionnée, des variables d’environnement ou un coffre de secrets.
