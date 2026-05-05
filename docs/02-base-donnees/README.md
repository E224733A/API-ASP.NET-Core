# Documentation base de données

Ce dossier documente les tables SQL Server dédiées au projet mobile.

## Principe

Les vues ABSSolute restent la source de lecture.

L’API ne modifie pas les tables internes ABSSolute.

Les données saisies par les livreurs sont stockées dans les tables mobiles.

## Tables mobiles

### Mobile_Livreur

Stocke les livreurs connus côté mobile.

Alimentation principale :

```text
v_chauffeurs.DRIVERNUMERO -> CodeLivreur
v_chauffeurs.DRIVERNAME   -> NomLivreur
```

### Mobile_Tournee

Stocke l’en-tête d’une tournée envoyée.

Une ligne correspond à une tournée envoyée par un livreur pour une date donnée.

### Mobile_TourneeLigne

Stocke les lignes client / point de livraison.

Une ligne correspond à un arrêt de tournée.

Les colonnes `QuantiteLivree` et `QuantiteReprise` sont des totaux calculés depuis `Mobile_TourneeLigneQuantite`.

### Mobile_TourneeLigneQuantite

Stocke le détail des quantités par article.

Chaque ligne contient :

- `CodeArticle` ;
- `LibelleArticle` ;
- `QuantiteLivree` ;
- `QuantiteRecuperee`.

Ce modèle permet d’ajouter de nouveaux articles sans changer la structure principale.

### Mobile_LogSynchronisation

Stocke les événements importants.

### Mobile_ExportAdmin

Stocke l’historique des exports admin.

## Anti-doublons

Anti-doublon technique :

```text
IdSynchronisation unique
```

Anti-doublon métier :

```text
DateTournee + CodeTournee + IdLivreur
```

Une même tournée peut être renvoyée une semaine plus tard, car `DateTournee` change.

## Dédoublonnage du GET

La vue `v_pdl_jour` peut contenir plusieurs lignes pour un même client / PDL / jour.

Le repository utilise `ROW_NUMBER()` pour ne garder qu’une ligne par point de passage.

Objectif : éviter d’envoyer deux fois le même arrêt au mobile.
