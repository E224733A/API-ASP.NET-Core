# Documentation API - Projet MobileSLI

Ce dossier documente l'API ASP.NET Core du projet MobileSLI.

L'API est le point d'entrée unique entre :

- l'application mobile Android utilisée par les livreurs ;
- l'application Web Expédition / Administration ;
- SQL Server ;
- les vues ABSSolute utilisées en lecture seule ;
- les tables `Mobile_*` dédiées au projet.

Le mobile et le Web Expédition ne se connectent jamais directement à SQL Server.

## Organisation

| Dossier | Rôle |
|---|---|
| `00-cadrage` | Décisions fonctionnelles et techniques globales |
| `01-api` | Routes, contrats JSON, erreurs API et Swagger |
| `02-base-donnees` | Tables, vues SQL, contraintes et migrations |
| `03-deploiement` | Lancement local, tests téléphone, IIS et HTTPS API |
| `04-tests` | Jeux JSON, scripts de test et procédures de validation |
| `05-codex` | Consignes de reprise pour un assistant de développement |

## Vérité actuelle du projet

```text
API ASP.NET Core = point d'entrée unique
URL API production cible = https://srvapi1.sli.local
Mobile = GET tournée + GET camions + POST synchronisation
Web Expédition = GET global + POST verrouillage
GET Expédition = aucun filtre accepté
POST Expédition = idempotent avec idLotVerrouillage
Mobile = lit uniquement les préparations verrouillées en SQL Server
Anti-doublon mobile technique = IdSynchronisation
Anti-doublon mobile métier = DateTournee + CodeTournee
Articles Expédition = ROLLS, ROLLS_VIDES, TAPIS, SACS
ROLLS_VIDES = autorisé côté Expédition et côté retour mobile
Contrat GET tournée mobile = schemaVersion 1.2
Contrat GET camions mobile = schemaVersion 1.3
Contrat POST synchronisation mobile = schemaVersion 1.3 uniquement
Contrat Expédition GET/POST = schemaVersion 1.2
schemaVersion 1.2 = refusé uniquement sur POST /api/synchronisations mobile
Trajet camion = obligatoire sur POST /api/synchronisations
Trajet camion = sauvegardé dans dbo.Mobile_TourneeCamion
```

## Séparation des contrats

```text
Contrat mobile de chargement : GET /api/tournees/jour -> schemaVersion 1.2.
Contrat mobile camions : GET /api/camions/disponibles -> schemaVersion 1.3.
Contrat mobile de synchronisation : POST /api/synchronisations -> schemaVersion 1.3 strict.
Contrat Expédition : GET /api/expedition/preparations/a-preparer et POST /api/expedition/preparations/verrouiller -> schemaVersion 1.2.
```

Le passage du contrat mobile de synchronisation en `1.3` ne modifie pas le contrat Expédition.

## Routes principales

| Module | Méthode | Route | Rôle |
|---|---:|---|---|
| Santé | GET | `/api/health` | Vérifier que l'API répond |
| Santé SQL ABSSolute | GET | `/api/health/abssolute` | Vérifier l'accès aux vues ABSSolute |
| Santé SQL Mobile | GET | `/api/health/mobile` | Vérifier l'accès aux tables `Mobile_*` |
| Livreurs | GET | `/api/livreurs` | Lister les livreurs connus |
| Mobile | GET | `/api/tournees/disponibles` | Lister les tournées disponibles pour la date serveur autorisée et un livreur |
| Mobile | GET | `/api/tournees/jour` | Charger une tournée complète |
| Mobile | GET | `/api/camions/disponibles` | Lister les camions disponibles pour le choix camion |
| Mobile | POST | `/api/synchronisations` | Envoyer le retour mobile strict 1.3 avec trajet camion |
| Mobile | GET | `/api/synchronisations` | Consulter les synchronisations reçues si la route est activée dans le code courant |
| Expédition | GET | `/api/expedition/preparations/a-preparer` | Charger toutes les préparations à préparer |
| Expédition | POST | `/api/expedition/preparations/verrouiller` | Verrouiller les préparations Expédition |

## Validation connue

Dernières validations communiquées par exécution locale/utilisateur :

```text
Build Release API : réussi d'après retour utilisateur.
Tag API final : réalisé d'après retour utilisateur.
Tests API Mobile stricts 1.3 : 31/31 OK sur https://srvapi1.sli.local.
GET /api/camions/disponibles : HTTP 200, schemaVersion = "1.3", camions[] retourné.
POST /api/synchronisations : schemaVersion 1.3 accepté avec trajet camion complet.
POST /api/synchronisations : schemaVersion 1.2 refusé.
```

Les commandes exactes doivent être relancées après toute modification de code ou de configuration.

## Lecture minimum conseillée

1. Lire `00-cadrage/decisions-techniques.md`.
2. Lire `01-api/README.md`.
3. Lire `01-api/routes-expedition.md` et `01-api/routes-mobile.md`.
4. Lire `01-api/contrats-json-expedition.md` et `01-api/contrats-json-mobile.md`.
5. Lire `02-base-donnees/README.md`.
6. Lire `03-deploiement/lancement-local.md`.
7. Lancer les tests dans `04-tests`.