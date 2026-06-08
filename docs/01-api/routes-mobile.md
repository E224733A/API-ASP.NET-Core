# Routes API - Mobile

## Principe

Les routes mobile servent à :

- lister les tournées disponibles ;
- charger une tournée complète ;
- lister les camions disponibles ;
- envoyer le retour de tournée ;
- consulter les synchronisations reçues.

Le mobile fonctionne hors connexion pendant la journée.

## Versions de contrat

```text
GET /api/tournees/jour       -> schemaVersion inchangé, actuellement "1.2".
POST /api/synchronisations   -> schemaVersion strictement "1.3".
GET /api/camions/disponibles -> schemaVersion "1.3".
```

## GET /api/tournees/jour

### Rôle

Charge une tournée complète dans l'application mobile pour consultation et édition hors ligne.

### Exemple

```http
GET /api/tournees/jour?codeTournee=5001&codeLivreur=2
```

### Paramètres

| Paramètre | Obligatoire | Rôle |
|---|---:|---|
| `codeTournee` | Oui | Code tournée |
| `codeLivreur` | Oui | Code livreur |
| `nomLivreur` | Non | Nom du livreur |
| `date` ou `dateTournee` | Interdit | Refusé - calculé côté API |

### Champ optionnel pointLivraison.lienAdresseLivraison

`pointLivraison.lienAdresseLivraison` peut être renvoyé pour permettre au mobile d'afficher un bouton d'ouverture de l'adresse dans Maps.

La source SQL finale imposée côté API est :

```sql
[lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
```

Colonnes attendues :

```text
CodePDL
AdresseLivraison
```

`AdresseLivraison` doit contenir un lien Google Maps déjà construit par la vue SQL.

Règles :

```text
champ optionnel.
null si CodePDL vide, absent de la vue, ou URL invalide.
ne modifie pas schemaVersion.
ne modifie pas POST /api/synchronisations.
pas de mode Hardcoded en version finale.
```

## GET /api/camions/disponibles

```http
GET /api/camions/disponibles
```

Retourne les camions disponibles en `schemaVersion` `1.3`.

## POST /api/synchronisations

```http
POST /api/synchronisations
Content-Type: application/json
```

Le contrat reste strictement en `schemaVersion` `1.3` avec section `trajet` obligatoire.

Le champ `lienAdresseLivraison` ne fait pas partie du payload final de synchronisation.
