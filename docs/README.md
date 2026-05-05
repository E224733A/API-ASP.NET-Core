# Documentation projet mobile tournée

Ce dossier contient la documentation de référence du projet API ASP.NET Core pour l’application mobile de tournée.

## État actuel du projet

Version fonctionnelle actuelle :

- API ASP.NET Core ;
- SQL Server ;
- lecture des données ABSSolute via des vues ;
- stockage des synchronisations mobiles dans les tables `Mobile_*` ;
- contrat JSON officiel `schemaVersion = "1.1"` ;
- quantités saisies via `saisie.quantites[]` ;
- validation métier centralisée dans `Validators/SynchronisationTourneeValidator.cs` ;
- constantes métier centralisées dans `Constants/` ;
- mapping du GET tournée isolé dans `Mappers/TourneeMobileMapper.cs` ;
- consultation admin typée via des DTOs dédiés ;
- tests métier documentés dans `docs/04-tests`.

## Structure du dossier

```text
docs/
├── 00-cadrage/
├── 01-api/
├── 02-base-donnees/
├── 03-deploiement/
├── 04-tests/
└── 05-codex/
```

## 00-cadrage

Contient le cahier des charges général.

Points importants à conserver :

- le livreur s’identifie par code livreur ;
- la date du jour est non modifiable côté mobile ;
- le livreur choisit uniquement sa tournée ;
- une tournée envoyée est verrouillée côté mobile ;
- seule une correction côté administration pourra être envisagée après envoi ;
- l’application mobile travaille hors connexion pendant la tournée ;
- la synchronisation est faite au dépôt, via le réseau interne.

## 01-api

Contient la documentation technique de l’API.

État actuel :

- `GET /api/tournees/jour` charge une tournée réelle depuis ABSSolute ;
- `POST /api/synchronisations` enregistre l’envoi final du mobile ;
- `GET /api/synchronisations` liste les synchronisations envoyées ;
- `GET /api/synchronisations/{idTourneeMobile}` consulte le détail d’un envoi.

Le contrat officiel est le contrat JSON `1.1`.

## 02-base-donnees

Contient la documentation liée à SQL Server.

Tables mobiles principales :

- `Mobile_Livreur` ;
- `Mobile_ChargementTournee` ;
- `Mobile_Tournee` ;
- `Mobile_TourneeLigne` ;
- `Mobile_TourneeLigneQuantite` ;
- `Mobile_LogSynchronisation` ;
- `Mobile_ExportAdmin`.

Les vues ABSSolute restent en lecture seule.

## 03-deploiement

Contient les notes de déploiement.

Objectif cible :

- API hébergée sur une VM Windows ;
- déploiement possible sous IIS ;
- accès depuis les téléphones Android via le Wi-Fi du dépôt ;
- SQL Server accessible depuis la VM API ;
- pas d’accès direct du mobile à SQL Server.

## 04-tests

Contient les fichiers JSON de test et le README des scénarios.

Ces tests couvrent :

- chargement réel d’une tournée ;
- envoi valide ;
- validations métier ;
- anti-doublon technique ;
- anti-doublon métier ;
- consultation admin.

## 05-codex

Contient les consignes pour les outils d’assistance au code.

Objectif :

- éviter de réintroduire l’ancien contrat JSON ;
- éviter les modifications incohérentes ;
- garder l’architecture propre.
