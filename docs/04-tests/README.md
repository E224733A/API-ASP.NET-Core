# Tests API mobile — contrat JSON v1.1

Date de mise à jour : 2026-05-05  
URL locale utilisée : `http://localhost:5120`

Ce dossier contient les tests métier minimaux pour vérifier les routes principales de l’API mobile :

- `GET /api/tournees/jour` : chargement réel d’une tournée depuis les vues ABSSolute ;
- `POST /api/synchronisations` : envoi final d’une tournée renseignée par le mobile ;
- `GET /api/synchronisations` : consultation des synchronisations envoyées ;
- `GET /api/synchronisations/{idTourneeMobile}` : détail d’une synchronisation envoyée.

Le contrat JSON utilisé est uniquement la version `1.1` avec le tableau `saisie.quantites[]`.

---

## 1. Préconditions

Avant de lancer les tests :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"
dotnet build
dotnet run
```

Dans un second terminal PowerShell :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core\docs\04-tests"
$api = "http://localhost:5120"
```

Important : les tests `POST` utilisent des `idSynchronisation` fixes. Pour obtenir exactement les résultats attendus `SUCCESS` puis `CONFLICT`, il faut les lancer dans l’ordre indiqué, de préférence sur une base de test réinitialisée.

Si un fichier JSON a déjà été envoyé avec succès, un nouvel envoi du même fichier doit retourner `409 Conflict`. C’est normal : l’API bloque les doubles envois.

---

## 2. Logique métier vérifiée

### Chargement du matin

Le livreur s’identifie par `codeLivreur`. La date est affichée mais non modifiable côté mobile. Le livreur choisit sa tournée. L’API récupère les lignes de la tournée depuis les vues ABSSolute et renvoie une structure exploitable par l’application mobile.

La réponse attendue contient notamment :

```text
schemaVersion = 1.1
dateModifiable = false
livreur.codeLivreur
livreur.nomLivreur
articlesSaisissables
lignes[]
saisie.quantites[]
```

### Envoi du soir

Une tournée envoyée est verrouillée côté mobile. Le livreur ne doit plus pouvoir la modifier après synchronisation réussie.

L’API doit refuser :

```text
- le même idSynchronisation envoyé deux fois ;
- la même DateTournee + CodeTournee + CodeLivreur envoyée deux fois ;
- une quantité négative ;
- un statut NON_FAIT sans commentaire ;
- un statut ANOMALIE sans commentaire ;
- une ligne validée sans heureValidation ;
- une ligne non validée dans l’envoi final ;
- un statut A_FAIRE dans l’envoi final ;
- un idLigneSource dupliqué dans la même requête ;
- un codeArticle dupliqué dans une même ligne ;
- une version de schéma non supportée ;
- un tableau quantites vide.
```

La tournée peut être recommencée la semaine suivante, car l’anti-doublon métier utilise la date :

```text
DateTournee + CodeTournee + IdLivreur
```

Donc ceci est interdit :

```text
2026-04-28 + 2001 + livreur 2
2026-04-28 + 2001 + livreur 2
```

Mais ceci est autorisé :

```text
2026-04-28 + 2001 + livreur 2
2026-05-05 + 2001 + livreur 2
```

Une tournée déjà envoyée ne peut pas être modifiée côté mobile.
Une nouvelle occurrence de la même tournée est possible uniquement avec une nouvelle date de tournée.

---

## 3. Fichiers JSON utilisés

| Fichier | Rôle | Résultat attendu |
|---|---|---|
| `sync-valide.json` | Envoi valide minimal | `200 OK / SUCCESS` |
| `sync-doublon.json` | Même `idSynchronisation` que `sync-valide.json` | `409 Conflict / CONFLICT` |
| `sync-double-envoi-tournee.json` | Même date + tournée + livreur, mais autre `idSynchronisation` | `409 Conflict / CONFLICT` |
| `sync-quantite-negative.json` | Quantité livrée négative | `400 Bad Request / VALIDATION_ERROR` |
| `sync-non-fait-sans-commentaire.json` | `NON_FAIT` sans commentaire | `400 Bad Request / VALIDATION_ERROR` |
| `sync-anomalie-sans-commentaire.json` | `ANOMALIE` sans commentaire | `400 Bad Request / VALIDATION_ERROR` |
| `sync-validee-sans-heure.json` | `estValidee = true` sans `heureValidation` | `400 Bad Request / VALIDATION_ERROR` |
| `sync-est-validee-false.json` | Ligne non validée dans l’envoi final | `400 Bad Request / VALIDATION_ERROR` |
| `sync-a-faire.json` | Statut `A_FAIRE` dans l’envoi final | `400 Bad Request / VALIDATION_ERROR` |
| `sync-idligne-duplique.json` | Même `idLigneSource` deux fois dans la requête | `400 Bad Request / VALIDATION_ERROR` |
| `sync-code-article-duplique.json` | Même `codeArticle` deux fois dans une ligne | `400 Bad Request / VALIDATION_ERROR` |
| `sync-schema-version-invalide.json` | Version `schemaVersion` non supportée | `400 Bad Request / VALIDATION_ERROR` |
| `sync-quantites-vide.json` | Tableau `quantites` vide | `400 Bad Request / VALIDATION_ERROR` |
| `get-tournee-reelle-1001.json` | Réponse réelle sauvegardée du GET | vérification manuelle |
| `sync-reel-1001.json` | Envoi réel construit à partir du GET | `200 OK / SUCCESS` si pas déjà envoyé, sinon `409 Conflict` |

---

## 4. Tests GET — chargement réel d’une tournée

### 4.1 Charger une tournée réelle depuis ABSSolute

```powershell
curl.exe -i "$api/api/tournees/jour?dateTournee=2026-04-27&codeTournee=1001&codeLivreur=3"
```

Résultat attendu :

```http
HTTP/1.1 200 OK
```

La réponse doit contenir :

```text
schemaVersion = 1.1
dateTournee = 2026-04-27
dateModifiable = false
jourTournee = 1
jourLibelle = Lundi
codeTournee = 1001
libelleTournee = CHATAIGNERAIE LES HERBIERS
livreur.codeLivreur = 3
livreur.nomLivreur = DAVID VARIN
chargement.nombrePointsEnvoyes = 7
articlesSaisissables = ROLLS, TAPIS, SACS
lignes.Count = 7
```

### 4.2 Sauvegarder la réponse réelle

```powershell
curl.exe "$api/api/tournees/jour?dateTournee=2026-04-27&codeTournee=1001&codeLivreur=3" `
  -o "get-tournee-reelle-1001.json"
```

### 4.3 Vérifier le nombre de lignes

```powershell
$json = Get-Content ".\get-tournee-reelle-1001.json" -Raw | ConvertFrom-Json
$json.lignes.Count
```

Résultat attendu :

```text
7
```

### 4.4 Vérifier qu’aucun `idLigneSource` n’est dupliqué

```powershell
$json.lignes |
  Group-Object idLigneSource |
  Where-Object { $_.Count -gt 1 }
```

Résultat attendu : aucun résultat.

### 4.5 Date invalide

```powershell
curl.exe -i "$api/api/tournees/jour?dateTournee=date-invalide&codeTournee=1001&codeLivreur=3"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

### 4.6 Tournée inexistante

```powershell
curl.exe -i "$api/api/tournees/jour?dateTournee=2026-04-27&codeTournee=9999&codeLivreur=3"
```

Résultat attendu :

```http
HTTP/1.1 404 Not Found
```

### 4.7 Livreur inexistant

```powershell
curl.exe -i "$api/api/tournees/jour?dateTournee=2026-04-27&codeTournee=1001&codeLivreur=999999"
```

Résultat attendu :

```http
HTTP/1.1 404 Not Found
```

---

## 5. Tests POST — synchronisation mobile

### 5.1 Envoi valide

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-valide.json"
```

Résultat attendu :

```http
HTTP/1.1 200 OK
```

```json
{"statut":"SUCCESS","message":"Synchronisation enregistrée avec succès."}
```

### 5.2 Doublon technique avec le même `idSynchronisation`


```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-doublon.json"
```

Résultat attendu :

```http
HTTP/1.1 409 Conflict
```

```json
{"statut":"CONFLICT"}
```

### 5.3 Double envoi métier de la même tournée

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-double-envoi-tournee.json"
```

Résultat attendu :

```http
HTTP/1.1 409 Conflict
```

```json
{"statut":"CONFLICT"}
```

### 5.4 Quantité négative

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-quantite-negative.json"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

```json
{"statut":"VALIDATION_ERROR"}
```

### 5.5 `NON_FAIT` sans commentaire

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-non-fait-sans-commentaire.json"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

```json
{"statut":"VALIDATION_ERROR"}
```

### 5.6 `ANOMALIE` sans commentaire

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-anomalie-sans-commentaire.json"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

```json
{"statut":"VALIDATION_ERROR"}
```

### 5.7 Ligne validée sans `heureValidation`

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-validee-sans-heure.json"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

```json
{"statut":"VALIDATION_ERROR"}
```

### 5.8 `estValidee = false` dans l’envoi final

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-est-validee-false.json"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

```json
{"statut":"VALIDATION_ERROR"}
```

### 5.9 `A_FAIRE` dans l’envoi final

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-a-faire.json"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

```json
{"statut":"VALIDATION_ERROR"}
```

### 5.10 `idLigneSource` dupliqué dans la même requête

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-idligne-duplique.json"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

```json
{"statut":"VALIDATION_ERROR"}
```

### 5.11 `codeArticle` dupliqué dans une ligne

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-code-article-duplique.json"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

```json
{"statut":"VALIDATION_ERROR"}
```

### 5.12 Version de schéma non supportée

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-schema-version-invalide.json"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

```json
{"statut":"VALIDATION_ERROR"}
```

### 5.13 Tableau `quantites` vide

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-quantites-vide.json"
```

Résultat attendu :

```http
HTTP/1.1 400 Bad Request
```

```json
{"statut":"VALIDATION_ERROR"}
```

---

## 6. Bloc PowerShell complet pour les tests POST

Ce bloc permet d’enchaîner les tests `POST`. À lancer depuis `docs\04-tests`, après avoir défini `$api`.

```powershell
$ApiUrl = "$api/api/synchronisations"

function Test-Synchronisation {
    param(
        [string]$NomTest,
        [string]$Fichier,
        [string]$ResultatAttendu
    )

    Write-Host ""
    Write-Host "============================================================"
    Write-Host "TEST : $NomTest"
    Write-Host "FICHIER : $Fichier"
    Write-Host "ATTENDU : $ResultatAttendu"
    Write-Host "============================================================"

    curl.exe -i -X POST $ApiUrl `
      -H "Content-Type: application/json" `
      --data-binary "@$Fichier"
}

Test-Synchronisation `
  -NomTest "01 - Synchronisation valide" `
  -Fichier "sync-valide.json" `
  -ResultatAttendu "200 OK / SUCCESS"

Test-Synchronisation `
  -NomTest "02 - Doublon technique idSynchronisation" `
  -Fichier "sync-doublon.json" `
  -ResultatAttendu "409 Conflict / CONFLICT"

Test-Synchronisation `
  -NomTest "03 - Double envoi métier même date + tournée + livreur" `
  -Fichier "sync-double-envoi-tournee.json" `
  -ResultatAttendu "409 Conflict / CONFLICT"

Test-Synchronisation `
  -NomTest "04 - Quantité négative" `
  -Fichier "sync-quantite-negative.json" `
  -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"

Test-Synchronisation `
  -NomTest "05 - NON_FAIT sans commentaire" `
  -Fichier "sync-non-fait-sans-commentaire.json" `
  -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"

Test-Synchronisation `
  -NomTest "06 - ANOMALIE sans commentaire" `
  -Fichier "sync-anomalie-sans-commentaire.json" `
  -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"

Test-Synchronisation `
  -NomTest "07 - Ligne validée sans heureValidation" `
  -Fichier "sync-validee-sans-heure.json" `
  -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"

Test-Synchronisation `
  -NomTest "08 - estValidee false dans envoi final" `
  -Fichier "sync-est-validee-false.json" `
  -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"

Test-Synchronisation `
  -NomTest "09 - A_FAIRE dans envoi final" `
  -Fichier "sync-a-faire.json" `
  -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"

Test-Synchronisation `
  -NomTest "10 - idLigneSource dupliqué" `
  -Fichier "sync-idligne-duplique.json" `
  -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"

Test-Synchronisation `
  -NomTest "11 - codeArticle dupliqué" `
  -Fichier "sync-code-article-duplique.json" `
  -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"

Test-Synchronisation `
  -NomTest "12 - schemaVersion non supportée" `
  -Fichier "sync-schema-version-invalide.json" `
  -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"

Test-Synchronisation `
  -NomTest "13 - quantites vide" `
  -Fichier "sync-quantites-vide.json" `
  -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
```

---

## 7. Test réel avec les données ABSSolute

Ce test valide le flux complet avec une vraie tournée récupérée depuis les vues ABSSolute.

### 7.1 Charger la tournée réelle

```powershell
curl.exe "$api/api/tournees/jour?dateTournee=2026-04-27&codeTournee=1001&codeLivreur=3" `
  -o "get-tournee-reelle-1001.json"
```

### 7.2 Vérifier le fichier obtenu

```powershell
$json = Get-Content ".\get-tournee-reelle-1001.json" -Raw | ConvertFrom-Json
$json.schemaVersion
$json.dateModifiable
$json.chargement.nombrePointsEnvoyes
$json.lignes.Count
$json.articlesSaisissables
$json.lignes | Group-Object idLigneSource | Where-Object { $_.Count -gt 1 }
```

Résultat attendu :

```text
schemaVersion = 1.1
dateModifiable = false
nombrePointsEnvoyes = 7
lignes.Count = 7
aucun idLigneSource dupliqué
```

### 7.3 Envoyer le fichier réel

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-reel-1001.json"
```

Résultat attendu si le fichier n’a jamais été envoyé :

```http
HTTP/1.1 200 OK
```

Résultat attendu si le fichier a déjà été envoyé :

```http
HTTP/1.1 409 Conflict
```

Le conflit est normal dans ce cas, car `sync-reel-1001.json` contient un `idSynchronisation` fixe.

---

## 8. Tests GET — consultation admin

### 8.1 Liste des synchronisations

```powershell
curl.exe -i "$api/api/synchronisations"
```

Résultat attendu :

```json
{"statut":"SUCCESS","count":0,"synchronisations":[]}
```

Le `count` dépend des données déjà présentes en base.

### 8.2 Filtre par date

```powershell
curl.exe -i "$api/api/synchronisations?dateTournee=2026-04-28"
```

Résultat attendu :

```json
{"statut":"SUCCESS"}
```

### 8.3 Filtre par date + tournée

```powershell
curl.exe -i "$api/api/synchronisations?dateTournee=2026-04-28&codeTournee=2001"
```

Résultat attendu :

```json
{"statut":"SUCCESS"}
```

### 8.4 Filtre par date + tournée + livreur

```powershell
curl.exe -i "$api/api/synchronisations?dateTournee=2026-04-28&codeTournee=2001&codeLivreur=2"
```

Résultat attendu :

```json
{"statut":"SUCCESS"}
```

### 8.5 Détail d’une synchronisation

Récupérer d’abord un ID :

```sql
SELECT TOP 1 IdTourneeMobile
FROM Mobile_Tournee
ORDER BY IdTourneeMobile DESC;
```

Puis remplacer `1` par le vrai `IdTourneeMobile` :

```powershell
curl.exe -i "$api/api/synchronisations/1"
```

Résultat attendu :

```json
{"statut":"SUCCESS","synchronisation":{}}
```

### 8.6 Détail inexistant

```powershell
curl.exe -i "$api/api/synchronisations/999999999"
```

Résultat attendu :

```http
HTTP/1.1 404 Not Found
```

---

## 9. Vérifications SQL après un POST valide

À exécuter dans `bd_eric`.

```sql
SELECT TOP 10 *
FROM Mobile_Livreur
ORDER BY IdLivreur DESC;
```

```sql
SELECT TOP 10 *
FROM Mobile_Tournee
ORDER BY IdTourneeMobile DESC;
```

```sql
SELECT TOP 50 *
FROM Mobile_TourneeLigne
ORDER BY IdTourneeMobile DESC, OrdreArret;
```

```sql
SELECT TOP 100 *
FROM Mobile_TourneeLigneQuantite
ORDER BY IdQuantite DESC;
```

```sql
SELECT TOP 20 *
FROM Mobile_LogSynchronisation
ORDER BY IdLog DESC;
```

### Requête complète de contrôle

```sql
SELECT TOP 100
    t.IdTourneeMobile,
    t.IdSynchronisation,
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
    liv.CodeLivreur,
    liv.NomLivreur,
    t.StatutSynchronisation,
    t.EstVerrouillee,
    l.IdTourneeLigne,
    l.IdLigneSource,
    l.OrdreArret,
    l.NumClient,
    l.NomClient,
    l.CodePDL,
    l.DescriptionPDL,
    l.QuantiteLivree AS TotalLivre,
    l.QuantiteReprise AS TotalRecupere,
    q.CodeArticle,
    q.LibelleArticle,
    q.QuantiteLivree,
    q.QuantiteRecuperee
FROM Mobile_Tournee t
INNER JOIN Mobile_Livreur liv
    ON liv.IdLivreur = t.IdLivreur
INNER JOIN Mobile_TourneeLigne l
    ON l.IdTourneeMobile = t.IdTourneeMobile
INNER JOIN Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = l.IdTourneeLigne
ORDER BY
    t.IdTourneeMobile DESC,
    l.OrdreArret,
    q.CodeArticle;
```

Pour `sync-valide.json`, on attend :

```text
1 ligne dans Mobile_Tournee
1 ligne dans Mobile_TourneeLigne
3 lignes dans Mobile_TourneeLigneQuantite
1 log ENVOI_REUSSI dans Mobile_LogSynchronisation
```

Pour `sync-reel-1001.json`, on attend si le fichier est envoyé sur une base vide :

```text
1 ligne dans Mobile_Tournee
7 lignes dans Mobile_TourneeLigne
21 lignes dans Mobile_TourneeLigneQuantite
1 log ENVOI_REUSSI dans Mobile_LogSynchronisation
```

---

## 10. Conclusion attendue

Les tests couvrent les réponses principales de l’API :

```text
200 OK / SUCCESS
400 Bad Request / VALIDATION_ERROR
404 Not Found
409 Conflict / CONFLICT
```

Les règles métier importantes sont couvertes :

```text
- chargement réel d’une tournée ;
- date non modifiable ;
- identification par code livreur ;
- quantités en format quantites[] ;
- blocage des quantités négatives ;
- blocage des statuts incomplets ;
- blocage d’une ligne non validée ;
- blocage de A_FAIRE dans l’envoi final ;
- blocage des doublons techniques ;
- blocage des doublons métier ;
- consultation admin des synchronisations envoyées.
```
