# Documentation projet mobile tournée

Ce dossier contient la documentation de référence du projet API ASP.NET Core pour l’application mobile de tournée SLI.

## État actuel du projet

Version fonctionnelle actuelle :

- API ASP.NET Core ;
- SQL Server ;
- lecture des données ABSSolute via des vues SQL ;
- aucune modification directe des tables internes ABSSolute ;
- stockage des chargements, synchronisations, quantités, logs et pré-remplissages dans les tables `Mobile_*` ;
- contrat JSON officiel `schemaVersion = "1.2"` ;
- quantités saisies via `saisie.quantites[]` ;
- prise en charge de `quantiteLivreePrevue`, `quantiteLivree` et `quantiteRecuperee` ;
- validation métier centralisée dans `Validators/SynchronisationTourneeValidator.cs` ;
- mapping du chargement de tournée isolé dans `Mappers/TourneeMobileMapper.cs` ;
- accès SQL isolé dans les repositories ;
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

Contient le cadrage fonctionnel du projet.

Points importants :

- le livreur s’identifie avec son code livreur ;
- la date de tournée est affichée mais non modifiable côté mobile ;
- le livreur choisit une tournée disponible ;
- le mobile charge la tournée le matin au dépôt ;
- le livreur travaille hors connexion pendant la journée ;
- la synchronisation est faite au dépôt en fin de journée ;
- une tournée envoyée avec succès est verrouillée côté mobile ;
- une correction après envoi doit passer par une future interface d’administration.

## 01-api

Contient la documentation technique de l’API.

Routes principales :

```text
GET  /api/health
GET  /api/health/abssolute
GET  /api/health/mobile
GET  /api/livreurs
GET  /api/tournees/disponibles
GET  /api/tournees/jour
POST /api/synchronisations
GET  /api/synchronisations
GET  /api/synchronisations/{idTourneeMobile}
```

Contrat officiel :

```text
schemaVersion = "1.2"
```

Le format des quantités est extensible :

```json
"quantites": [
  {
    "codeArticle": "ROLLS",
    "libelle": "Rolls",
    "quantiteLivreePrevue": null,
    "quantiteLivree": 1,
    "quantiteRecuperee": 2
  }
]
```

Règle importante :

```text
quantiteLivreePrevue = null -> l’expédition n’a rien renseigné
quantiteLivreePrevue = 0    -> l’expédition a volontairement prévu zéro
quantiteLivreePrevue > 0    -> l’expédition a prévu une quantité
```

## 02-base-donnees

Contient la documentation SQL Server.

Tables principales :

- `Mobile_Livreur` ;
- `Mobile_ChargementTournee` ;
- `Mobile_Tournee` ;
- `Mobile_TourneeLigne` ;
- `Mobile_TourneeLigneQuantite` ;
- `Mobile_ArticleSaisissable` ;
- `Mobile_PreRemplissageTournee` ;
- `Mobile_PreRemplissageQuantite` ;
- `Mobile_PreRemplissageHistorique` ;
- `Mobile_CommentaireExceptionnel` ;
- `Mobile_LogSynchronisation` ;
- `Mobile_ExportAdmin`.

Les vues ABSSolute restent en lecture seule.

La source principale des quantités est :

```text
Mobile_TourneeLigneQuantite
```

Les anciennes colonnes de compatibilité comme `NbRolls`, `NbTapis`, `NbSacs` et `NbRecuperes` ne doivent pas redevenir la source principale.

## 03-deploiement

Contient les notes de déploiement.

Cible recommandée :

- VM Windows ;
- hébergement IIS ;
- API ASP.NET Core publiée en `Release` ;
- accès depuis les téléphones Android via le Wi-Fi du dépôt ;
- SQL Server accessible uniquement depuis le serveur, la VM ou le réseau interne autorisé ;
- secrets configurés hors Git.

Principe à conserver :

```text
Téléphone Android
-> API ASP.NET Core
-> SQL Server
   ├── vues ABSSolute en lecture
   └── tables Mobile_* en écriture
```

## 04-tests

Contient les fichiers JSON de test et les scénarios de validation.

Les tests couvrent :

- chargement réel d’une tournée ;
- envoi valide ;
- validations métier ;
- anti-doublon technique ;
- anti-doublon métier ;
- consultation admin ;
- tests de concurrence.

Résultats validés :

```text
200 OK / SUCCESS
400 Bad Request / VALIDATION_ERROR
404 Not Found
409 Conflict / CONFLICT
```

## 05-codex

Contient les règles destinées aux assistants de code.

Objectif :

- éviter de réintroduire l’ancien contrat JSON ;
- conserver `schemaVersion = "1.2"` ;
- conserver `saisie.quantites[]` ;
- conserver `quantiteLivreePrevue` ;
- garder les contrôleurs simples ;
- conserver la validation dans le validateur ;
- conserver les accès SQL dans les repositories ;
- éviter les modifications incohérentes dans le modèle SQL ou JSON.

## À retenir

Le projet repose sur trois principes :

```text
1. Le mobile ne se connecte jamais directement à SQL Server.
2. L’API centralise les échanges, les règles métier et les validations.
3. Les données terrain sont stockées dans les tables Mobile_* sans modifier ABSSolute.
```
