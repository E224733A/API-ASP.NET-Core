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

### VALIDATION_ERROR

```http
400 Bad Request
```

Signifie que la requête mobile ne respecte pas le contrat JSON ou les règles métier.

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
