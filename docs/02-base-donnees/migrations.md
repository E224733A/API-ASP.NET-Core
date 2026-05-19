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
2026-05-19_alignement_expedition_preparation.sql
```

## Décision actuelle en développement

Comme le projet est encore en développement, il est acceptable de repartir de zéro avec le script complet si les données de test peuvent être supprimées.

Procédure recommandée :

```text
1. Sauvegarder si des données doivent être conservées.
2. Exécuter complete/BDD_sli_v13_complete.sql sur la base de développement.
3. Corriger l'API pour correspondre au schéma final.
4. Tester les routes API dans l'ordre.
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
    └── 2026-05-19_alignement_expedition_preparation.sql
```

## Règle en développement

Pour repartir de zéro :

```text
1. Exécuter le script complet.
2. Ne pas exécuter ensuite d'anciennes migrations qui recréent Mobile_PreRemplissage*.
3. Corriger l'API pour utiliser les tables finales Mobile_ExpeditionPreparation*.
4. Relancer l'API.
5. Tester GET /api/expedition/preparations/a-preparer.
6. Tester POST /api/expedition/preparations/verrouiller.
7. Tester GET /api/tournees/jour.
8. Tester POST /api/synchronisations.
```

## Règle sur une base déjà utilisée

Sur une base qui contient des données à conserver :

```text
Ne pas relancer un script complet destructif.
Exécuter uniquement les migrations nécessaires.
Faire une sauvegarde avant modification.
Tester la migration sur une copie avant production.
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

## Règle de cohérence API / SQL

Le code C# et le script SQL doivent utiliser le même vocabulaire.

Vocabulaire final retenu :

```text
Mobile_ExpeditionPreparation
Mobile_ExpeditionPreparationLigne
Mobile_ExpeditionPreparationHistorique
```

Les noms suivants sont obsolètes :

```text
Mobile_PreRemplissageTournee
Mobile_PreRemplissageQuantite
Mobile_PreRemplissageHistorique
```

Ils ne doivent plus être utilisés par l'API finale.
