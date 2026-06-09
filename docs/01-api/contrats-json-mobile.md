# Contrats JSON - Mobile

## Versions mobiles

Le projet utilise trois contrats mobiles distincts :

```text
GET /api/tournees/disponibles -> schemaVersion inchangé, actuellement "1.2".
GET /api/tournees/jour       -> schemaVersion inchangé, actuellement "1.2".
GET /api/camions/disponibles -> schemaVersion "1.3".
POST /api/synchronisations   -> schemaVersion strictement "1.3".
```

Le champ optionnel `pointLivraison.lienAdresseLivraison` est ajouté uniquement au JSON de chargement des tournées mobile. Il ne change pas `schemaVersion`.

`schemaVersion` `1.2` est refusé uniquement pour `POST /api/synchronisations`.

## GET /api/tournees/jour - lienAdresseLivraison final

### Source SQL finale

La source finale imposée côté API est :

```sql
[lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
```

Colonnes attendues :

```text
NUM_CLI
CodePDL
AdresseLivraison
```

`AdresseLivraison` doit contenir un lien Google Maps déjà construit par la vue SQL. L'API ne construit plus de lien à partir de latitude/longitude et ne contient plus de mode Hardcoded.

Requête utilisée par l'API :

```sql
SELECT TOP (1)
    NULLIF(LTRIM(RTRIM(CAST(AdresseLivraison AS NVARCHAR(2048)))), N'') AS AdresseLivraison
FROM [lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
WHERE LTRIM(RTRIM(CAST(NUM_CLI AS NVARCHAR(50)))) = @NumCli
  AND LTRIM(RTRIM(CAST(CodePDL AS NVARCHAR(100)))) = @CodePDL;
```

### Règles

```text
champ optionnel.
recherche par NUM_CLI + CodePDL.
null si NUM_CLI vide.
null si CodePDL vide.
null si le couple NUM_CLI + CodePDL est absent de la vue.
null si AdresseLivraison est vide.
null si AdresseLivraison n'est pas une URL exploitable.
ne change pas schemaVersion.
ne change pas POST /api/synchronisations.
```

### Exemple JSON

```json
{
  "pointLivraison": {
    "codePDL": "PDL001",
    "descriptionPDL": "Entrée principale",
    "adresseLigne1": "10 Rue Exemple",
    "adresseLigne2": null,
    "adresseLigne3": null,
    "ville": "Nantes",
    "codePostal": "44000",
    "lienAdresseLivraison": "https://www.google.com/maps/search/?api=1&query=47.218371,-1.553621"
  }
}
```

### Configuration API

```json
{
  "LiensAdresseLivraison": {
    "Enabled": true
  }
}
```

## GET /api/camions/disponibles

Réponse attendue :

```json
{
  "schemaVersion": "1.3",
  "camions": [
    {
      "idCamion": "DY-662-QN",
      "codeCamion": "DY-662-QN",
      "libelleCamion": "Camion 12",
      "immatriculation": "DY-662-QN",
      "estActif": true
    }
  ]
}
```

Règles :

```text
schemaVersion = "1.3".
camions[] toujours présent.
camions triés par codeCamion, immatriculation puis idCamion.
les paramètres date et dateTournee sont refusés.
```

Source SQL confirmée côté repository :

```sql
[lavinprosli].[dbo].[v_Truck]
```

## POST /api/synchronisations

Le mobile envoie le résultat final de la tournée au retour dépôt.

La synchronisation est strictement en `schemaVersion` `1.3` avec section `trajet` obligatoire.

### Structure minimale du trajet

```json
{
  "schemaVersion": "1.3",
  "trajet": {
    "camion": {
      "idCamion": "DY-662-QN",
      "codeCamion": "DY-662-QN",
      "libelleCamion": "Camion 12",
      "immatriculation": "DY-662-QN"
    },
    "kilometrageDepart": 120000,
    "kilometrageArrivee": 120085,
    "dateDepartMobile": "2026-06-09T08:00:00+02:00",
    "dateArriveeMobile": "2026-06-09T17:30:00+02:00"
  }
}
```

### Validation trajet

```text
trajet obligatoire.
trajet.camion obligatoire.
trajet.camion.idCamion obligatoire.
trajet.kilometrageDepart obligatoire, positif ou nul.
trajet.kilometrageArrivee obligatoire, positif ou nul.
trajet.kilometrageArrivee >= trajet.kilometrageDepart.
trajet.dateDepartMobile obligatoire.
trajet.dateArriveeMobile obligatoire.
trajet.dateArriveeMobile >= trajet.dateDepartMobile.
```

### Stockage

Le trajet est sauvegardé dans `dbo.Mobile_TourneeCamion` dans la même transaction SQL que `dbo.Mobile_Tournee`, avant l'insertion des lignes et des quantités.

Le champ `pointLivraison.lienAdresseLivraison` ne doit pas être ajouté au payload final de synchronisation.

## Statuts de passage

```text
FAIT
NON_FAIT
ANOMALIE
```

`A_FAIRE` ne doit pas être présent dans le POST final.

`NON_FAIT` et `ANOMALIE` exigent un commentaire livreur.