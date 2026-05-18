# Schéma SQL - Expédition

## Tables concernées

```text
Mobile_PreRemplissageTournee
Mobile_PreRemplissageQuantite
Mobile_PreRemplissageHistorique
Mobile_ExpeditionLotVerrouillage
Mobile_LogSynchronisation
```

## Principe

Le Web Expédition conserve ses brouillons dans SQLite local.

SQL Server reçoit uniquement les données officielles après verrouillage par l'API.

Flux :

```text
Web Expédition -> brouillon SQLite local
Web Expédition -> POST /api/expedition/preparations/verrouiller
API -> validation
API -> écriture SQL Server
Mobile -> lecture des préparations verrouillées
```

## Mobile_ExpeditionLotVerrouillage

Rôle : tracer les lots de verrouillage envoyés par le Web Expédition.

Cette table permet l'idempotence du POST :

```http
POST /api/expedition/preparations/verrouiller
```

Clé métier :

```text
IdLotVerrouillage
```

Champs importants :

```text
IdLotVerrouillage
DateTournee
DateReceptionApi
Statut
Message
NombreTournees
NombreLignes
NombreQuantites
EmpreintePayload
AdresseIP
DateCreation
```

## Mobile_PreRemplissageTournee

Stocke les tournées verrouillées côté Expédition.

Champs importants :

```text
DateTournee
CodeTournee
LibelleTournee
EstVerrouille
DateVerrouillage
IdLotVerrouillage
DateReceptionApi
```

## Mobile_PreRemplissageQuantite

Stocke les quantités prévues par article.

Champs importants :

```text
IdPreRemplissageTournee
IdLigneSource
CodeArticle
LibelleArticle
QuantiteLivreePrevue
Actif
DateCreation
DateModification
```

## Mobile_PreRemplissageHistorique

Trace les événements liés aux pré-remplissages.

Exemples :

```text
CREATION
MODIFICATION
SUPPRESSION
VERROUILLAGE
TENTATIVE_MODIFICATION_APRES_BLOCAGE
```

## Articles Expédition

Autorisés :

```text
ROLLS
TAPIS
SACS
```

Interdit :

```text
ROLLS_VIDES
```

## Quantités Expédition

```text
QuantiteLivreePrevue = NULL -> non renseigné
QuantiteLivreePrevue = 0    -> zéro prévu explicitement
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
FROM dbo.Mobile_PreRemplissageTournee
ORDER BY DateVerrouillage DESC;

SELECT TOP 100 *
FROM dbo.Mobile_PreRemplissageQuantite
ORDER BY DateCreation DESC;

SELECT TOP 20 *
FROM dbo.Mobile_PreRemplissageHistorique
ORDER BY DateEvenement DESC;
```

Résultat attendu :

```text
Mobile_ExpeditionLotVerrouillage -> lot de verrouillage SUCCESS
Mobile_PreRemplissageTournee     -> tournées verrouillées avec IdLotVerrouillage
Mobile_PreRemplissageQuantite    -> quantités prévues par idLigneSource et codeArticle
Mobile_PreRemplissageHistorique  -> trace du verrouillage
```

## Lien avec le mobile

Le mobile ne lit pas SQLite.

Le mobile lit uniquement les données verrouillées en SQL Server.

Une préparation non verrouillée ne doit pas être visible dans `GET /api/tournees/jour`.
