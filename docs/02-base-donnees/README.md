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
| `Mobile_Tournee` | En-tête de synchronisation mobile |
| `Mobile_TourneeCamion` | Camion, kilométrages et dates de trajet associés à une synchronisation mobile |
| `Mobile_TourneeLigne` | Lignes de tournée synchronisées par le mobile |
| `Mobile_TourneeLigneQuantite` | Quantités synchronisées par ligne mobile |
| Tables `Mobile_Expedition*` | Données verrouillées par le module Expédition |
| `Mobile_ArticleSaisissable` | Référentiel commun des articles suivis |
| `Mobile_CommentaireExceptionnel` | Commentaires ponctuels lus par le mobile |
| `Mobile_LogSynchronisation` | Audit technique et métier |

## Stockage du contrat mobile 1.3

### En-tête de synchronisation

`dbo.Mobile_Tournee` conserve l'en-tête de synchronisation mobile :

```text
schemaVersion
idSynchronisation
dateTournee
codeTournee
livreur
mobile
date d'envoi
statut de synchronisation
```

### Trajet camion

`dbo.Mobile_TourneeCamion` conserve le trajet camion envoyé en `schemaVersion` `1.3` :

```text
IdTourneeMobile
IdCamionSource
CodeCamion
LibelleCamion
Immatriculation
KilometrageDepart
KilometrageArrivee
DateDepartMobile
DateArriveeMobile
DateCreation
```

Règle de cardinalité :

```text
Un seul Mobile_TourneeCamion par IdTourneeMobile.
```

Le repository insère `Mobile_TourneeCamion` dans la même transaction SQL que `Mobile_Tournee`, avant les lignes et avant le commit final.

## Documents

| Fichier | Rôle |
|---|---|
| `schema-mobile.md` | Tables principales côté mobile |
| `expedition-sql.md` | Tables liées au module Expédition |
| `migrations.md` | Règles d'organisation des scripts SQL |
| `complete/BDD_sli_v13_complete.sql` | Script complet de recréation en développement/test |
| `migrations/20260604_ajout_mobile_tournee_camion.sql` | Migration non destructive de `Mobile_TourneeCamion` |
| `../04-tests/masse/sql/nettoyage-run-k6-mobile-13-trajet-camion.sql` | Nettoyage ciblé des runs k6 mobile 1.3 |

## Règles importantes

Le mobile ne se connecte jamais directement à SQL Server.

Le serveur web Expédition ne se connecte jamais directement à SQL Server.

L'API ASP.NET Core est le seul point d'entrée vers SQL Server pour le mobile et pour le module Expédition.

Les vues et tables internes ABSSolute ne doivent pas être modifiées par le projet.

Une préparation Expédition non verrouillée ne doit jamais alimenter `GET /api/tournees/jour`.

`Mobile_TourneeCamion` ne remplace pas `Mobile_Tournee` : elle complète la synchronisation avec le camion et les kilométrages.

## Ordre de validation recommandé

Après exécution du script complet ou d'une migration :

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
10. Tester GET /api/camions/disponibles.
11. Tester POST /api/synchronisations en schemaVersion 1.3.
12. Vérifier l'insertion dans Mobile_TourneeCamion.
13. Lancer le test k6 mobile 1.3.
```
