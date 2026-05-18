# Routes API Expédition - contrat JSON v1.2 uniquement

Ce dossier décrit la version finale des routes Expédition côté API.

## Routes retenues

```http
GET /api/expedition/preparations/a-preparer
POST /api/expedition/preparations/verrouiller
```

Aucune autre route Expédition n'est nécessaire.

## GET /api/expedition/preparations/a-preparer

Cette route charge toutes les données préparables pour la date calculée côté API.

Règles :

- aucun `dateTournee` dans l'URL ;
- aucun `codeTournee` dans l'URL ;
- aucun `codeLivreur` dans l'URL ;
- la date préparable est calculée côté API ;
- la sélection de tournée reste côté application web Expédition ;
- si aucune tournée n'est trouvée, l'API répond `200 SUCCESS` avec `tournees: []`.

Exemple de réponse :

```json
{
  "statut": "SUCCESS",
  "schemaVersion": "1.2",
  "dateTournee": "2026-05-16",
  "datePreparable": "2026-05-16",
  "dateModifiable": false,
  "fuseauHoraireMetier": "Europe/Paris",
  "dateGenerationApi": "2026-05-15T08:00:00+02:00",
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
      "codeArticle": "TAPIS",
      "libelle": "Tapis",
      "typeQuantite": "LIVREE_PREVUE",
      "quantiteNullable": true,
      "ordreAffichage": 2
    },
    {
      "codeArticle": "SACS",
      "libelle": "Sacs",
      "typeQuantite": "LIVREE_PREVUE",
      "quantiteNullable": true,
      "ordreAffichage": 3
    }
  ],
  "tournees": [
    {
      "codeTournee": "4006",
      "libelleTournee": "TOURNEE EXEMPLE",
      "statutPreparationWeb": "PRETE_VERROUILLAGE",
      "lignes": [
        {
          "idLigneSource": "2026-05-16|4006|4|1058|1|1",
          "ordreArret": 1,
          "client": {
            "numClient": "1058",
            "nomClient": "CLIENT EXEMPLE",
            "nomAffiche": "CLIENT EXEMPLE"
          },
          "pointLivraison": {
            "codePDL": "1",
            "descriptionPDL": "ENTREE PRINCIPALE"
          },
          "infosLecture": {
            "horaire": "1",
            "codeTournee": "4006",
            "libelleTournee": "TOURNEE EXEMPLE",
            "instructions": "Livraison par l'arrière.",
            "estFerme": false,
            "dateFermeture": null,
            "motifFermeture": null,
            "zoneDechargement": null
          },
          "preparationInitiale": {
            "commentaireExceptionnel": null,
            "quantitesPrevues": [
              {
                "codeArticle": "ROLLS",
                "libelle": "Rolls",
                "quantiteLivreePrevue": null
              }
            ]
          }
        }
      ]
    }
  ],
  "regles": {
    "heureVerrouillageMetier": "00:05",
    "fuseauHoraireMetier": "Europe/Paris",
    "fenetreModification": "Les préparations sont modifiables avant le verrouillage automatique autour de 00:05.",
    "articlesAutorises": ["ROLLS", "SACS", "TAPIS"],
    "articlesInterdits": ["ROLLS_VIDES"],
    "exclureRollsVides": true,
    "quantitesNullesAutorisees": true
  }
}
```

## POST /api/expedition/preparations/verrouiller

Cette route reçoit le lot global final envoyé par le backend web Expédition.

Règles :

- `schemaVersion` doit être `"1.2"` ;
- `source` doit être `"APPLICATION_WEB_EXPEDITION"` ;
- `fuseauHoraireMetier` doit être `"Europe/Paris"` ;
- `dateVerrouillageDemandee` doit être au format ISO 8601 avec offset ;
- `tournees[]` doit contenir au moins une tournée ;
- les articles autorisés sont `ROLLS`, `TAPIS`, `SACS` ;
- `ROLLS_VIDES` est toujours refusé côté Expédition ;
- chaque `idLigneSource` doit exister dans les lignes préparables de la date ;
- le lot est idempotent via `idLotVerrouillage`.

Exemple de requête :

```json
{
  "schemaVersion": "1.2",
  "idLotVerrouillage": "EXP-2026-05-16-0005-001",
  "source": "APPLICATION_WEB_EXPEDITION",
  "dateTournee": "2026-05-16",
  "dateVerrouillageDemandee": "2026-05-16T00:05:00+02:00",
  "fuseauHoraireMetier": "Europe/Paris",
  "tournees": [
    {
      "codeTournee": "4006",
      "libelleTournee": "TOURNEE EXEMPLE",
      "statutPreparationWeb": "PRETE_VERROUILLAGE",
      "lignes": [
        {
          "idLigneSource": "2026-05-16|4006|4|1058|1|1",
          "ordreArret": 1,
          "client": {
            "numClient": "1058",
            "nomClient": "CLIENT EXEMPLE",
            "nomAffiche": "CLIENT EXEMPLE"
          },
          "pointLivraison": {
            "codePDL": "1",
            "descriptionPDL": "ENTREE PRINCIPALE"
          },
          "commentaireExceptionnel": "Prévoir passage avant 10h.",
          "derniereModification": {
            "date": "2026-05-15T17:32:00+02:00",
            "utilisateur": "EXPEDITION"
          },
          "quantitesPrevues": [
            {
              "codeArticle": "ROLLS",
              "quantiteLivreePrevue": 3
            },
            {
              "codeArticle": "TAPIS",
              "quantiteLivreePrevue": 1
            },
            {
              "codeArticle": "SACS",
              "quantiteLivreePrevue": 0
            }
          ]
        }
      ]
    }
  ]
}
```

Réponse succès :

```json
{
  "statut": "SUCCESS",
  "code": "EXPEDITION_LOT_LOCKED",
  "message": "Préparations Expédition sauvegardées et verrouillées avec succès.",
  "idLotVerrouillage": "EXP-2026-05-16-0005-001",
  "dateTournee": "2026-05-16",
  "statutVerrouillage": "VERROUILLEE_BD",
  "dateReceptionApi": "2026-05-16T00:05:03+02:00",
  "dateSauvegardeSql": "2026-05-16T00:05:04+02:00",
  "nombreTourneesVerrouillees": 1,
  "nombreLignesVerrouillees": 1
}
```

## Codes métier

- `SUCCESS` : traitement réussi.
- `ALREADY_PROCESSED` : même `idLotVerrouillage`, même contenu.
- `CONFLICT` : même `idLotVerrouillage`, contenu différent.
- `VALIDATION_ERROR` : JSON ou règle métier invalide.
- `ERROR` : erreur technique serveur.
