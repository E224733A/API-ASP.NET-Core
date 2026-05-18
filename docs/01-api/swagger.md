# Swagger - API MobileSLI

Swagger permet de consulter et tester les routes exposées par l'API ASP.NET Core.

La documentation Swagger doit être alignée avec les fichiers du dossier `01-api`.

## Accès local

Lancer l'API :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"

$env:ASPNETCORE_ENVIRONMENT="Development"

dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

Ouvrir Swagger :

```text
http://127.0.0.1:5000/swagger
```

## Routes à afficher dans Swagger

| Méthode | Route | Rôle |
|---|---|---|
| GET | `/api/health` | Vérifier que l'API répond |
| GET | `/api/health/abssolute` | Vérifier l'accès aux vues ABSSolute |
| GET | `/api/health/mobile` | Vérifier l'accès aux tables `Mobile_*` |
| GET | `/api/livreurs` | Lister les livreurs |
| GET | `/api/tournees/disponibles` | Lister les tournées disponibles |
| GET | `/api/tournees/jour` | Charger une tournée complète |
| POST | `/api/synchronisations` | Enregistrer le retour mobile |
| GET | `/api/synchronisations` | Consulter les synchronisations reçues |
| GET | `/api/synchronisations/{idTourneeMobile}` | Consulter le détail d'une synchronisation |
| GET | `/api/expedition/preparations/a-preparer` | Charger toutes les préparations Expédition |
| POST | `/api/expedition/preparations/verrouiller` | Verrouiller un lot global de préparations Expédition |
| GET | `/api/debug/sql/*` | Routes de debug SQL en développement |

## Tester les routes Expédition depuis Swagger

### GET /api/expedition/preparations/a-preparer

Ne renseigner aucun paramètre.

Résultat attendu :

```http
200 OK
```

La réponse doit contenir :

```text
schemaVersion = 1.2
dateTournee
datePreparable
fuseauHoraireMetier = Europe/Paris
articlesPreparables[]
tournees[]
regles
```

### Test d'erreur attendu

Tester volontairement :

```http
GET /api/expedition/preparations/a-preparer?dateTournee=2026-05-19
```

Résultat attendu :

```http
400 Bad Request
```

Code attendu :

```text
EXPEDITION_GET_QUERY_PARAMS_FORBIDDEN
```

### POST /api/expedition/preparations/verrouiller

Utiliser un JSON du dossier :

```text
docs/04-tests/Expedition/json/
```

Champs obligatoires :

```text
schemaVersion = 1.2
idLotVerrouillage
source = APPLICATION_WEB_EXPEDITION
dateTournee
dateVerrouillageDemandee
fuseauHoraireMetier = Europe/Paris
tournees[]
```

## Points à améliorer ensuite dans Swagger

La prochaine étape de documentation consiste à améliorer directement Swagger dans le code :

```text
commentaires XML des contrôleurs
exemples JSON réalistes
descriptions des DTO
codes de réponse
règles métier visibles dans Swagger UI
```

## Routes de debug SQL

Les routes `/api/debug/sql/*` sont utiles en développement.

Elles doivent être désactivées, protégées ou réservées à un rôle administrateur avant une mise en production réelle.
