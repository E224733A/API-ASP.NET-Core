# Contrats JSON - Expédition

## Version

```json
{
  "schemaVersion": "1.2"
}
```

## GET /api/expedition/preparations/a-preparer

### Structure générale

```json
{
  "statut": "SUCCESS",
  "schemaVersion": "1.2",
  "dateTournee": "2026-05-19",
  "datePreparable": "2026-05-19",
  "dateModifiable": false,
  "fuseauHoraireMetier": "Europe/Paris",
  "dateGenerationApi": "2026-05-18T14:00:00+02:00",
  "message": null,
  "articlesPreparables": [
    {
      "codeArticle": "ROLLS",
      "libelle": "Rolls",
      "typeQuantite": "LIVREE_PREVUE",
      "quantiteNullable": true,
      "ordreAffichage": 1
    },
    {
      "codeArticle": "ROLLS_VIDES",
      "libelle": "Rolls Vides",
      "typeQuantite": "LIVREE_PREVUE",
      "quantiteNullable": true,
      "ordreAffichage": 2
    },
    {
      "codeArticle": "TAPIS",
      "libelle": "Tapis",
      "typeQuantite": "LIVREE_PREVUE",
      "quantiteNullable": true,
      "ordreAffichage": 3
    },
    {
      "codeArticle": "SACS",
      "libelle": "Sacs",
      "typeQuantite": "LIVREE_PREVUE",
      "quantiteNullable": true,
      "ordreAffichage": 4
    }
  ],
  "tournees": [],
  "regles": {
    "heureVerrouillageMetier": "22:35",
    "fuseauHoraireMetier": "Europe/Paris",
    "fenetreModification": "Les préparations sont modifiables avant le verrouillage automatique entre 22:35 et 22:55.",
    "articlesAutorises": ["ROLLS", "ROLLS_VIDES", "TAPIS", "SACS"],
    "articlesInterdits": [],
    "exclureRollsVides": false,
    "quantitesNullesAutorisees": true
  }
}
```

## Tournée Expédition

```json
{
  "codeTournee": "2023",
  "libelleTournee": "CLISSON",
  "statutPreparationWeb": "PRETE_VERROUILLAGE",
  "lignes": []
}
```

## Ligne Expédition

```json
{
  "idLigneSource": "2026-05-19|2023|2|12866|2866|0",
  "ordreArret": 0,
  "client": {
    "numClient": "12866",
    "nomClient": "LOGISTIQUE SPORT - VILLE CLISS",
    "nomAffiche": "LOGISTIQUE SPORT - VILLE CLISS"
  },
  "pointLivraison": {
    "codePDL": "2866",
    "descriptionPDL": "LOGISTIQUE SPORT - VILLE CLISS",
    "adresseLigne1": "CENTRE TECHNIQUE MUNICIPAL",
    "adresseLigne2": "10 RUE DES FILATURES",
    "adresseLigne3": null,
    "ville": "CLISSON",
    "codePostal": "44190"
  },
  "infosLecture": {
    "horaire": "0",
    "codeTournee": "2023",
    "libelleTournee": "CLISSON",
    "instructions": null,
    "estFerme": false,
    "dateFermeture": null,
    "motifFermeture": null,
    "zoneDechargement": "VTS"
  },
  "preparationInitiale": {
    "commentaireExceptionnel": null,
    "quantitesPrevues": [
      {
        "codeArticle": "ROLLS",
        "libelle": "Rolls",
        "quantiteLivreePrevue": null
      },
      {
        "codeArticle": "TAPIS",
        "libelle": "Tapis",
        "quantiteLivreePrevue": null
      },
      {
        "codeArticle": "SACS",
        "libelle": "Sacs",
        "quantiteLivreePrevue": null
      }
    ]
  }
}
```

## Sens de quantiteLivreePrevue

```text
null -> l'Expédition n'a rien renseigné
0    -> l'Expédition a volontairement prévu zéro
> 0  -> quantité prévue
< 0  -> interdit
```

## POST /api/expedition/preparations/verrouiller

### Structure

```json
{
  "schemaVersion": "1.2",
  "idLotVerrouillage": "EXP-2026-05-19-0005",
  "source": "APPLICATION_WEB_EXPEDITION",
  "dateTournee": "2026-05-19",
  "dateVerrouillageDemandee": "2026-05-19T22:35:00+02:00",
  "fuseauHoraireMetier": "Europe/Paris",
  "tournees": [
    {
      "codeTournee": "2023",
      "libelleTournee": "CLISSON",
      "statutPreparationWeb": "PRETE_VERROUILLAGE",
      "dateModification": "2026-05-19T22:30:00+02:00",
      "lignes": [
        {
          "idLigneSource": "2026-05-19|2023|2|12866|2866|0",
          "ordreArret": 0,
          "client": {
            "numClient": "12866",
            "nomClient": "LOGISTIQUE SPORT - VILLE CLISS",
            "nomAffiche": "LOGISTIQUE SPORT - VILLE CLISS"
          },
          "pointLivraison": {
            "codePDL": "2866",
            "descriptionPDL": "LOGISTIQUE SPORT - VILLE CLISS",
            "adresseLigne1": "CENTRE TECHNIQUE MUNICIPAL",
            "adresseLigne2": "10 RUE DES FILATURES",
            "adresseLigne3": null,
            "ville": "CLISSON",
            "codePostal": "44190"
          },
          "commentaireExceptionnel": null,
          "derniereModification": {
            "date": "2026-05-18T15:00:00+02:00",
            "utilisateur": "EXPEDITION"
          },
          "quantitesPrevues": [
            {
              "codeArticle": "ROLLS",
              "quantiteLivreePrevue": 2
            },
            {
              "codeArticle": "ROLLS_VIDES",
              "quantiteLivreePrevue": 1
            },
            {
              "codeArticle": "TAPIS",
              "quantiteLivreePrevue": 0
            },
            {
              "codeArticle": "SACS",
              "quantiteLivreePrevue": null
            }
          ]
        }
      ]
    }
  ]
}
```

## Règles de compatibilité

Les champs `quantitesPrevues[]` permettent d'ajouter ou retirer des articles sans modifier la structure globale du JSON.

Les articles côté Expédition sont limités à :

```text
ROLLS
ROLLS_VIDES
TAPIS
SACS
```

Les valeurs `null` doivent rester visibles dans le JSON. Elles ne doivent pas être supprimées par la sérialisation.

Le champ `dateModification` doit être au format ISO 8601 avec offset horaire explicite (+02:00).
Il correspond à l'heure du dernier clic "Marquer prête pour verrouillage" côté Web Expédition.
