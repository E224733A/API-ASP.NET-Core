# Flux global du projet

## Vue d'ensemble

```text
ABSSolute / vues SQL
        |
        v
API ASP.NET Core
        |
        +--> MobileSLI
        |
        +--> Web Expédition
        |
        +--> Tables Mobile_*
```

## Flux Expédition

```text
1. Le Web Expédition appelle GET /api/expedition/preparations/a-preparer.
2. L'API calcule la date préparable.
3. L'API lit les lignes préparables depuis les données métier.
4. L'API retourne toutes les tournées préparables.
5. Le Web Expédition laisse l'utilisateur choisir une tournée côté interface.
6. Les modifications restent en brouillon dans SQLite local côté Web Expédition.
7. À 00:05, le backend Web Expédition envoie un lot global à l'API.
8. L'API valide le lot.
9. L'API écrit les préparations verrouillées dans SQL Server.
10. Les données verrouillées deviennent disponibles pour le mobile.
```

## Flux mobile

```text
1. Le livreur est au dépôt.
2. Le mobile teste la connexion à l'API.
3. Le mobile charge la tournée.
4. L'API retourne les données de tournée et les quantités prévues verrouillées.
5. Le mobile stocke localement les données.
6. Le livreur travaille hors connexion pendant la journée.
7. Au retour dépôt, le mobile envoie le POST /api/synchronisations.
8. L'API valide les données.
9. L'API enregistre la tournée, les lignes, les quantités et les logs.
10. La tournée devient envoyée et ne peut plus être renvoyée pour la même date et le même code tournée.
```

## Règle critique

Une préparation Expédition encore en brouillon dans SQLite ne doit jamais être visible côté mobile.

Seules les préparations verrouillées en SQL Server peuvent alimenter le chargement mobile.
