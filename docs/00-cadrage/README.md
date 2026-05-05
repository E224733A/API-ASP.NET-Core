# Cadrage projet

Ce dossier contient le cahier des charges du projet mobile de tournée.

## Fonctionnement métier retenu

Le fonctionnement retenu est le suivant :

1. Le livreur s’identifie avec son code livreur.
2. La date de tournée correspond à la date du jour.
3. La date n’est pas modifiable dans l’application mobile.
4. Le livreur choisit sa tournée.
5. L’application charge les lignes de tournée depuis l’API.
6. Le livreur travaille hors connexion pendant la journée.
7. Le livreur valide chaque passage.
8. Le soir, il envoie la tournée complète.
9. Après envoi, la tournée est verrouillée côté mobile.

## Correction après envoi

Une tournée envoyée ne doit plus être modifiable par le livreur.

Si une correction est nécessaire, elle doit être traitée côté administration ou informatique, pas depuis le mobile.

## Données affichées au livreur

L’application doit limiter la saisie manuelle.

Le livreur renseigne principalement :

- le statut du passage ;
- les quantités livrées ;
- les quantités récupérées ;
- une précision si nécessaire ;
- un commentaire obligatoire en cas de `NON_FAIT` ou `ANOMALIE`.

## Quantités

La saisie utilise le format extensible `quantites[]`.

Pour la version actuelle, les articles actifs sont :

- `ROLLS` ;
- `TAPIS` ;
- `SACS`.

Le modèle permet d’ajouter ensuite d’autres articles, par exemple `VETEMENTS` ou `EXPES`, sans changer la structure du contrat JSON.
