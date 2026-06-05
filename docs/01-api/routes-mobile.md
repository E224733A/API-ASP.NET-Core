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

Le champ optionnel `pointLivraison.lienAdresseLivraison` est ajouté uniquement dans le JSON de chargement des tournées mobile. Il ne change pas `schemaVersion`.

## Contrat POST synchronisation 1.3 strict

Le contrat mobile final pour `POST /api/synchronisations` est strictement en `schemaVersion` `1.3`.

```text
schemaVersion doit être exactement "1.3".
schemaVersion "1.2" est refusé.
Toute autre schemaVersion est refusée.
trajet est obligatoire.
trajet.camion est obligatoire.
trajet.camion.idCamion est obligatoire.
trajet.kilometrageDepart est obligatoire.
trajet.kilometrageArrivee est obligatoire.
trajet.dateDepartMobile est obligatoire.
trajet.dateArriveeMobile est obligatoire.
```

## GET /api/tournees/disponibles

### Rôle

Liste les tournées disponibles pour la date métier serveur et un livreur.

### Important : Pas de paramètre date dans l'URL

❌ **La date n'est pas acceptée dans l'URL.**
- Les paramètres `date` et `dateTournee` sont **explicitement refusés** par l'API.
- La date est toujours calculée côté serveur avec le fuseau horaire métier **Europe/Paris**.

### Exemple

```http
GET /api/tournees/disponibles?codeLivreur=2
```

### Paramètres

| Paramètre | Obligatoire | Rôle |
|---|---:|---|
| `codeLivreur` | Oui | Code livreur |
| `date` ou `dateTournee` | ❌ Interdit | Refusé - calculé côté API |

## GET /api/tournees/jour

### Rôle

Charge une tournée complète dans l'application mobile pour consultation et édition hors ligne.

### Important : Pas de paramètre date dans l'URL

❌ **La date n'est pas acceptée dans l'URL.**
- Les paramètres `date` et `dateTournee` sont **explicitement refusés** par l'API.
- La date est toujours calculée côté serveur avec le fuseau horaire métier **Europe/Paris**.

### Exemple

```http
GET /api/tournees/jour?codeTournee=2023&codeLivreur=2
```

### Paramètres

| Paramètre | Obligatoire | Rôle |
|---|---:|---|
| `codeTournee` | Oui | Code tournée |
| `codeLivreur` | Oui | Code livreur |
| `nomLivreur` | Non | Nom du livreur (complément informatif) |
| `date` ou `dateTournee` | ❌ Interdit | Refusé - calculé côté API |

### Règle avec Expédition

Le mobile doit lire uniquement des préparations Expédition verrouillées en SQL Server.

Une préparation encore en brouillon côté Web Expédition ne doit pas apparaître dans le chargement mobile.

### Champ optionnel pointLivraison.lienAdresseLivraison

`pointLivraison.lienAdresseLivraison` peut être renvoyé pour permettre au mobile d'afficher un bouton d'ouverture d'adresse.

Exemple :

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
    "lienAdresseLivraison": "https://www.google.com/maps/search/?api=1&query=Nantes"
  }
}
```

Règles :

```text
champ optionnel.
null si désactivé, absent ou invalide.
aucune erreur si CodePDL est vide.
aucune erreur si la source finale n'est pas disponible.
ne modifie pas schemaVersion.
ne modifie pas POST /api/synchronisations.
```

Configuration :

```json
{
  "LiensAdresseLivraison": {
    "Enabled": true,
    "Mode": "Hardcoded",
    "HardcodedUrl": "https://www.google.com/maps/search/?api=1&query=Nantes"
  }
}
```

Modes :

```text
Disabled   -> retourne toujours null.
Hardcoded  -> retourne l'URL de test configurée si CodePDL est présent.
Repository -> prépare la future source métier ABSSolute par CodePDL.
```

Source finale prévue en mode `Repository` :

```text
CodePDL
LienAdresseLivraison
EstActif
DateModification
```

La jointure métier prévue se fait par `CodePDL`.

## GET /api/camions/disponibles

### Rôle

Liste les camions disponibles pour permettre au mobile de proposer un choix camion avant le départ tournée.

### Exemple

```http
GET /api/camions/disponibles
```

### Paramètres

Aucun paramètre n'est nécessaire.

Les paramètres suivants sont explicitement refusés avec `HTTP 400` et `VALIDATION_ERROR` :

| Paramètre | Statut |
|---|---|
| `date` | Refusé |
| `dateTournee` | Refusé |

### Réponse

```json
{
  "schemaVersion": "1.3",
  "camions": [
    {
      "idCamion": "DY-662-QN",
      "codeCamion": "DY-662-QN",
      "libelleCamion": "VL RENAULT",
      "immatriculation": "DY-662-QN",
      "estActif": true
    }
  ]
}
```

### Règles de normalisation

```text
schemaVersion vaut toujours "1.3".
camions[] est toujours présent.
trim des chaînes.
les camions sans idCamion sont exclus.
estActif vaut true par défaut si la source ne fournit pas l'information.
tri stable par codeCamion, immatriculation, idCamion.
immatriculation et libelleCamion peuvent être null.
```

### Source SQL camion

Source SQL confirmée côté API :

```sql
[lavinprosli].[dbo].[v_Truck]
```

Mapping :

```text
CODE        -> idCamion, codeCamion, immatriculation
DESCRIPTION -> libelleCamion
```

## POST /api/synchronisations

### Rôle

Envoie le résultat final de la tournée au retour dépôt.

### Exemple

```http
POST /api/synchronisations
Content-Type: application/json
```

### Règles de validation existantes conservées

L'API refuse :

```text
schemaVersion manquant
schemaVersion différent de "1.3"
schemaVersion "1.2"
idSynchronisation manquant
idSynchronisation déjà reçu
dateTournee manquant
codeTournee manquant
livreur.codeLivreur manquant
mobile manquant
lignes[] vide
ligne sans idLigneSource
idLigneSource dupliqué dans la requête
saisie manquante
statutPassage manquant
A_FAIRE dans l'envoi final
estValidee = false dans l'envoi final
estValidee = true sans heureValidation
NON_FAIT sans commentaireLivreur
ANOMALIE sans commentaireLivreur
quantites[] vide
codeArticle dupliqué dans une ligne
quantiteLivree négative
quantiteRecuperee négative
quantiteLivreePrevue négative
```

### Section trajet obligatoire en 1.3

En `schemaVersion` `1.3`, le mobile doit envoyer le camion utilisé et les informations de kilométrage dans l'objet `trajet`.

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

### Règles de validation trajet

L'API refuse :

```text
schemaVersion différent de "1.3"
schemaVersion "1.2"
schemaVersion 1.3 sans trajet
schemaVersion 1.3 sans trajet.camion
schemaVersion 1.3 sans trajet.camion.idCamion
schemaVersion 1.3 sans trajet.kilometrageDepart
schemaVersion 1.3 sans trajet.kilometrageArrivee
schemaVersion 1.3 sans trajet.dateDepartMobile
schemaVersion 1.3 sans trajet.dateArriveeMobile
trajet.kilometrageDepart < 0
trajet.kilometrageArrivee < 0
trajet.kilometrageArrivee < trajet.kilometrageDepart
trajet.dateArriveeMobile < trajet.dateDepartMobile
```

L'API accepte :

```text
schemaVersion 1.3 avec trajet complet et cohérent
```

### Dates mobile et dates trajet

```text
mobile.dateChargementMobile -> trace le chargement technique de la tournée dans l'application mobile.
mobile.dateEnvoiMobile      -> trace l'envoi technique du payload final à l'API.
trajet.dateDepartMobile     -> correspond au départ métier après chargement de la tournée.
trajet.dateArriveeMobile    -> correspond à l'arrivée métier avant ou au moment de l'envoi final à l'API.
```

Ces champs sont séparés dans le contrat pour clarifier le métier.

## Anti-doublons

### Anti-doublon technique

```text
idSynchronisation
```

Objectif : empêcher le rejeu exact du même paquet mobile.

### Anti-doublon métier

```text
DateTournee + CodeTournee
```

Objectif : empêcher qu'une même tournée soit envoyée deux fois le même jour.

Le code livreur est conservé pour tracer qui a envoyé la tournée. Il ne permet pas de renvoyer une deuxième fois la même tournée le même jour.

## GET /api/synchronisations

### Rôle

Consulte les synchronisations reçues.

### Exemples

```http
GET /api/synchronisations
GET /api/synchronisations?dateTournee=2026-05-19&codeTournee=2023&codeLivreur=2
GET /api/synchronisations/1
```
