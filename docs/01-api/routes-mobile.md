# Routes API - Mobile

## Principe

Les routes mobile servent à :

- lister les tournées disponibles pour la date métier serveur ;
- charger une tournée complète ;
- lister les camions disponibles ;
- envoyer le retour de tournée ;
- consulter les synchronisations reçues si la route de consultation est disponible dans le code courant.

Le mobile fonctionne hors connexion pendant la journée.

## Base URL validée

```text
Production HTTPS : https://srvapi1.sli.local
Développement local : http://127.0.0.1:5000
```

## Versions de contrat

```text
GET /api/tournees/disponibles -> schemaVersion inchangé, actuellement "1.2".
GET /api/tournees/jour       -> schemaVersion inchangé, actuellement "1.2".
GET /api/camions/disponibles -> schemaVersion "1.3".
POST /api/synchronisations   -> schemaVersion strictement "1.3".
```

`schemaVersion = "1.2"` reste valable pour le chargement mobile du matin, mais il est refusé pour le POST final de synchronisation.

## Règle commune sur les dates

Les routes mobiles de lecture refusent les paramètres `date` et `dateTournee`.

La date métier est calculée côté API avec le fuseau Europe/Paris.

## GET /api/tournees/disponibles

### Rôle

Liste les tournées disponibles pour le livreur et la date métier serveur.

### Exemple

```http
GET /api/tournees/disponibles?codeLivreur=2
```

### Paramètres

| Paramètre | Obligatoire | Rôle |
|---|---:|---|
| `codeLivreur` | Oui | Code livreur |
| `date` ou `dateTournee` | Interdit | Refusé - calculé côté API |

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
NUM_CLI
CodePDL
AdresseLivraison
```

`AdresseLivraison` doit contenir un lien Google Maps déjà construit par la vue SQL.

Règles :

```text
champ optionnel.
recherche par NUM_CLI + CodePDL.
null si NUM_CLI vide.
null si CodePDL vide.
null si le couple NUM_CLI + CodePDL est absent de la vue.
null si AdresseLivraison est vide ou invalide.
ne modifie pas schemaVersion.
ne modifie pas POST /api/synchronisations.
pas de mode Hardcoded en version finale.
```

## GET /api/camions/disponibles

```http
GET /api/camions/disponibles
```

Retourne les camions disponibles en `schemaVersion` `1.3`.

Paramètres interdits :

```text
date
dateTournee
```

Source SQL confirmée côté repository :

```sql
[lavinprosli].[dbo].[v_Truck]
```

Mapping retenu :

```text
CODE        -> idCamion
CODE        -> codeCamion
DESCRIPTION -> libelleCamion
CODE        -> immatriculation
EstActif    -> true par défaut
```

## POST /api/synchronisations

```http
POST /api/synchronisations
Content-Type: application/json
```

Le contrat est strictement en `schemaVersion` `1.3` avec section `trajet` obligatoire.

Champs obligatoires dans `trajet` :

```text
trajet.camion.idCamion
trajet.kilometrageDepart
trajet.kilometrageArrivee
trajet.dateDepartMobile
trajet.dateArriveeMobile
```

Règles métier principales :

```text
kilometrageDepart >= 0.
kilometrageArrivee >= 0.
kilometrageArrivee >= kilometrageDepart.
dateArriveeMobile >= dateDepartMobile.
A_FAIRE interdit dans l'envoi final.
NON_FAIT et ANOMALIE exigent un commentaire livreur.
Chaque ligne envoyée doit être validée.
Chaque ligne doit contenir au moins une quantité.
```

Le champ `lienAdresseLivraison` ne fait pas partie du payload final de synchronisation.

Les données de trajet sont sauvegardées dans `dbo.Mobile_TourneeCamion` dans la même transaction SQL que la synchronisation mobile.