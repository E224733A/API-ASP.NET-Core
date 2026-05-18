# 02 - Documentation base de données

La base SQL Server contient :

- les vues ABSSolute utilisées en lecture seule ;
- les tables `Mobile_*` dédiées au projet MobileSLI.

L'API lit les vues ABSSolute mais ne modifie jamais les tables internes ABSSolute.

Les données produites par le projet sont stockées dans les tables `Mobile_*`.

## Flux général

```text
ABSSolute / vues SQL -> API ASP.NET Core -> Application mobile
Application mobile -> API ASP.NET Core -> Tables Mobile_*
Application Web Expédition -> API ASP.NET Core -> Tables Mobile_*
```

## Documents

| Fichier | Rôle |
|---|---|
| `schema-mobile.md` | Tables principales côté mobile |
| `expedition-sql.md` | Tables liées au module Expédition |
| `migrations.md` | Règles d'organisation des scripts SQL |

## Règle importante

SQLite côté Web Expédition sert uniquement au brouillon local avant verrouillage.

SQL Server conserve uniquement les préparations verrouillées et officielles.

Le mobile ne lit jamais les brouillons SQLite.
