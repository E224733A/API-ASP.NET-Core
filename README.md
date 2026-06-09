# API ASP.NET Core - MobileSLI

API centrale du projet MobileSLI.

Elle sert de point d'entrée unique entre :

- l'application mobile Android utilisée par les livreurs ;
- l'application Web Expédition / Administration ;
- SQL Server ;
- les vues ABSSolute utilisées en lecture seule ;
- les tables `Mobile_*` dédiées au projet.

Le mobile et le ServeWeb ne doivent pas accéder directement à SQL Server.

## Etat validé

```text
Version fonctionnelle API : contrat mobile strict 1.3
URL HTTPS cible           : https://srvapi1.sli.local
GET mobile chargement     : schemaVersion 1.2
GET camions disponibles   : schemaVersion 1.3
POST synchronisation      : schemaVersion 1.3 uniquement
schemaVersion 1.2 POST    : refusé
Trajet camion POST        : obligatoire
```

Validation communiquée le 09/06/2026 :

```text
git status API                 : clean
branche main                   : synchronisée avec origin/main
Tests API Mobile HTTPS stricts : 31/31 OK
Base testée                    : https://srvapi1.sli.local
Tag API réalisé                : oui, d'après retour utilisateur
```

Les tests n'ont pas été relancés par cette mise à jour documentaire.

## Préconditions

Avant de lancer ou déployer l'API, vérifier que :

```text
.NET SDK installé
SQL Server accessible
vues ABSSolute accessibles
tables Mobile_* présentes
chaîne de connexion configurée
aucun secret stocké dans Git
certificat HTTPS installé sur IIS en production
```

Vérifier .NET :

```powershell
dotnet --info
```

## Lancer l'API en développement local

Le développement local reste en HTTP sur `127.0.0.1:5000`.

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\API\API-ASP.NET-Core"

$env:ASPNETCORE_ENVIRONMENT="Development"

dotnet restore
dotnet build
dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

Résultat attendu :

```text
Now listening on: http://127.0.0.1:5000
Application started.
```

## Vérifier l'API locale

Dans un deuxième terminal :

```powershell
Invoke-RestMethod "http://127.0.0.1:5000/api/health"
Invoke-RestMethod "http://127.0.0.1:5000/api/health/abssolute"
Invoke-RestMethod "http://127.0.0.1:5000/api/health/mobile"
```

Résultat attendu : HTTP 200.

## Vérifier l'API production HTTPS

Sur un poste ou serveur qui résout `srvapi1.sli.local` :

```powershell
Invoke-WebRequest "https://srvapi1.sli.local/api/health" -UseBasicParsing
```

Résultat attendu :

```text
StatusCode : 200
```

Le fallback HTTP historique peut exister pour maintenance selon configuration IIS, mais l'URL cible validée pour le mobile et ServeWeb est :

```text
https://srvapi1.sli.local
```

## Tests API Mobile stricts 1.3

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force

.\docs\04-tests\Mobile\scripts\run-api-mobile-tests.ps1 `
  -ApiBaseUrl "https://srvapi1.sli.local" `
  -DateTournee "2026-06-09"
```

Résultat attendu :

```text
Total   : 31
OK      : 31
KO      : 0
SKIPPED : 0
```

Adapter `DateTournee` à la date métier autorisée par l'API au moment du test.

## Routes principales

| Module | Méthode | Route | Rôle |
|---|---:|---|---|
| Santé | GET | `/api/health` | Vérifier que l'API répond |
| Santé SQL ABSSolute | GET | `/api/health/abssolute` | Vérifier l'accès aux vues ABSSolute |
| Santé SQL Mobile | GET | `/api/health/mobile` | Vérifier l'accès aux tables `Mobile_*` |
| Livreurs | GET | `/api/livreurs` | Lister les livreurs |
| Mobile | GET | `/api/tournees/disponibles` | Lister les tournées disponibles pour la date serveur autorisée |
| Mobile | GET | `/api/tournees/jour` | Charger une tournée complète en schemaVersion 1.2 |
| Mobile | GET | `/api/camions/disponibles` | Lister les camions disponibles en schemaVersion 1.3 |
| Mobile | POST | `/api/synchronisations` | Envoyer le retour mobile strict 1.3 avec trajet camion |
| Expédition | GET | `/api/expedition/preparations/a-preparer` | Charger les préparations Expédition à préparer |
| Expédition | POST | `/api/expedition/preparations/verrouiller` | Verrouiller les préparations Expédition |

## Commandes de maintenance courantes

```powershell
dotnet restore
dotnet clean
dotnet build
dotnet build -c Release
dotnet publish -c Release -o ".\publish"
```

## Déploiement IIS

Le script de mise à jour applicative est :

```text
docs/update-api-iis.ps1
```

Ce script ne crée pas la configuration HTTPS. Il attend une configuration IIS déjà prête avec :

```text
Site IIS API      : MobileSLI.Api
Binding HTTPS     : https *:443:srvapi1.sli.local
Health HTTPS      : https://srvapi1.sli.local/api/health
Fallback HTTP     : http://srvapi1.sli.local:5000/api/health si conservé
```

## Documentation utile

| Fichier | Rôle |
|---|---|
| `docs/README.md` | Sommaire documentaire API |
| `docs/01-api/README.md` | Vue d'ensemble des routes et contrats |
| `docs/01-api/routes-mobile.md` | Routes utilisées par l'application mobile |
| `docs/01-api/contrats-json-mobile.md` | Contrats JSON mobile |
| `docs/01-api/routes-expedition.md` | Routes utilisées par ServeWeb Expédition |
| `docs/01-api/contrats-json-expedition.md` | Contrats JSON Expédition |
| `docs/01-api/erreurs-api.md` | Codes HTTP et erreurs |
| `docs/02-base-donnees/README.md` | Stockage SQL et tables Mobile_* |
| `docs/04-tests/Mobile/matrice-tests-mobile.md` | Matrice des tests API Mobile |

## Règles à conserver

```text
Le mobile ne se connecte jamais directement à SQL Server.
ServeWeb ne se connecte jamais directement à SQL Server.
La date métier est calculée côté API avec le fuseau Europe/Paris.
Les GET mobiles refusent les paramètres date et dateTournee.
POST /api/synchronisations accepte uniquement schemaVersion 1.3.
Le trajet camion est obligatoire en synchronisation 1.3.
Les données trajet sont sauvegardées dans dbo.Mobile_TourneeCamion.
```