# Contrats JSON - Mobile

## Version

Le contrat mobile final documenté pour le choix camion et les kilométrages est strictement en `schemaVersion` `1.3`.

```json
{
  "schemaVersion": "1.3"
}
```

Cette documentation prépare le développement du contrat `1.3`. Elle ne signifie pas que le code API, les scripts SQL, les payloads JSON de tests ou l'application mobile MAUI sont déjà modifiés.

## Contrat 1.3 strict

```text
schemaVersion doit être exactement "1.3".
schemaVersion "1.2" doit être refusé.
Toute autre schemaVersion doit être refusée.
trajet obligatoire.
trajet.camion obligatoire.
trajet.camion.idCamion obligatoire.
trajet.kilometrageDepart obligatoire.
trajet.kilometrageArrivee obligatoire.
trajet.dateDepartMobile obligatoire.
trajet.dateArriveeMobile obligatoire.
trajet.kilometrageDepart >= 0.
trajet.kilometrageArrivee >= 0.
trajet.kilometrageArrivee >= trajet.kilometrageDepart.
trajet.dateArriveeMobile >= trajet.dateDepartMobile.
```

L'API devra refuser explicitement les synchronisations mobiles en `schemaVersion` `1.2` lorsque ce contrat sera codé.

## GET /api/camions/disponibles

### Rôle

Retourne au mobile la liste des camions disponibles pour permettre au livreur de sélectionner le camion utilisé avant le départ.

Cette route est une nouvelle route à ajouter lors du développement de la version camion / kilométrages.

### Réponse attendue

```json
{
  "schemaVersion": "1.3",
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

### Structure attendue 1.3 stricte

```json
{
  "schemaVersion": "1.3",
  "idSynchronisation": "11111111-1111-1111-1111-111111111111",
  "dateTournee": "2026-06-04",
  "codeTournee": "4001",
  "libelleTournee": "CLISSON",
  "livreur": {
    "codeLivreur": "2",
    "nomLivreur": "DAVID LEBAS"
  },
  "mobile": {
    "nomAppareil": "Telephone SLI",
    "versionApplication": "1.0.0",
    "dateChargementMobile": "2026-06-04T07:30:00+02:00",
    "dateEnvoiMobile": "2026-06-04T16:45:00+02:00"
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

### Section trajet obligatoire

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

### Règles de validation

```text
schemaVersion doit être exactement "1.3".
schemaVersion "1.2" doit être refusé.
Toute autre schemaVersion doit être refusée.
trajet obligatoire.
trajet.camion obligatoire.
trajet.camion.idCamion obligatoire.
trajet.kilometrageDepart obligatoire.
trajet.kilometrageArrivee obligatoire.
trajet.dateDepartMobile obligatoire.
trajet.dateArriveeMobile obligatoire.
trajet.kilometrageDepart >= 0.
trajet.kilometrageArrivee >= 0.
trajet.kilometrageArrivee >= trajet.kilometrageDepart.
trajet.dateArriveeMobile >= trajet.dateDepartMobile.
```

## Dates mobile et dates trajet

```text
mobile.dateChargementMobile -> trace le chargement technique de la tournée.
mobile.dateEnvoiMobile      -> trace l'envoi technique du payload.
trajet.dateDepartMobile     -> correspond au départ métier après chargement de la tournée.
trajet.dateArriveeMobile    -> correspond à l'arrivée métier avant ou au moment de l'envoi final à l'API.
```

En première version, ces dates peuvent être identiques aux dates mobile, mais elles sont séparées dans le contrat pour clarifier le métier.

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
