# Contrats JSON - Mobile

## Version

```json
{
  "schemaVersion": "1.2"
}
```

## Transition 1.2 -> 1.3

Cette section prépare l'évolution du contrat mobile avant codage.

État documenté :

```text
schemaVersion 1.2 -> contrat existant, accepté temporairement avec trajet optionnel
schemaVersion 1.3 -> contrat cible, accepté avec trajet obligatoire lorsque la migration sera codée
```

Points importants :

- la version `1.2` reste compatible avec les synchronisations existantes ;
- l'objet `trajet` est optionnel en `1.2` pour permettre une transition progressive ;
- l'objet `trajet` devient obligatoire en `1.3` ;
- cette documentation prépare la migration et ne signifie pas que le code API est déjà modifié.

## GET /api/camions/disponibles

### Rôle

Retourne au mobile la liste des camions disponibles pour permettre au livreur de sélectionner le camion utilisé avant le départ.

Cette route est une nouvelle route à ajouter lors du développement de la version camion / kilométrages.

### Réponse attendue

```json
{
  "schemaVersion": "1.2",
  "camions": [
    {
      "idCamion": "12",
      "codeCamion": "12",
      "libelleCamion": "Camion 12",
      "immatriculation": "AB-123-CD",
      "estActif": true
    }
  ]
}
```

### Champs camion

```text
idCamion        -> identifiant stable du camion côté API / base
codeCamion      -> code métier lisible
libelleCamion   -> libellé affichable dans le mobile
immatriculation -> plaque du véhicule, si disponible
estActif        -> indique si le camion peut être proposé au mobile
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

### Structure minimale 1.2 existante

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

### Section trajet cible 1.3

En `schemaVersion` `1.3`, la synchronisation doit contenir une section `trajet` permettant de tracer le camion utilisé et les kilométrages de départ et d'arrivée.

```json
{
  "trajet": {
    "camion": {
      "idCamion": "12",
      "codeCamion": "12",
      "libelleCamion": "Camion 12",
      "immatriculation": "AB-123-CD"
    },
    "kilometrageDepart": 128100,
    "kilometrageArrivee": 128450,
    "dateDepartMobile": "2026-06-04T07:30:00+02:00",
    "dateArriveeMobile": "2026-06-04T16:45:00+02:00"
  }
}
```

### Exemple cible 1.3 avec trajet

```json
{
  "schemaVersion": "1.3",
  "idSynchronisation": "11111111-1111-1111-1111-111111111111",
  "dateTournee": "2026-06-04",
  "codeTournee": "2023",
  "libelleTournee": "CLISSON",
  "livreur": {
    "codeLivreur": "2",
    "nomLivreur": "DAVID LEBAS"
  },
  "mobile": {
    "nomAppareil": "Telephone SLI",
    "versionApplication": "1.0.0",
    "dateChargementMobile": "2026-06-04T07:00:00+02:00",
    "dateEnvoiMobile": "2026-06-04T17:00:00+02:00"
  },
  "trajet": {
    "camion": {
      "idCamion": "12",
      "codeCamion": "12",
      "libelleCamion": "Camion 12",
      "immatriculation": "AB-123-CD"
    },
    "kilometrageDepart": 128100,
    "kilometrageArrivee": 128450,
    "dateDepartMobile": "2026-06-04T07:30:00+02:00",
    "dateArriveeMobile": "2026-06-04T16:45:00+02:00"
  },
  "commentaireGlobal": null,
  "lignes": []
}
```

### Règles de validation trajet

```text
schemaVersion 1.2 accepté temporairement avec trajet optionnel
schemaVersion 1.3 accepté avec trajet obligatoire
trajet obligatoire en 1.3
trajet.camion obligatoire en 1.3
trajet.camion.idCamion obligatoire en 1.3
trajet.kilometrageDepart obligatoire en 1.3
trajet.kilometrageArrivee obligatoire en 1.3
trajet.dateDepartMobile obligatoire en 1.3
trajet.dateArriveeMobile obligatoire en 1.3
trajet.kilometrageDepart >= 0
trajet.kilometrageArrivee >= 0
trajet.kilometrageArrivee >= trajet.kilometrageDepart
trajet.dateArriveeMobile >= trajet.dateDepartMobile
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
