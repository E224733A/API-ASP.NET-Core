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

Cette table permet l'idempotence du POST :

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
Pour le POST global, CodeTournee vaut généralement GLOBAL.
```

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
```

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
