# Documentation API - Projet MobileSLI

Ce dossier documente l'API ASP.NET Core du projet MobileSLI.

L'API est le point d'entrée unique entre :

- l'application mobile Android utilisée par les livreurs ;
- l'application Web Expédition ;
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
| `03-deploiement` | Lancement local, tests téléphone, ngrok, ADB, IIS |
| `04-tests` | Jeux JSON, scripts de test et procédures de validation |
| `05-codex` | Consignes de reprise pour un assistant de développement |

## Vérité actuelle du projet

```text
API ASP.NET Core = point d'entrée unique
Mobile = GET tournée + POST synchronisation
Web Expédition = GET global + POST verrouillage
GET Expédition = aucun filtre accepté
POST Expédition = idempotent avec idLotVerrouillage
Mobile = lit uniquement les préparations verrouillées en SQL Server
Anti-doublon mobile = DateTournee + CodeTournee
Articles Expédition = ROLLS, TAPIS, SACS
ROLLS_VIDES = interdit côté Expédition
schemaVersion = 1.2
```

## Routes principales

| Module | Méthode | Route | Rôle |
|---|---:|---|---|
| Santé | GET | `/api/health` | Vérifier que l'API répond |
| Santé SQL ABSSolute | GET | `/api/health/abssolute` | Vérifier l'accès aux vues ABSSolute |
| Santé SQL Mobile | GET | `/api/health/mobile` | Vérifier l'accès aux tables `Mobile_*` |
| Livreurs | GET | `/api/livreurs` | Lister les livreurs connus |
| Mobile | GET | `/api/tournees/disponibles` | Lister les tournées disponibles pour une date et un livreur |
| Mobile | GET | `/api/tournees/jour` | Charger une tournée complète |
| Mobile | POST | `/api/synchronisations` | Envoyer le retour mobile |
| Mobile | GET | `/api/synchronisations` | Consulter les synchronisations reçues |
| Expédition | GET | `/api/expedition/preparations/a-preparer` | Charger toutes les préparations à préparer |
| Expédition | POST | `/api/expedition/preparations/verrouiller` | Verrouiller les préparations Expédition |

## Lecture conseillée

1. Lire `00-cadrage/decisions-techniques.md`.
2. Lire `01-api/README.md`.
3. Lire `01-api/routes-expedition.md` et `01-api/routes-mobile.md`.
4. Lire `01-api/contrats-json-expedition.md` et `01-api/contrats-json-mobile.md`.
5. Lire `02-base-donnees/README.md`.
6. Lire `03-deploiement/lancement-local.md`.
7. Lancer les tests dans `04-tests`.
