# Contrats JSON - Mobile

## Versions mobiles

Le projet utilise deux contrats mobiles distincts :

```text
GET /api/tournees/jour          -> schemaVersion inchangé, actuellement "1.2".
POST /api/synchronisations      -> schemaVersion strictement "1.3".
GET /api/camions/disponibles    -> schemaVersion "1.3".
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
CodePDL
AdresseLivraison
```

`AdresseLivraison` doit contenir un lien Google Maps déjà construit par la vue SQL. L'API ne construit plus de lien à partir de latitude/longitude et ne contient plus de mode Hardcoded.

Requête utilisée par l'API :

```sql
SELECT TOP (1)
    NULLIF(LTRIM(RTRIM(CAST(AdresseLivraison AS NVARCHAR(2048)))), N'') AS AdresseLivraison
FROM [lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
WHERE LTRIM(RTRIM(CAST(CodePDL AS NVARCHAR(100)))) = @CodePDL;
```

### Règles

```text
champ optionnel.
null si CodePDL vide.
null si CodePDL absent de la vue.
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

## POST /api/synchronisations

Le mobile envoie le résultat final de la tournée au retour dépôt.

La synchronisation reste strictement en `schemaVersion` `1.3` avec section `trajet` obligatoire.

Le champ `pointLivraison.lienAdresseLivraison` ne doit pas être ajouté au payload final de synchronisation.

## Statuts de passage

```text
FAIT
NON_FAIT
ANOMALIE
```

`A_FAIRE` ne doit pas être présent dans le POST final.
