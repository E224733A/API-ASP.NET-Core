# Erreurs API

## Codes HTTP

| Code | Signification |
|---:|---|
| `200` | Requête réussie |
| `400` | Données invalides |
| `404` | Ressource introuvable |
| `409` | Conflit métier ou technique |
| `500` | Erreur technique serveur |

## Erreurs Mobile

### DATE_QUERY_PARAM_INTERDIT

```http
400 Bad Request
```

Signifie que la requête GET mobile a inclus un paramètre `date` ou `dateTournee` dans l'URL, ce qui est explicitement interdit.

La date est toujours calculée côté API avec le fuseau horaire **Europe/Paris**.

### VALIDATION_ERROR

```http
400 Bad Request
```

Signifie que la requête mobile ne respecte pas le contrat JSON ou les règles métier.

Exemples :

```text
schemaVersion absent ou différent de 1.3 sur POST /api/synchronisations.
schemaVersion 1.2 envoyé sur POST /api/synchronisations.
trajet manquant.
camion manquant.
idCamion manquant.
kilométrage manquant ou négatif.
kilometrageArrivee inférieur à kilometrageDepart.
dateDepartMobile ou dateArriveeMobile manquante.
A_FAIRE envoyé dans l'envoi final.
NON_FAIT ou ANOMALIE sans commentaire livreur.
```

### DATE_TOURNEE_EXPIREE

```http
409 Conflict
```

Signifie que la date de tournée envoyée par le mobile est antérieure à la date métier actuellement autorisée par l'API.

La réponse contient notamment :

```text
success = false
statut = CONFLICT
code = DATE_TOURNEE_EXPIREE
dateTourneePayload
dateTourneeAutorisee
```

### DATE_TOURNEE_NON_AUTORISEE

```http
409 Conflict
```

Signifie que la date de tournée envoyée par le mobile ne correspond pas à la date métier autorisée par l'API.

La réponse contient notamment :

```text
success = false
statut = CONFLICT
code = DATE_TOURNEE_NON_AUTORISEE
dateTourneePayload
dateTourneeAutorisee
```

### SYNCHRONISATION_ALREADY_EXISTS

```http
409 Conflict
```

Signifie que le même `idSynchronisation` a déjà été reçu.

### TOURNEE_ALREADY_SENT

```http
409 Conflict
```

Signifie qu'une tournée a déjà été envoyée pour le même couple :

```text
DateTournee + CodeTournee
```

Le `CodeLivreur` sert à tracer qui a envoyé la tournée, mais il ne permet pas d'envoyer une deuxième fois la même tournée le même jour.

## Erreurs Expédition

### EXPEDITION_GET_QUERY_PARAMS_FORBIDDEN

```http
400 Bad Request
```

Signifie que le GET Expédition a reçu un paramètre de requête interdit.

La route suivante ne doit recevoir aucun paramètre :

```http
GET /api/expedition/preparations/a-preparer
```

### EXPEDITION_VALIDATION_ERROR

```http
400 Bad Request
```

Signifie que le lot de verrouillage Expédition contient des données invalides.

### ALREADY_PROCESSED

```http
200 OK
```

Signifie que le même lot Expédition a déjà été traité avec le même contenu.

Ce n'est pas une erreur bloquante.

### EXPEDITION_LOT_PAYLOAD_MISMATCH

```http
409 Conflict
```

Signifie que le même `idLotVerrouillage` a déjà été utilisé avec un contenu différent.

### EXPEDITION_LOT_LOCKED

```http
200 OK
```

Signifie que les préparations Expédition ont été sauvegardées et verrouillées avec succès.

## Erreur technique

### SERVER_ERROR

```http
500 Internal Server Error
```

Signifie qu'une exception technique est survenue côté serveur.

Exemples :

```text
problème SQL Server
erreur de configuration
exception non gérée
```