# Matrice de tests API Mobile SLI

## Couverture lienAdresseLivraison final

Le champ `pointLivraison.lienAdresseLivraison` est alimenté depuis la vue SQL finale :

```sql
[lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
```

Colonnes attendues :

```text
CodePDL
AdresseLivraison
```

| ID | Scenario | Script | Type | HTTP attendu | Attendu |
|---|---|---|---|---:|---|
| MOB-API-LIEN-001 | GET tournée avec vue contenant des liens | scripts/test-lien-adresse-livraison.ps1 | Valide | 200 | schemaVersion inchangé, JSON parseable, au moins un lienAdresseLivraison non vide et URL absolue |
| MOB-API-LIEN-002 | GET tournée avec CodePDL absent de la vue | scripts/test-lien-adresse-livraison.ps1 | Valide | 200 | schemaVersion inchangé, JSON parseable, lienAdresseLivraison null sur les lignes non trouvées |

Commande type :

```powershell
.\docs\04-tests\Mobile\scripts\test-lien-adresse-livraison.ps1 `
  -ApiBaseUrl "http://srvapi1.sli.local:5000" `
  -CodeLivreur "2" `
  -CodeTournee "5001" `
  -ExpectedSchemaVersion "1.2" `
  -ExpectLienAdresseLivraison
```

Le POST `/api/synchronisations` reste couvert par la matrice mobile 1.3 existante.
