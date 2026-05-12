# Documentation base de données

Ce dossier documente la base SQL Server dédiée au projet mobile SLI.

## Principe général

Les vues ABSSolute restent la source de lecture métier.

L'API lit les données ABSSolute, mais ne modifie jamais les tables internes ABSSolute.

Les données saisies par les livreurs, les synchronisations, les pré-remplissages et les logs sont stockés dans les tables `Mobile_*`.

```text
ABSSolute / vues SQL -> API ASP.NET Core -> Application mobile
Application mobile -> API ASP.NET Core -> Tables Mobile_*
```

## Tables principales

### Mobile_Livreur

Référence les livreurs connus côté mobile.

Alimentation principale :

```text
v_chauffeurs.DRIVERNUMERO -> CodeLivreur
v_chauffeurs.DRIVERNAME   -> NomLivreur
```

### Mobile_ChargementTournee

Historise les chargements de tournée du matin.

Cette table permet de savoir quelle tournée a été envoyée au mobile, quand, sur quel appareil et avec combien de points.

### Mobile_Tournee

Stocke l'en-tête d'une tournée synchronisée en fin de journée.

Une ligne correspond à une tournée envoyée par un livreur pour une date donnée.

Champs importants :

```text
IdSynchronisation
DateTournee
CodeTournee
IdLivreur
StatutSynchronisation
DateEnvoi
EstVerrouillee
NomAppareil
VersionApplication
```

### Mobile_TourneeLigne

Stocke les arrêts de tournée : client, point de livraison, adresse, informations livreur et saisie terrain.

Une ligne correspond à un arrêt de tournée.

Les colonnes `QuantiteLivree` et `QuantiteReprise` sont des totaux de compatibilité calculés depuis `Mobile_TourneeLigneQuantite`.

### Mobile_TourneeLigneQuantite

Table principale des quantités.

Une ligne correspond à un article saisi pour un arrêt.

Champs importants :

```text
CodeArticle
LibelleArticle
QuantiteLivreePrevue
QuantiteLivree
QuantiteRecuperee
```

Règle métier :

```text
QuantiteLivreePrevue = NULL -> l'expédition n'a rien renseigné
QuantiteLivreePrevue = 0    -> l'expédition a volontairement prévu zéro
QuantiteLivreePrevue > 0    -> l'expédition a prévu une quantité
```

Ce modèle permet d'ajouter de nouveaux articles sans modifier la structure principale.

### Mobile_ArticleSaisissable

Liste les articles affichés dans l'application mobile.

Première version :

```text
ROLLS
TAPIS
SACS
```

### Mobile_PreRemplissageTournee

Stocke les pré-remplissages préparés par l'expédition pour une tournée.

### Mobile_PreRemplissageQuantite

Stocke les quantités prévues par article.

Le pré-remplissage concerne uniquement la colonne `Livré`.

### Mobile_PreRemplissageHistorique

Trace les créations, modifications et suppressions de pré-remplissages.

### Mobile_CommentaireExceptionnel

Stocke les commentaires exceptionnels propres au projet mobile.

Ces commentaires ne viennent pas d'ABSSolute.

Ils sont liés à une date, un client et éventuellement un point de livraison.

### Mobile_LogSynchronisation

Stocke les événements importants liés aux synchronisations.

Exemples :

```text
ENVOI_REUSSI
VALIDATION_ERROR
DOUBLON_SYNCHRONISATION
DOUBLON_TOURNEE
ERREUR_TECHNIQUE
```

### Mobile_ExportAdmin

Prévu pour tracer les exports ou consultations administratives futures.

## Anti-doublons

### Anti-doublon technique

Empêche le même paquet mobile d'être enregistré deux fois.

```text
IdSynchronisation unique
```

### Anti-doublon métier

Empêche un livreur d'envoyer deux fois la même tournée pour la même date.

```text
DateTournee + CodeTournee + IdLivreur
```

Une même tournée peut être envoyée une semaine plus tard, car `DateTournee` change.

## Format JSON associé

La base est alignée avec le contrat JSON API `1.2`.

Le point central est le tableau `quantites[]` :

```json
{
  "quantites": [
    {
      "codeArticle": "ROLLS",
      "libelle": "Rolls",
      "quantiteLivreePrevue": null,
      "quantiteLivree": 1,
      "quantiteRecuperee": 2
    }
  ]
}
```

La source principale des quantités est donc :

```text
Mobile_TourneeLigneQuantite
```

Les anciennes colonnes de type `NbRolls`, `NbTapis`, `NbSacs`, `NbRecuperes` sont uniquement des colonnes de compatibilité.

## Dédoublonnage du GET

Certaines vues ABSSolute peuvent contenir plusieurs lignes pour un même client, point de livraison et jour.

Le repository utilise `ROW_NUMBER()` pour ne garder qu'une seule ligne par arrêt.

Objectif : éviter d'envoyer deux fois le même arrêt au mobile.

## Vérifications après synchronisation

Après un `POST /api/synchronisations` réussi, vérifier :

```sql
SELECT TOP 10 *
FROM Mobile_Tournee
ORDER BY IdTourneeMobile DESC;

SELECT TOP 50 *
FROM Mobile_TourneeLigne
ORDER BY IdTourneeMobile DESC, OrdreArret;

SELECT TOP 100 *
FROM Mobile_TourneeLigneQuantite
ORDER BY IdQuantite DESC;

SELECT TOP 20 *
FROM Mobile_LogSynchronisation
ORDER BY IdLog DESC;
```

Résultat attendu :

```text
Mobile_Tournee                  -> 1 en-tête de tournée
Mobile_TourneeLigne             -> 1 ligne par arrêt
Mobile_TourneeLigneQuantite     -> 1 ligne par article et par arrêt
Mobile_LogSynchronisation       -> 1 log ENVOI_REUSSI
```

## À retenir

La base mobile ne remplace pas ABSSolute.

Elle sert à conserver les données terrain saisies par les livreurs, les quantités prévues et réelles, les commentaires exceptionnels, les logs et les futures exploitations administratives.
