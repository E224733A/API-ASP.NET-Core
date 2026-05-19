# 02 - Documentation base de données

La base SQL Server contient :

- les vues ABSSolute utilisées en lecture seule ;
- les tables `Mobile_*` dédiées au projet MobileSLI ;
- les tables de préparation Expédition officiellement verrouillées.

L'API lit les vues ABSSolute mais ne modifie jamais les tables internes ABSSolute.

Les données produites par le projet sont stockées uniquement dans les tables `Mobile_*`.

## Décision retenue pour Expédition

Le projet retient l'option A :

```text
Brouillons Expédition -> SQLite local côté serveur web Expédition
Préparations verrouillées -> SQL Server via API
```

Conséquence importante :

```text
SQL Server ne stocke pas les brouillons Expédition.
SQL Server ne conserve que les préparations verrouillées et officielles.
Le mobile ne lit jamais SQLite.
```

Cette décision permet de conserver seulement deux routes côté API Expédition :

```text
GET  /api/expedition/preparations/a-preparer
POST /api/expedition/preparations/verrouiller
```

## Flux général

```text
ABSSolute / vues SQL -> API ASP.NET Core -> Application mobile
Application mobile -> API ASP.NET Core -> Tables Mobile_*
Application Web Expédition -> SQLite local pour les brouillons
Application Web Expédition -> API ASP.NET Core -> Tables Mobile_Expedition*
```

## Séparation des responsabilités

| Zone | Rôle |
|---|---|
| Vues ABSSolute | Source métier en lecture seule |
| Tables `Mobile_Tournee*` | Données envoyées par le mobile en fin de tournée |
| Tables `Mobile_Expedition*` | Données verrouillées par le module Expédition |
| `Mobile_ArticleSaisissable` | Référentiel commun des articles suivis |
| `Mobile_CommentaireExceptionnel` | Commentaires ponctuels lus par le mobile |
| `Mobile_LogSynchronisation` | Audit technique et métier |

## Documents

| Fichier | Rôle |
|---|---|
| `schema-mobile.md` | Tables principales côté mobile |
| `expedition-sql.md` | Tables liées au module Expédition |
| `migrations.md` | Règles d'organisation des scripts SQL |
| `complete/BDD_sli_v13_complete.sql` | Script complet de recréation en développement/test |

## Règles importantes

Le mobile ne se connecte jamais directement à SQL Server.

Le serveur web Expédition ne se connecte jamais directement à SQL Server.

L'API ASP.NET Core est le seul point d'entrée vers SQL Server pour le mobile et pour le module Expédition.

Les vues et tables internes ABSSolute ne doivent pas être modifiées par le projet.

Une préparation Expédition non verrouillée ne doit jamais alimenter `GET /api/tournees/jour`.

## Ordre de validation recommandé

Après exécution du script complet :

```text
1. Vérifier les tables Mobile_* créées.
2. Vérifier les vues de suivi créées.
3. Lancer l'API.
4. Tester GET /api/health.
5. Tester GET /api/expedition/preparations/a-preparer.
6. Tester POST /api/expedition/preparations/verrouiller.
7. Vérifier les tables Mobile_Expedition*.
8. Tester GET /api/tournees/jour.
9. Vérifier que quantiteLivreePrevue remonte côté mobile.
10. Tester POST /api/synchronisations.
```
