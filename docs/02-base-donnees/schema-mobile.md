# Schéma SQL - Mobile

## Principe

Les tables `Mobile_*` conservent les données propres au projet MobileSLI.

Elles ne remplacent pas ABSSolute.

Elles servent à stocker les données de terrain, les synchronisations, les quantités et les logs.

## Séparation Mobile / Expédition

Les tables mobile stockent ce qui vient du livreur et de l'application Android.

Les tables Expédition stockent ce qui vient du service Expédition après verrouillage.

```text
Mobile_Tournee*                 -> données saisies et envoyées par le mobile
Mobile_ExpeditionPreparation*   -> données préparées et verrouillées par l'Expédition
Mobile_ArticleSaisissable       -> référentiel commun
Mobile_LogSynchronisation       -> logs communs
```

Le mobile ne lit pas les brouillons SQLite du serveur web Expédition.

Le mobile reçoit uniquement les préparations Expédition verrouillées par l'API.

## Tables principales côté mobile

| Table | Rôle |
|---|---|
| `Mobile_Livreur` | Référentiel des livreurs connus côté mobile |
| `Mobile_ChargementTournee` | Historique des chargements de tournée |
| `Mobile_Tournee` | En-tête d'une tournée synchronisée |
| `Mobile_TourneeLigne` | Détail des points de livraison |
| `Mobile_TourneeLigneQuantite` | Quantités par article et par ligne |
| `Mobile_LogSynchronisation` | Journal des synchronisations et erreurs |
| `Mobile_ArticleSaisissable` | Articles affichés ou saisis dans l'application |

## Tables Expédition lues par le mobile via l'API

Le mobile ne lit pas ces tables directement.

L'API les utilise pour alimenter `quantiteLivreePrevue` dans `GET /api/tournees/jour`.

| Table | Rôle |
|---|---|
| `Mobile_ExpeditionPreparation` | En-tête d'une préparation Expédition verrouillée |
| `Mobile_ExpeditionPreparationLigne` | Quantités prévues par article et par arrêt |
| `Mobile_CommentaireExceptionnel` | Commentaire ponctuel séparé des instructions |

### Règle importante : ne pas utiliser StatutLot pour le mobile

Le mobile ne doit pas déterminer les tournées disponibles à partir de :

```text
Mobile_ExpeditionLotVerrouillage.StatutLot
```

`Mobile_ExpeditionLotVerrouillage` est un journal technique des lots envoyés par le serveur web Expédition.

Cette table sert à tracer les POST de verrouillage, à conserver l’historique des lots et à identifier le dernier lot global actif.

Elle ne doit pas être utilisée comme source principale pour savoir quelles tournées sont valides côté mobile.

La validité des données Expédition pour le mobile repose sur :

```sql
Mobile_ExpeditionPreparation.StatutPreparation = N'VERROUILLEE'
AND Mobile_ExpeditionPreparation.EstVerrouille = 1
AND Mobile_ExpeditionPreparationLigne.Actif = 1
```

Un lot avec :

```text
StatutLot = REMPLACE
```

peut avoir verrouillé des tournées encore valides dans :

```text
Mobile_ExpeditionPreparation
```

Exemple :

```text
1. Un premier lot verrouille les tournées 5001 et 5017.
2. Un second lot verrouille ensuite la tournée 5005.
3. Le premier lot passe en REMPLACE.
4. Le second lot devient VERROUILLE.
5. Les tournées 5001, 5017 et 5005 restent valides si elles sont présentes dans Mobile_ExpeditionPreparation avec :
   - StatutPreparation = VERROUILLEE ;
   - EstVerrouille = 1 ;
   - lignes Actif = 1.
```

Donc le mobile et les futurs exports métier doivent partir de :

```text
Mobile_ExpeditionPreparation
Mobile_ExpeditionPreparationLigne
```

et non de :

```text
Mobile_ExpeditionLotVerrouillage.StatutLot
```

## Mobile_Tournee

Champs importants :

```text
IdTourneeMobile
IdSynchronisation
DateTournee
CodeTournee
LibelleTournee
IdLivreur
StatutSynchronisation
DateChargementMobile
DateReceptionApi
DateEnvoi
EstVerrouillee
NomAppareil
VersionApplication
AdresseIP
```

## Mobile_TourneeLigne

Stocke les informations d'un arrêt de tournée :

```text
IdLigneSource
OrdreArret
NumClient
NomClient
NomAffiche
CodePDL
DescriptionPDL
Adresse
Instructions
CommentaireExceptionnel
StatutPassage
CommentaireLivreur
HeureValidation
EstValidee
```

## Mobile_TourneeLigneQuantite

Stocke les quantités par article :

```text
CodeArticle
LibelleArticle
QuantiteLivreePrevue
QuantiteLivree
QuantiteRecuperee
```

## Origine de QuantiteLivreePrevue

`QuantiteLivreePrevue` vient du module Expédition uniquement après verrouillage par l’API.

Flux :

```text
Mobile_ExpeditionPreparationLigne.QuantiteLivreePrevue
        -> API GET /api/tournees/jour
        -> lignes[].saisie.quantites[].quantiteLivreePrevue
        -> Mobile_TourneeLigneQuantite.QuantiteLivreePrevue après synchronisation mobile
```

Une préparation Expédition non verrouillée ne doit jamais alimenter le mobile.

Le mobile reçoit uniquement les quantités prévues provenant de lignes actives :

```sql
Mobile_ExpeditionPreparation.StatutPreparation = N'VERROUILLEE'
AND Mobile_ExpeditionPreparation.EstVerrouille = 1
AND Mobile_ExpeditionPreparationLigne.Actif = 1
```

Le statut du lot global dans `Mobile_ExpeditionLotVerrouillage` ne doit pas être utilisé pour décider si une tournée est disponible côté mobile.

`Mobile_ExpeditionLotVerrouillage.StatutLot` sert à l’audit des lots envoyés, pas à la validité métier des tournées.

## Articles

Articles principaux :

```text
ROLLS
TAPIS
SACS
ROLLS_VIDES
```

Règles :

```text
ROLLS, TAPIS, SACS -> livrables et récupérables selon le besoin métier
ROLLS_VIDES        -> récupérable côté mobile uniquement
ROLLS_VIDES        -> interdit côté Expédition
```

## Règles métier sur les quantités

```text
QuantiteLivreePrevue = NULL -> l'Expédition n'a rien renseigné
QuantiteLivreePrevue = 0    -> l'Expédition a volontairement prévu zéro
QuantiteLivreePrevue > 0    -> quantité prévue
QuantiteLivreePrevue < 0    -> interdit
QuantiteLivree < 0          -> interdit
QuantiteRecuperee < 0       -> interdit
```

## Anti-doublon mobile

Anti-doublon technique :

```text
IdSynchronisation unique
```

Anti-doublon métier :

```text
DateTournee + CodeTournee
```

Le livreur est conservé pour la trace, mais il ne permet pas d'envoyer une deuxième fois la même tournée pour la même date.

## Vérifications après synchronisation mobile

```sql
SELECT TOP 10 *
FROM dbo.Mobile_Tournee
ORDER BY IdTourneeMobile DESC;

SELECT TOP 50 *
FROM dbo.Mobile_TourneeLigne
ORDER BY IdTourneeMobile DESC, OrdreArret;

SELECT TOP 100 *
FROM dbo.Mobile_TourneeLigneQuantite
ORDER BY IdQuantite DESC;

SELECT TOP 20 *
FROM dbo.Mobile_LogSynchronisation
ORDER BY IdLog DESC;
```

Résultat attendu :

```text
Mobile_Tournee              -> 1 en-tête de tournée
Mobile_TourneeLigne         -> 1 ligne par arrêt
Mobile_TourneeLigneQuantite -> 1 ligne par article et par arrêt
Mobile_LogSynchronisation   -> 1 log de réussite ou d'erreur
```

## Vérification que les préparations verrouillées alimentent le mobile

```sql
SELECT TOP 50
    p.DateTournee,
    p.CodeTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    q.IdLigneSource,
    q.CodeArticle,
    q.QuantiteLivreePrevue
FROM dbo.Mobile_ExpeditionPreparation p
INNER JOIN dbo.Mobile_ExpeditionPreparationLigne q
    ON q.IdPreparationExpedition = p.IdPreparationExpedition
WHERE p.EstVerrouille = 1
  AND p.StatutPreparation = N'VERROUILLEE'
  AND q.Actif = 1
ORDER BY p.DateTournee DESC, p.CodeTournee, q.IdLigneSource, q.CodeArticle;
```
