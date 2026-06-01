# Routes API - Expédition

## Principe

Le module Expédition utilise deux routes uniquement :

```http
GET  /api/expedition/preparations/a-preparer
POST /api/expedition/preparations/verrouiller
```

Le GET est global.

Le choix de la tournée se fait côté application Web Expédition après chargement complet.

L'API ne reçoit pas de filtre `dateTournee`, `codeTournee` ou `codeLivreur` sur le GET Expédition.

## GET /api/expedition/preparations/a-preparer

### Rôle

Retourne toutes les préparations à préparer pour la date préparable calculée côté API.

La date préparable est le **prochain jour ouvré métier** (lundi à vendredi).

### Règle de calcul de la date préparable

```text
Jour courant  →  Date préparable
lundi         →  mardi
mardi         →  mercredi
mercredi      →  jeudi
jeudi         →  vendredi
vendredi      →  lundi (samedi et dimanche ignorés)
samedi        →  lundi
dimanche      →  lundi
```

### Paramètres

Aucun paramètre accepté.

### Paramètres interdits

| Paramètre | Résultat attendu |
|---|---:|
| `dateTournee` | `400 Bad Request` |
| `codeTournee` | `400 Bad Request` |
| `codeLivreur` | `400 Bad Request` |

### Exemple interdit

```http
GET /api/expedition/preparations/a-preparer?dateTournee=2026-05-19
```

Réponse attendue :

```http
400 Bad Request
```

Code métier attendu :

```text
EXPEDITION_GET_QUERY_PARAMS_FORBIDDEN
```

### Règle métier

Le GET Expédition est global.

Le choix de la tournée se fait côté application Web Expédition.

La route ne doit pas être transformée en route filtrée.

## POST /api/expedition/preparations/verrouiller

### Rôle

Reçoit un lot global de préparations Expédition et le verrouille dans SQL Server.

Après ce verrouillage, les données deviennent exploitables par le mobile.

### Corps JSON attendu

```json
{
  "schemaVersion": "1.2",
  "idLotVerrouillage": "EXP-2026-05-19-0005",
  "source": "APPLICATION_WEB_EXPEDITION",
  "dateTournee": "2026-05-19",
  "dateVerrouillageDemandee": "2026-05-19T22:35:00+02:00",
  "fuseauHoraireMetier": "Europe/Paris",
  "tournees": []
}
```

### Règles de validation

L'API refuse :

```text
schemaVersion différent de 1.2
idLotVerrouillage manquant
source différente de APPLICATION_WEB_EXPEDITION
dateTournee invalide
dateTournee différente de la date préparable calculée par l'API
dateVerrouillageDemandee invalide
dateVerrouillageDemandee sans offset horaire explicite
fuseauHoraireMetier différent de Europe/Paris
tournees[] vide
codeTournee manquant
idLigneSource manquant
idLigneSource dupliqué dans une tournée
idLigneSource présent dans plusieurs tournées du lot
idLigneSource inexistant dans les lignes préparables
client.numClient manquant
codeArticle manquant
codeArticle dupliqué dans une ligne
article différent de ROLLS, ROLLS_VIDES, TAPIS ou SACS
quantiteLivreePrevue négative
```

### Articles autorisés

```text
ROLLS
ROLLS_VIDES
TAPIS
SACS
```

### Statuts de préparation Web acceptés

```text
PRETE_VERROUILLAGE
EN_PREPARATION_WEB
```

### Idempotence

Le POST est idempotent avec `idLotVerrouillage`.

Même `idLotVerrouillage` et même contenu :

```http
200 OK
```

Code métier :

```text
ALREADY_PROCESSED
```

Même `idLotVerrouillage` mais contenu différent :

```http
409 Conflict
```

Code métier :

```text
EXPEDITION_LOT_PAYLOAD_MISMATCH
```

### Réponse succès

```json
{
  "statut": "SUCCESS",
  "code": "EXPEDITION_LOT_LOCKED",
  "message": "Préparations Expédition sauvegardées et verrouillées avec succès.",
  "idLotVerrouillage": "EXP-2026-05-19-0005",
  "dateTournee": "2026-05-19",
  "statutVerrouillage": "VERROUILLEE_BD",
  "dateReceptionApi": "2026-05-19T22:35:01+02:00",
  "dateSauvegardeSql": "2026-05-19T22:35:01+02:00",
  "nombreTourneesVerrouillees": 1,
  "nombreLignesVerrouillees": 4
}
```
