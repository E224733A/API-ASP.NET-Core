# Schéma SQL - Mobile

## Principe

Les tables `Mobile_*` conservent les données propres au projet MobileSLI.

Elles ne remplacent pas ABSSolute.

Elles servent à stocker les données de terrain, les synchronisations, les quantités et les logs.

## Tables principales

| Table | Rôle |
|---|---|
| `Mobile_Livreur` | Référentiel des livreurs connus côté mobile |
| `Mobile_ChargementTournee` | Historique des chargements de tournée |
| `Mobile_Tournee` | En-tête d'une tournée synchronisée |
| `Mobile_TourneeLigne` | Détail des points de livraison |
| `Mobile_TourneeLigneQuantite` | Quantités par article et par ligne |
| `Mobile_LogSynchronisation` | Journal des synchronisations et erreurs |
| `Mobile_ArticleSaisissable` | Articles affichés ou saisis dans l'application |

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

## Règles métier

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
