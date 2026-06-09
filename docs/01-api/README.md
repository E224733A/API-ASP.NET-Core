# 01 - Documentation API

Ce dossier documente l'API ASP.NET Core du projet MobileSLI.

L'API est le point d'entrée unique entre :

- l'application mobile livreur ;
- l'application Web Expédition ;
- SQL Server ;
- les vues ABSSolute en lecture seule ;
- les tables `Mobile_*` dédiées au projet.

## Contrats mobiles actuels

```text
GET /api/tournees/disponibles -> schemaVersion 1.2.
GET /api/tournees/jour -> schemaVersion 1.2.
GET /api/camions/disponibles -> schemaVersion 1.3.
POST /api/synchronisations -> schemaVersion 1.3 uniquement.
```

Le contrat de synchronisation mobile est strict :

```text
schemaVersion = "1.3" uniquement.
schemaVersion = "1.2" refusé sur POST /api/synchronisations.
trajet obligatoire.
camion obligatoire.
idCamion obligatoire.
kilométrages départ et arrivée obligatoires.
dates départ et arrivée obligatoires.
kilometrageArrivee doit être supérieur ou égal à kilometrageDepart.
dateArriveeMobile doit être supérieure ou égale à dateDepartMobile.
```

Le contrat de chargement du matin reste en `schemaVersion = "1.2"`. Cette version 1.2 ne doit pas être envoyée au POST final de synchronisation.

## Routes principales

| Module | Méthode | Route | Rôle |
|---|---:|---|---|
| Santé | GET | `/api/health` | Vérifier que l'API répond |
| Santé SQL ABSSolute | GET | `/api/health/abssolute` | Vérifier l'accès aux vues ABSSolute |
| Santé SQL Mobile | GET | `/api/health/mobile` | Vérifier l'accès aux tables `Mobile_*` |
| Livreurs | GET | `/api/livreurs` | Lister les livreurs |
| Mobile | GET | `/api/tournees/disponibles` | Lister les tournées disponibles |
| Mobile | GET | `/api/tournees/jour` | Charger une tournée complète |
| Mobile | GET | `/api/camions/disponibles` | Lister les camions disponibles |
| Mobile | POST | `/api/synchronisations` | Synchroniser le retour mobile strict 1.3 avec trajet camion |
| Expédition | GET | `/api/expedition/preparations/a-preparer` | Charger toutes les préparations à préparer |
| Expédition | POST | `/api/expedition/preparations/verrouiller` | Verrouiller les préparations Expédition |

## Documents

| Fichier | Rôle |
|---|---|
| `routes-mobile.md` | Routes utilisées par l'application mobile |
| `routes-expedition.md` | Routes utilisées par l'application Web Expédition |
| `contrats-json-mobile.md` | Contrats JSON mobile |
| `contrats-json-expedition.md` | Contrats JSON Expédition |
| `erreurs-api.md` | Codes HTTP et erreurs métier |
| `swagger.md` | Utilisation de Swagger |

## Architecture logique

```text
Controllers : HealthController, LivreursController, TourneesController, CamionsController, SynchronisationsController, ExpeditionPreparationsController.
Services : TourneesService, CamionsService, SynchronisationService, ExpeditionService, ExpeditionPreparationService, ExpeditionVerrouillageService.
Repositories : LivreursRepository, TourneesRepository, CamionsRepository, SynchronisationsRepository, ExpeditionRepository.
Mappers : TourneeMobileMapper, SynchronisationMapper, ExpeditionMapper.
Validators : TourneeRequestValidator, SynchronisationTourneeValidator, ExpeditionVerrouillageValidator.
```

## Règles majeures

```text
Le GET Expédition est global.
Le POST Expédition est idempotent avec idLotVerrouillage.
Le mobile lit uniquement les préparations verrouillées.
L'anti-doublon mobile technique est IdSynchronisation.
L'anti-doublon mobile métier est DateTournee + CodeTournee.
Le POST /api/synchronisations exige schemaVersion = "1.3".
Le POST /api/synchronisations exige un trajet camion complet.
Le trajet camion est sauvegardé avec la synchronisation mobile.
Les GET mobiles refusent les paramètres date et dateTournee.
La date métier est calculée côté API avec le fuseau Europe/Paris.
```