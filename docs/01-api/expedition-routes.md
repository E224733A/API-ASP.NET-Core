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

Règles importantes :

- aucun paramètre `dateTournee` n'est attendu dans l'URL ;
- aucun paramètre `codeLivreur` n'est attendu ;
- aucun filtre obligatoire par tournée n'est demandé au GET global ;
- la sélection de tournée reste côté web après chargement complet ;
- le résultat contient toutes les lignes préparables pour la date calculée.

L'application web doit ensuite stocker le brouillon dans SQLite local et ne pas rappeler l'API à chaque modification.

## 2. Verrouiller et sauvegarder les préparations

```http
POST /api/expedition/preparations/verrouiller
```

Rôle :

- recevoir un lot complet de préparations envoyé automatiquement par le backend web Expédition autour de 00:05 ;
- contrôler la date métier côté API ;
- contrôler `idLotVerrouillage`, `idLigneSource`, articles, quantités et commentaires ;
- refuser les rolls vides préparés côté Expédition ;
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
  "codeTournee": "4006",
  "libelleTournee": "Tournée 4006",
  "utilisateur": {
    "identifiant": "expedition",
    "nomAffiche": "Service expédition"
  },
  "lignes": [
    {
      "idLigneSource": "2026-05-16|4006|6|12345|PDL01|10",
      "ordreArret": 10,
      "numClient": "12345",
      "nomClient": "CLIENT TEST",
      "codePDL": "PDL01",
      "descriptionPDL": "Accueil principal",
      "commentaireExceptionnel": "Prévoir passage avant 10h.",
      "quantites": [
        {
          "codeArticle": "ROLLS",
          "libelle": "Rolls",
          "quantiteLivreePrevue": 3
        },
        {
          "codeArticle": "TAPIS",
          "libelle": "Tapis",
          "quantiteLivreePrevue": null
        },
        {
          "codeArticle": "SACS",
          "libelle": "Sacs",
          "quantiteLivreePrevue": 0
        }
      ]
    }
  ]
}
```

## Codes de réponse principaux

- `200 SUCCESS` : lot verrouillé ou déjà traité avec succès.
- `400 VALIDATION_ERROR` : JSON invalide, quantité négative, article interdit, ligne invalide.
- `404 NOT_FOUND` : aucune tournée à préparer trouvée pour le GET.
- `409 CONFLICT` : tournée déjà verrouillée ou conflit d'idempotence.
- `500 ERROR` : erreur technique serveur ou SQL.
