# Routes API - Mobile

## Principe

Les routes mobile servent à :

- lister les tournées disponibles ;
- charger une tournée complète ;
- lister les camions disponibles ;
- envoyer le retour de tournée ;
- consulter les synchronisations reçues.

Le mobile fonctionne hors connexion pendant la journée.

## Transition 1.2 -> 1.3

Cette documentation prépare l'ajout du choix camion et des kilométrages avant codage.

Règles de transition prévues :

```text
schemaVersion 1.2 -> accepté temporairement avec trajet optionnel
schemaVersion 1.3 -> accepté avec trajet obligatoire lorsque la migration sera codée
```

La route `GET /api/camions/disponibles` et la section `trajet` de `POST /api/synchronisations` sont documentées comme contrat cible. La migration 1.3 n'est pas présentée comme terminée tant que le code API, les scripts SQL et le mobile MAUI n'ont pas été modifiés et testés.

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

## GET /api/camions/disponibles

### Rôle

Liste les camions disponibles pour permettre au mobile de proposer un choix camion avant le départ tournée.

Cette route est une nouvelle route à ajouter lors du développement de la migration camion / kilométrages.

### Exemple

```http
GET /api/camions/disponibles
```

### Paramètres

Aucun paramètre obligatoire documenté à ce stade.

### Réponse attendue

```json
{
  "schemaVersion": "1.2",
  "camions": [
    {
      "idCamion": "12",
      "codeCamion": "12",
      "libelleCamion": "Camion 12",
      "immatriculation": "AB-123-CD",
      "estActif": true
    }
  ]
}
```

### Règles prévues

```text
schemaVersion obligatoire dans la réponse
camions[] présent dans la réponse
idCamion obligatoire pour chaque camion retourné
codeCamion affichable dans le mobile
libelleCamion affichable dans le mobile
immatriculation affichable si disponible
estActif indique si le camion peut être proposé
```

## POST /api/synchronisations

### Rôle

Envoie le résultat final de la tournée au retour dépôt.

### Exemple

```http
POST /api/synchronisations
Content-Type: application/json
```

### Règles de validation existantes

L'API refuse :

```text
schemaVersion manquant
schemaVersion non supporté
idSynchronisation manquant
idSynchronisation déjà reçu
dateTournee manquant
codeTournee manquant
livreur.codeLivreur manquant
mobile manquant
lignes[] vide
ligne sans idLigneSource
idLigneSource dupliqué dans la requête
client manquant
pointLivraison manquant
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

### Section trajet cible 1.3

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

### Règles de validation trajet à ajouter

L'API devra refuser :

```text
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

L'API devra accepter temporairement :

```text
schemaVersion 1.2 sans trajet
schemaVersion 1.2 avec trajet optionnel
schemaVersion 1.3 avec trajet complet et cohérent
```

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
