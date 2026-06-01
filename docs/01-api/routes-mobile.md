# Routes API - Mobile

## Principe

Les routes mobile servent à :

- lister les tournées disponibles ;
- charger une tournée complète ;
- envoyer le retour de tournée ;
- consulter les synchronisations reçues.

Le mobile fonctionne hors connexion pendant la journée.

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

## POST /api/synchronisations

### Rôle

Envoie le résultat final de la tournée au retour dépôt.

### Exemple

```http
POST /api/synchronisations
Content-Type: application/json
```

### Règles de validation

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
