# 00 - Cadrage

Ce dossier rassemble les décisions de cadrage du projet.

Il ne contient pas les commandes de lancement, les scripts SQL détaillés ou les jeux de tests. Ces éléments sont placés dans les dossiers spécialisés.

## Documents

| Fichier | Rôle |
|---|---|
| `decisions-techniques.md` | Décisions structurantes du projet |
| `flux-global.md` | Flux métier entre Web Expédition, API, SQL Server et mobile |

## Résumé

Le projet vise à dématérialiser la fiche de tournée.

L'API ASP.NET Core centralise les accès à SQL Server.

Le mobile fonctionne hors connexion pendant la tournée.

Le Web Expédition prépare les quantités prévues avant le départ.

Les données officielles sont celles validées par l'API et enregistrées en SQL Server.
