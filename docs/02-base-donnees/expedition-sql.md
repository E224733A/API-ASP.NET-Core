# Schéma SQL - Expédition

## Décision d'architecture

Le module Expédition utilise l'option A :

```text
Brouillons Expédition -> SQLite local côté serveur web Expédition
Préparations verrouillées -> SQL Server via l'API centrale
```

SQL Server ne stocke donc pas les brouillons de saisie.

SQL Server reçoit uniquement les données officielles après verrouillage par l'API.

## Tables concernées

```text
Mobile_ExpeditionLotVerrouillage
Mobile_ExpeditionPreparation
Mobile_ExpeditionPreparationLigne
Mobile_ExpeditionPreparationHistorique
Mobile_CommentaireExceptionnel
Mobile_LogSynchronisation
```

Les anciens noms suivants ne doivent plus être utilisés dans le script complet final :

```text
Mobile_PreRemplissageTournee
Mobile_PreRemplissageQuantite
Mobile_PreRemplissageHistorique
```

Ils peuvent seulement apparaître dans la partie de nettoyage d'un script complet destructif pour supprimer d'anciennes tables de développement.

## Flux métier

```text
1. Web Expédition charge les données à préparer
   -> GET /api/expedition/preparations/a-preparer

2. Web Expédition conserve les modifications en brouillon local
   -> SQLite côté serveur web Expédition

3. Web Expédition verrouille le lot global
   -> POST /api/expedition/preparations/verrouiller

4. API valide le lot
   -> contrôle date, idLotVerrouillage, articles, lignes, quantités

5. API écrit dans SQL Server
   -> Mobile_ExpeditionLotVerrouillage
   -> Mobile_ExpeditionPreparation
   -> Mobile_ExpeditionPreparationLigne
   -> Mobile_ExpeditionPreparationHistorique
   -> Mobile_CommentaireExceptionnel si nécessaire
   -> Mobile_LogSynchronisation

6. Mobile charge ensuite uniquement les préparations verrouillées
   -> GET /api/tournees/jour
```

## Mobile_ExpeditionLotVerrouillage

Rôle : tracer les lots globaux envoyés par le Web Expédition.

Cette table est un journal technique des POST de verrouillage Expédition.

Elle permet :

```text
- de tracer chaque lot envoyé par le serveur web Expédition ;
- d'assurer l'idempotence du POST via IdLotVerrouillage ;
- de conserver l'historique des lots remplacés ;
- de savoir quel est le dernier lot global actif pour une date.
```

Route concernée :

```http
POST /api/expedition/preparations/verrouiller
```

Clé technique :

```text
IdLotVerrouillage
```

Règle métier :

```text
Un seul lot VERROUILLE est autorisé par DateTournee + CodeTournee.
Pour le POST global Expédition, CodeTournee vaut généralement GLOBAL.
```

Quand un nouveau verrouillage global est effectué pour une date déjà verrouillée, l'ancien lot `GLOBAL / VERROUILLE` passe en :

```text
REMPLACE
```

Le nouveau lot devient alors le seul lot :

```text
GLOBAL / VERROUILLE
```

Important : `StatutLot = REMPLACE` ne signifie pas que les tournées contenues dans cet ancien lot sont invalides.

Cela signifie seulement :

```text
ce lot global n’est plus le dernier lot global actif pour la date concernée.
```

Les tournées réellement valides pour le mobile ne sont pas déterminées par `Mobile_ExpeditionLotVerrouillage.StatutLot`.

La validité métier d’une tournée verrouillée est portée par :

```text
Mobile_ExpeditionPreparation
Mobile_ExpeditionPreparationLigne
```

Une tournée Expédition est considérée comme valide pour alimenter le mobile si :

```sql
Mobile_ExpeditionPreparation.StatutPreparation = N'VERROUILLEE'
AND Mobile_ExpeditionPreparation.EstVerrouille = 1
AND Mobile_ExpeditionPreparationLigne.Actif = 1
```

Le mobile, les exports métier et les futurs écrans d’administration ne doivent donc pas utiliser uniquement :

```sql
WHERE Mobile_ExpeditionLotVerrouillage.StatutLot = N'VERROUILLE'
```

pour déterminer les tournées utilisables.

Cette requête serait incorrecte, car elle ne récupérerait que les tournées du dernier lot global actif, alors que des tournées verrouillées par un ancien lot `REMPLACE` peuvent encore être valides dans `Mobile_ExpeditionPreparation`.

Champs importants :

```text
IdLotVerrouillage
EmpreintePayload
DateTournee
CodeTournee
LibelleTournee
StatutLot
NombrePreparations
NombreLignes
NombreQuantites
DateReceptionApi
DateSauvegardeSql
AdresseIP
NomAppareil
VersionApplication
MessageRetour
DateCreation
DateModification
```

Statuts autorisés :

```text
VERROUILLE
REJOUE_IDENTIQUE
REFUSE
REMPLACE
```

Signification des statuts :

```text
VERROUILLE
    Dernier lot global actif pour une date et un CodeTournee donné.

REJOUE_IDENTIQUE
    Lot rejoué avec le même contenu, conservé pour trace technique.

REFUSE
    Lot refusé par l’API ou par les règles de validation.

REMPLACE
    Ancien lot global qui a été remplacé par un nouveau lot VERROUILLE.
    Il reste conservé pour l’audit, mais il n’est plus le lot global actif.
```

Exemple :

```text
1. SERVWEB prépare les tournées 5001 et 5017.
2. L’API verrouille 5001 et 5017.
3. Mobile_ExpeditionPreparation contient 5001 et 5017 en VERROUILLEE.

4. SERVWEB prépare ensuite la tournée 5005.
5. L’API verrouille 5005.
6. L’ancien lot GLOBAL passe en REMPLACE.
7. Le nouveau lot GLOBAL passe en VERROUILLE.
8. 5001, 5017 et 5005 restent en VERROUILLEE dans Mobile_ExpeditionPreparation.
9. Le mobile peut lire 5001, 5017 et 5005.
```

Requête correcte pour récupérer les préparations valides :

```sql
SELECT
    p.DateTournee,
    p.CodeTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    l.IdLigneSource,
    l.CodeArticle,
    l.QuantiteLivreePrevue
FROM dbo.Mobile_ExpeditionPreparation p
INNER JOIN dbo.Mobile_ExpeditionPreparationLigne l
    ON l.IdPreparationExpedition = p.IdPreparationExpedition
WHERE p.DateTournee = @DateTournee
  AND p.StatutPreparation = N'VERROUILLEE'
  AND p.EstVerrouille = 1
  AND l.Actif = 1;
```

Requête à éviter pour déterminer les tournées utilisables :

```sql
SELECT *
FROM dbo.Mobile_ExpeditionLotVerrouillage lot
WHERE lot.StatutLot = N'VERROUILLE';
```

Cette requête ne doit servir qu’à identifier le dernier lot global actif, pas les tournées réellement exploitables par le mobile.

## Mobile_ExpeditionPreparation

Rôle : stocker l'en-tête d'une préparation Expédition officiellement verrouillée pour une date et une tournée.

Important :

```text
Cette table ne stocke pas de brouillon.
```

Champs importants :

```text
IdPreparationExpedition
DateTournee
CodeTournee
LibelleTournee
StatutPreparation
EstVerrouille
DateVerrouillage
IdLotVerrouillage
EmpreintePayload
DateCreation
DateModification
```

Règles :

```text
StatutPreparation = VERROUILLEE
EstVerrouille = 1
DateVerrouillage obligatoire
IdLotVerrouillage obligatoire
EmpreintePayload obligatoire
DateTournee + CodeTournee unique
```

Le statut `BROUILLON` ne doit pas exister dans cette table avec l'option A.

## Mobile_ExpeditionPreparationLigne

Rôle : stocker les quantités prévues par l'Expédition, par ligne métier et par article.

Ces valeurs alimentent ensuite :

```text
lignes[].saisie.quantites[].quantiteLivreePrevue
```

dans le JSON mobile.

Champs importants :

```text
IdPreparationExpeditionLigne
IdPreparationExpedition
IdLigneSource
OrdreArret
NumClient
NomClient
NomAffiche
CodePDL
DescriptionPDL
CodeArticle
LibelleArticle
QuantiteLivreePrevue
Actif
DateCreation
DateModification
```

Règles :

```text
Une ligne appartient à une préparation verrouillée.
Une seule quantité active est autorisée par IdPreparationExpedition + IdLigneSource + CodeArticle.
QuantiteLivreePrevue peut être NULL, 0 ou positive.
QuantiteLivreePrevue négative est interdite.
```

## Mobile_ExpeditionPreparationHistorique

Rôle : tracer les événements importants liés au verrouillage Expédition.

Actions autorisées :

```text
VERROUILLAGE
REJEU_VERROUILLAGE_IDENTIQUE
REFUS_VERROUILLAGE
TENTATIVE_MODIFICATION_APRES_VERROUILLAGE
```

Cette table ne doit pas servir à historiser des brouillons SQL Server, puisque les brouillons restent dans SQLite côté serveur web Expédition.

## Mobile_CommentaireExceptionnel

Rôle : stocker les commentaires ponctuels saisis ou validés par l'Expédition.

Ces commentaires sont lus ensuite par le mobile dans un champ séparé des instructions existantes.

Principe d'affichage côté mobile :

```text
instructions ABSSolute -> infosLivreur.instructions
commentaire Expédition -> infosLivreur.commentaireExceptionnel
```

Les deux informations ne doivent pas être fusionnées.

## Articles Expédition

Articles autorisés côté Expédition :

```text
ROLLS
TAPIS
SACS
```

Article interdit côté Expédition :

```text
ROLLS_VIDES
```

`ROLLS_VIDES` peut exister côté mobile pour la récupération, mais il ne doit pas être préparé par l'Expédition.

## Quantités Expédition

```text
QuantiteLivreePrevue = NULL -> l'Expédition n'a rien renseigné
QuantiteLivreePrevue = 0    -> l'Expédition a volontairement prévu zéro
QuantiteLivreePrevue > 0    -> quantité prévue
QuantiteLivreePrevue < 0    -> interdit
```

Les quantités récupérées ne sont pas préparées par l'Expédition.

## Vérification SQL après verrouillage

```sql
SELECT TOP 10 *
FROM dbo.Mobile_ExpeditionLotVerrouillage
ORDER BY DateReceptionApi DESC;

SELECT TOP 20 *
FROM dbo.Mobile_ExpeditionPreparation
ORDER BY DateVerrouillage DESC;

SELECT TOP 100 *
FROM dbo.Mobile_ExpeditionPreparationLigne
ORDER BY DateCreation DESC;

SELECT TOP 20 *
FROM dbo.Mobile_ExpeditionPreparationHistorique
ORDER BY DateEvenement DESC;
```

Résultat attendu :

```text
Mobile_ExpeditionLotVerrouillage     -> lot de verrouillage VERROUILLE
Mobile_ExpeditionPreparation         -> tournées verrouillées avec IdLotVerrouillage
Mobile_ExpeditionPreparationLigne    -> quantités prévues par idLigneSource et codeArticle
Mobile_ExpeditionPreparationHistorique -> trace du verrouillage
```

## Lien avec le mobile

Le mobile ne lit pas SQLite.

Le mobile lit uniquement les préparations verrouillées en SQL Server.

Une préparation non verrouillée ne doit pas être visible dans `GET /api/tournees/jour`.

La requête API qui alimente le mobile doit donc filtrer :

```sql
p.EstVerrouille = 1
AND p.StatutPreparation = N'VERROUILLEE'
AND q.Actif = 1
```
