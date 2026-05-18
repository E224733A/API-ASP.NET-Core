# 01 - Documentation API

Ce dossier documente l'API ASP.NET Core du projet MobileSLI.

L'API est le point d'entrée unique entre :

- l'application mobile livreur ;
- l'application Web Expédition ;
- SQL Server ;
- les vues ABSSolute en lecture seule ;
- les tables `Mobile_*` dédiées au projet.

## Version de contrat

Version actuelle :

```json
{
  "schemaVersion": "1.2"
}
```

## Routes principales

| Module | Méthode | Route | Rôle |
|---|---:|---|---|
| Santé | GET | `/api/health` | Vérifier que l'API répond |
| Santé SQL ABSSolute | GET | `/api/health/abssolute` | Vérifier l'accès aux vues ABSSolute |
| Santé SQL Mobile | GET | `/api/health/mobile` | Vérifier l'accès aux tables `Mobile_*` |
| Livreurs | GET | `/api/livreurs` | Lister les livreurs |
| Mobile | GET | `/api/tournees/disponibles` | Lister les tournées disponibles |
| Mobile | GET | `/api/tournees/jour` | Charger une tournée complète |
| Mobile | POST | `/api/synchronisations` | Synchroniser le retour mobile |
| Mobile | GET | `/api/synchronisations` | Consulter les synchronisations reçues |
| Mobile | GET | `/api/synchronisations/{idTourneeMobile}` | Consulter le détail d'une synchronisation |
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
| `changelog.md` | Historique des décisions API |

## Architecture logique

```text
Controllers/
├── HealthController.cs
├── LivreursController.cs
├── TourneesController.cs
├── SynchronisationsController.cs
├── ExpeditionPreparationsController.cs
└── DebugSqlController.cs

Services/
├── TourneesService.cs
├── SynchronisationService.cs
└── ExpeditionService.cs

Repositories/
├── LivreursRepository.cs
├── TourneesRepository.cs
├── SynchronisationsRepository.cs
└── ExpeditionRepository.cs

Mappers/
└── TourneeMobileMapper.cs

Validators/
└── SynchronisationTourneeValidator.cs
```

## Règles majeures

```text
Le mobile ne se connecte jamais à SQL Server.
Le Web Expédition ne filtre pas le GET côté API.
Le GET Expédition est global.
Le POST Expédition est idempotent avec idLotVerrouillage.
Le mobile lit uniquement les préparations verrouillées.
L'anti-doublon mobile est DateTournee + CodeTournee.
```
