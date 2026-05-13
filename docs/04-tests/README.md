# Tests API mobile SLI — contrat JSON v1.2

Date de mise à jour : 2026-05-12  
URL locale utilisée : `http://127.0.0.1:5000`

Ce dossier contient les fichiers JSON de test pour vérifier le contrat technique `schemaVersion = 1.2`.

La version 1.2 vérifie notamment :

```text
- saisie.quantites[]
- quantiteLivreePrevue
- distinction entre quantiteLivreePrevue = null et quantiteLivreePrevue = 0
- quantiteLivree
- quantiteRecuperee
- commentaireExceptionnel
- informations de tournée, retour et point de livraison conservées dans le snapshot
- anti-doublon technique par idSynchronisation
- anti-doublon métier par dateTournee + codeTournee + codeLivreur
```

---

## 1. Préconditions

Démarrer l'API dans un premier terminal :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"
dotnet build
dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

Dans un second terminal :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core\docs\04-tests"
$api = "http://127.0.0.1:5000"
```

Les tests `POST` utilisent des `idSynchronisation` fixes. Pour obtenir exactement les résultats attendus, lance-les dans l'ordre indiqué, de préférence sur une base de développement réinitialisée.

Si un fichier valide a déjà été envoyé avec succès, un nouvel envoi peut retourner `409 Conflict`. C'est normal.

---

## 2. Réinitialisation optionnelle des données de test

À exécuter uniquement sur la base de développement `bd_eric`.

```sql
DELETE FROM Mobile_TourneeLigneQuantite;
DELETE FROM Mobile_LogSynchronisation;
DELETE FROM Mobile_ExportAdmin;
DELETE FROM Mobile_TourneeLigne;
DELETE FROM Mobile_Tournee;
DELETE FROM Mobile_ChargementTournee;
DELETE FROM Mobile_Livreur;
```

Ne pas supprimer les tables de référence et de préparation :

```text
Mobile_ArticleSaisissable
Mobile_UtilisateurExpedition
Mobile_CommentaireExceptionnel
Mobile_PreRemplissageTournee
Mobile_PreRemplissageQuantite
Mobile_PreRemplissageHistorique
```

---

## 3. Tests de santé et de connexion

```powershell
curl.exe -i "$api/api/health"
curl.exe -i "$api/api/health/abssolute"
curl.exe -i "$api/api/health/mobile"
```

Résultat attendu :

```text
HTTP/1.1 200 OK
```

---

## 4. Tests GET — livreurs et tournées

### 4.1 Liste des livreurs

```powershell
curl.exe -i "$api/api/livreurs"
```

### 4.2 Tournées disponibles

```powershell
curl.exe -i "$api/api/tournees/disponibles?dateTournee=2026-05-07&codeLivreur=2"
```

Résultat attendu : `200 OK`, `schemaVersion = 1.2`, `dateModifiable = false`, `tournees.Count > 0`.

### 4.3 Chargement complet d'une tournée

```powershell
curl.exe -i "$api/api/tournees/jour?dateTournee=2026-05-07&codeTournee=4006&codeLivreur=2"
```

Résultat attendu : `200 OK`, `schemaVersion = 1.2`, `articlesSaisissables`, `lignes[]`, `saisie.quantites[]`, `quantiteLivreePrevue` visible même quand la valeur est `null`.

### 4.4 Date invalide

```powershell
curl.exe -i "$api/api/tournees/jour?dateTournee=date-invalide&codeTournee=4006&codeLivreur=2"
```

Résultat attendu : `400 Bad Request`.

### 4.5 Tournée inexistante

```powershell
curl.exe -i "$api/api/tournees/jour?dateTournee=2026-05-07&codeTournee=9999&codeLivreur=2"
```

Résultat attendu : `404 Not Found`.

### 4.6 Livreur inexistant

```powershell
curl.exe -i "$api/api/tournees/jour?dateTournee=2026-05-07&codeTournee=4006&codeLivreur=999999"
```

Résultat attendu : `404 Not Found`.

---

## 5. Fichiers JSON fournis

| Fichier | Rôle | Résultat attendu |
|---|---|---|
| `sync-valide.json` | Envoi valide principal | `200 OK / SUCCESS` |
| `sync-doublon.json` | Même `idSynchronisation` que `sync-valide.json` | `409 Conflict / SYNCHRONISATION_ALREADY_EXISTS` |
| `sync-double-envoi-tournee.json` | Même date + tournée + livreur que `sync-valide.json`, mais autre `idSynchronisation` | `409 Conflict / TOURNEE_ALREADY_SENT` |
| `sync-quantite-negative.json` | Quantité livrée négative | `400 Bad Request / VALIDATION_ERROR` |
| `sync-non-fait-sans-commentaire.json` | `NON_FAIT` sans commentaire | `400 Bad Request / VALIDATION_ERROR` |
| `sync-anomalie-sans-commentaire.json` | `ANOMALIE` sans commentaire | `400 Bad Request / VALIDATION_ERROR` |
| `sync-validee-sans-heure.json` | `estValidee = true` sans `heureValidation` | `400 Bad Request / VALIDATION_ERROR` |
| `sync-est-validee-false.json` | Ligne non validée dans l'envoi final | `400 Bad Request / VALIDATION_ERROR` |
| `sync-a-faire.json` | Statut `A_FAIRE` dans l'envoi final | `400 Bad Request / VALIDATION_ERROR` |
| `sync-idligne-duplique.json` | Même `idLigneSource` deux fois dans la requête | `400 Bad Request / VALIDATION_ERROR` |
| `sync-code-article-duplique.json` | Même `codeArticle` deux fois dans une ligne | `400 Bad Request / VALIDATION_ERROR` |
| `sync-schema-version-invalide.json` | Version `schemaVersion` non supportée | `400 Bad Request / VALIDATION_ERROR` |
| `sync-quantites-vide.json` | Tableau `quantites` vide | `400 Bad Request / VALIDATION_ERROR` |
| `sync-quantite-prevue-negative.json` | Quantité prévue négative | `400 Bad Request / VALIDATION_ERROR` |
| `sync-prevu-null.json` | Test explicite `quantiteLivreePrevue = null` | `200 OK / SUCCESS` |
| `sync-prevu-zero.json` | Test explicite `quantiteLivreePrevue = 0` | `200 OK / SUCCESS` |
| `sync.json` | Exemple indépendant de synchronisation valide | `200 OK / SUCCESS` |
| `get-tournee-reelle-1001.json` | Exemple sauvegardé de réponse GET v1.2 | vérification manuelle |
| `sync-reel-1001.json` | Envoi construit à partir d'un GET réel | `200 OK / SUCCESS` si pas déjà envoyé |

---

## 6. Tests POST principaux

### 6.1 Envoi valide

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-valide.json"
```

Résultat attendu : `200 OK / SUCCESS`.

### 6.2 Doublon technique

À lancer après `sync-valide.json`.

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-doublon.json"
```

Résultat attendu : `409 Conflict / SYNCHRONISATION_ALREADY_EXISTS`.

### 6.3 Double envoi métier

À lancer après `sync-valide.json`.

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-double-envoi-tournee.json"
```

Résultat attendu : `409 Conflict / TOURNEE_ALREADY_SENT`.

---

## 7. Tests POST — validations métier

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-quantite-negative.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-non-fait-sans-commentaire.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-anomalie-sans-commentaire.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-validee-sans-heure.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-est-validee-false.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-a-faire.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-idligne-duplique.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-code-article-duplique.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-schema-version-invalide.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-quantites-vide.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-quantite-prevue-negative.json"
```

Résultat attendu pour chaque commande : `400 Bad Request / VALIDATION_ERROR`.

---

## 8. Tests spécifiques `quantiteLivreePrevue`

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-prevu-null.json"

curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-prevu-zero.json"
```

Résultat attendu : `200 OK / SUCCESS`.

---

## 9. Consultation après synchronisation

```powershell
curl.exe -i "$api/api/synchronisations"
```

```powershell
curl.exe -i "$api/api/synchronisations?dateTournee=2026-06-04&codeTournee=4006&codeLivreur=2"
```

```powershell
$response = curl.exe "$api/api/synchronisations?dateTournee=2026-06-04&codeTournee=4006&codeLivreur=2" | ConvertFrom-Json
$id = $response.synchronisations[0].idTourneeMobile
curl.exe -i "$api/api/synchronisations/$id"
```

```powershell
curl.exe -i "$api/api/synchronisations/999999999"
```

Résultat attendu pour l'ID inexistant : `404 Not Found`.

---

## 10. Bloc PowerShell complet pour enchaîner les tests POST

À lancer depuis `docs\04-tests`, après avoir défini `$api`.

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

Test-Synchronisation -NomTest "01 - Synchronisation valide" -Fichier "sync-valide.json" -ResultatAttendu "200 OK / SUCCESS"
Test-Synchronisation -NomTest "02 - Doublon technique idSynchronisation" -Fichier "sync-doublon.json" -ResultatAttendu "409 Conflict / SYNCHRONISATION_ALREADY_EXISTS"
Test-Synchronisation -NomTest "03 - Double envoi métier même date + tournée + livreur" -Fichier "sync-double-envoi-tournee.json" -ResultatAttendu "409 Conflict / TOURNEE_ALREADY_SENT"
Test-Synchronisation -NomTest "04 - Quantité négative" -Fichier "sync-quantite-negative.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "05 - NON_FAIT sans commentaire" -Fichier "sync-non-fait-sans-commentaire.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "06 - ANOMALIE sans commentaire" -Fichier "sync-anomalie-sans-commentaire.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "07 - Ligne validée sans heureValidation" -Fichier "sync-validee-sans-heure.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "08 - estValidee false dans envoi final" -Fichier "sync-est-validee-false.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "09 - A_FAIRE dans envoi final" -Fichier "sync-a-faire.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "10 - idLigneSource dupliqué" -Fichier "sync-idligne-duplique.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "11 - codeArticle dupliqué" -Fichier "sync-code-article-duplique.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "12 - schemaVersion non supportée" -Fichier "sync-schema-version-invalide.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "13 - quantites vide" -Fichier "sync-quantites-vide.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "14 - quantiteLivreePrevue négative" -Fichier "sync-quantite-prevue-negative.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
```

---

## 11. Vérifications SQL après les tests

À exécuter dans `bd_eric`.

```sql
SELECT TOP 20 *
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

Requête complète :

```sql
SELECT TOP 100
    t.IdTourneeMobile,
    t.SchemaVersion,
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
    q.QuantiteLivreePrevue,
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

---

## 12. Résultat attendu global

Les tests doivent couvrir :

```text
200 OK / SUCCESS
400 Bad Request / VALIDATION_ERROR
404 Not Found
409 Conflict / CONFLICT
```

Une fois ces tests validés, la prochaine étape sera la mise à jour de la documentation API et de Swagger.
