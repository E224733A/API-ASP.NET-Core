# Routes API Expédition

Ce module ajoute uniquement deux routes API Expédition.

## 1. Charger toutes les données à préparer

```http
GET /api/expedition/preparations/a-preparer
```

Rôle :

- calculer côté API la prochaine date préparable ;
- charger toutes les tournées de cette date ;
- charger les clients, PDL, adresses, instructions, fermetures et articles préparables ;
- générer les `idLigneSource` ;
- renvoyer un JSON complet à l'application web Expédition.

L'application web doit ensuite stocker le brouillon dans SQLite local et ne pas rappeler l'API à chaque modification.

## 2. Verrouiller et sauvegarder les préparations

```http
POST /api/expedition/preparations/verrouiller
```

Rôle :

- recevoir un lot complet de préparations envoyé automatiquement par le backend web Expédition autour de 00:05 ;
- contrôler la date métier côté API ;
- contrôler `idLotVerrouillage`, `idLigneSource`, articles, quantités et commentaires ;
- refuser les rolls vides récupérés côté Expédition ;
- sauvegarder définitivement dans SQL Server ;
- verrouiller les préparations ;
- tracer le traitement.

## Règle de responsabilité

```text
Backend web Expédition déclenche.
API centrale vérifie.
SQL Server trace.
```

## Exemple de POST

```json
{
  "schemaVersion": "1.0",
  "idLotVerrouillage": "11111111-1111-1111-1111-111111111111",
  "dateTournee": "2026-05-16",
  "dateDeclenchementWeb": "2026-05-16T00:05:00+02:00",
  "fuseauHoraireWeb": "Europe/Paris",
  "tournees": [
    {
      "codeTournee": "4006",
      "libelleTournee": "Tournée 4006",
      "statutPreparation": "PRETE_VERROUILLAGE",
      "lignes": [
        {
          "idLigneSource": "2026-05-16|4006|6|12345|PDL01|10",
          "ordreArret": 10,
          "numClient": "12345",
          "nomClient": "CLIENT TEST",
          "codePDL": "PDL01",
          "descriptionPDL": "Accueil principal",
          "commentaireExceptionnel": "Prévoir passage avant 10h.",
          "heureValidation": "2026-05-15T17:42:00+02:00",
          "quantites": [
            {
              "codeArticle": "ROLLS",
              "libelleArticle": "Rolls",
              "quantiteLivreePrevue": 3
            },
            {
              "codeArticle": "TAPIS",
              "libelleArticle": "Tapis",
              "quantiteLivreePrevue": null
            },
            {
              "codeArticle": "SACS",
              "libelleArticle": "Sacs",
              "quantiteLivreePrevue": 0
            }
          ]
        }
      ]
    }
  ]
}
```

## Codes de réponse principaux

- `200 SUCCESS` : lot verrouillé ou déjà traité avec succès.
- `400 VALIDATION_ERROR` : JSON invalide, date incohérente, quantité négative, article interdit, ligne inconnue.
- `409 CONFLICT` : tournée déjà verrouillée ou conflit d'idempotence.
- `500 ERROR` : erreur technique serveur ou SQL.
