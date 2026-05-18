# Migrations SQL

## Principe

Les scripts SQL doivent être organisés en deux catégories :

```text
scripts complets
scripts de migration
```

## Script complet

Rôle : recréer toute la base dédiée au projet en développement ou en test.

Exemple :

```text
BDD_sli_v13_complete.sql
```

Attention : un script complet peut être destructif s'il contient des `DROP TABLE`.

Il ne doit pas être exécuté sur une base contenant des données à conserver sans sauvegarde préalable.

## Script de migration

Rôle : faire évoluer une base existante sans tout supprimer.

Exemples :

```text
2026-05-15_expedition_verrouillage_lot.sql
2026-05-18_update_antidoublon_mobile.sql
```

## Organisation recommandée

```text
docs/02-base-donnees/
├── README.md
├── schema-mobile.md
├── expedition-sql.md
├── migrations.md
├── complete/
│   └── BDD_sli_v13_complete.sql
└── migrations/
    └── 2026-05-15_expedition_verrouillage_lot.sql
```

## Règle

En développement, pour repartir de zéro :

```text
1. Exécuter le script complet.
2. Exécuter les migrations nécessaires dans l'ordre chronologique.
```

Sur une base déjà utilisée :

```text
Ne pas relancer un script complet destructif.
Exécuter uniquement les migrations nécessaires.
Faire une sauvegarde avant modification.
```

## À documenter pour chaque migration

Chaque migration doit préciser :

```text
objectif
date
tables modifiées
colonnes ajoutées
contraintes ajoutées
index ajoutés
risques
commande de vérification
commande de rollback si disponible
```
