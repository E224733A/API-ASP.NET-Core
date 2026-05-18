# Contrats JSON - Mobile

## Version

```json
{
  "schemaVersion": "1.2"
}
```

## GET /api/tournees/jour

### Rôle

Retourne au mobile une tournée complète à charger au dépôt.

La réponse contient :

```text
schemaVersion
dateTournee
codeTournee
libelleTournee
statutSynchronisation
livreur
mobile
chargement
lignes[]
```

## Ligne mobile

Chaque ligne représente un point de livraison.

Champs principaux :

```text
idLigneSource
ordreArret
horaire
client
pointLivraison
tournee
retour
infosLivreur
saisie
```

## idLigneSource

`idLigneSource` est l'identifiant stable utilisé par le mobile hors connexion.

Il sert à relier :

- la ligne chargée le matin ;
- les quantités prévues Expédition ;
- la saisie mobile du livreur ;
- les données enregistrées au retour.

Format courant :

```text
dateTournee|codeTournee|jourTournee|numClient|codePDL|ordreArret
```

## infosLivreur

Le mobile doit afficher séparément :

```text
instructions
commentaireExceptionnel
```

Il ne faut pas fusionner les deux champs.

Exemples d'affichage attendu :

```text
Instruction : Livraison par l'arrière.
Commentaire exceptionnel : Prévoir passage avant 10h.
```

## saisie.quantites[]

La saisie des quantités est flexible.

Exemple :

```json
{
  "codeArticle": "ROLLS",
  "libelle": "Rolls",
  "quantiteLivreePrevue": null,
  "quantiteLivree": 1,
  "quantiteRecuperee": 2
}
```

## Sens de quantiteLivreePrevue

```text
null -> l'Expédition n'a rien renseigné
0    -> l'Expédition a volontairement prévu zéro
> 0  -> quantité prévue
< 0  -> interdit
```

## POST /api/synchronisations

### Rôle

Le mobile envoie le résultat final de la tournée au retour dépôt.

### Structure minimale

```json
{
  "schemaVersion": "1.2",
  "idSynchronisation": "11111111-1111-1111-1111-111111111111",
  "dateTournee": "2026-05-19",
  "codeTournee": "2023",
  "libelleTournee": "CLISSON",
  "livreur": {
    "codeLivreur": "2",
    "nomLivreur": "DAVID LEBAS"
  },
  "mobile": {
    "nomAppareil": "Telephone SLI",
    "versionApplication": "1.0.0",
    "dateChargementMobile": "2026-05-19T07:00:00+02:00",
    "dateEnvoiMobile": "2026-05-19T17:00:00+02:00"
  },
  "commentaireGlobal": null,
  "lignes": []
}
```

## Statuts de passage

```text
FAIT
NON_FAIT
ANOMALIE
```

`A_FAIRE` ne doit pas être présent dans le POST final.

## Anti-doublons

```text
idSynchronisation -> anti-rejeu technique
DateTournee + CodeTournee -> anti-doublon métier
```
