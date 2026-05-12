# Tests de concurrence — API tournées mobile

## 1. Objectif

Ces tests vérifient que l’API reste stable lorsque plusieurs livreurs chargent ou synchronisent leur tournée en même temps.

Routes concernées :

```text
GET  /api/tournees/disponibles
GET  /api/tournees/jour
POST /api/synchronisations
```

Le test ne remplace pas un test de production complet. Il valide un scénario réaliste de chargement simultané au dépôt et d’envoi simultané en fin de journée.

## 2. Environnement

API testée :

```text
http://localhost:5120
```

Disponibilité :

```powershell
curl.exe "http://localhost:5120/api/health"
```

L’API doit être lancée avec :

```powershell
dotnet run
```

Le contrat JSON utilisé est :

```text
schemaVersion = "1.2"
```

## 3. Points à vérifier avant les tests

### Port

Le port utilisé est :

```text
5120
```

Vérification :

```powershell
curl.exe "http://localhost:5120/api/health"
```

### Codes d’erreur

```text
404 -> combinaison dateTournee + codeTournee + codeLivreur inexistante
409 -> conflit attendu pour un doublon
500 -> erreur technique à analyser, souvent timeout SQL sous forte charge
```

## 4. Test 1 — 20 requêtes concurrentes sur `/api/tournees/disponibles`

### Objectif

Simuler plusieurs téléphones qui demandent en même temps la liste des tournées disponibles.

Route :

```text
GET /api/tournees/disponibles?dateTournee=2026-05-07&codeLivreur=2
```

### Script PowerShell

```powershell
$baseUrl = "http://localhost:5120"
$dateTournee = "2026-05-07"
$codeLivreur = "2"
$nbRequetes = 20

$url = "$baseUrl/api/tournees/disponibles?dateTournee=$dateTournee&codeLivreur=$codeLivreur"

$jobs = @()
$globalWatch = [System.Diagnostics.Stopwatch]::StartNew()

for ($i = 1; $i -le $nbRequetes; $i++) {
    $jobs += Start-Job -ScriptBlock {
        param($requestNumber, $url)

        $watch = [System.Diagnostics.Stopwatch]::StartNew()

        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60
            $watch.Stop()

            [PSCustomObject]@{
                Numero = $requestNumber
                Succes = $true
                StatusCode = [int]$response.StatusCode
                TempsMs = $watch.ElapsedMilliseconds
                Erreur = ""
            }
        }
        catch {
            $watch.Stop()

            $statusCode = 0

            if ($_.Exception.Response -ne $null) {
                try {
                    $statusCode = [int]$_.Exception.Response.StatusCode
                }
                catch {
                    $statusCode = 0
                }
            }

            [PSCustomObject]@{
                Numero = $requestNumber
                Succes = $false
                StatusCode = $statusCode
                TempsMs = $watch.ElapsedMilliseconds
                Erreur = $_.Exception.Message
            }
        }
    } -ArgumentList $i, $url
}

$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job
$globalWatch.Stop()

$success = @($results | Where-Object { $_.Succes -eq $true })
$failed = @($results | Where-Object { $_.Succes -eq $false })
$times = @($success | Select-Object -ExpandProperty TempsMs | Sort-Object)

$p95Index = [Math]::Ceiling($times.Count * 0.95) - 1
if ($p95Index -lt 0) { $p95Index = 0 }

[PSCustomObject]@{
    Requetes = $results.Count
    Succes = $success.Count
    Echecs = $failed.Count
    TempsTotalMs = $globalWatch.ElapsedMilliseconds
    TempsMoyenMs = if ($success.Count -gt 0) { [Math]::Round(($success | Measure-Object TempsMs -Average).Average, 2) } else { 0 }
    TempsMinMs = if ($success.Count -gt 0) { ($success | Measure-Object TempsMs -Minimum).Minimum } else { 0 }
    TempsMaxMs = if ($success.Count -gt 0) { ($success | Measure-Object TempsMs -Maximum).Maximum } else { 0 }
    Percentile95Ms = if ($success.Count -gt 0) { $times[$p95Index] } else { 0 }
} | Format-List

$results | Sort-Object Numero | Format-Table Numero, Succes, StatusCode, TempsMs -AutoSize

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Erreurs détectées :"
    $failed | Format-Table Numero, StatusCode, Erreur -AutoSize
}
```

### Résultat obtenu

```text
Requêtes       : 20
Succès         : 20
Échecs         : 0
Temps total    : 121215 ms
Temps moyen    : 5408 ms
Temps min      : 2935 ms
Temps max      : 10662 ms
Percentile 95  : 9575 ms
```

### Conclusion

La route `GET /api/tournees/disponibles` supporte 20 requêtes concurrentes sans erreur.

## 5. Test 2 — Vérification individuelle du chargement complet

### Objectif

Vérifier que chaque tournée existe avant le test de concurrence.

Route :

```text
GET /api/tournees/jour?dateTournee=2026-05-07&codeTournee=XXXX&codeLivreur=2
```

### Tournées disponibles testées

```text
4001, 4002, 4003, 4004, 4005, 4006, 4007,
4011, 4012, 4013, 4014, 4015, 4016, 4017,
4018, 4019, 4020, 4021, 4022, 4023
```

### Résultat obtenu

Chaque tournée testée individuellement a répondu :

```text
HTTP 200
```

## 6. Test 3 — Chargements complets simultanés sur `/api/tournees/jour`

### Objectif

Simuler plusieurs livreurs qui chargent chacun une tournée complète en même temps.

### Script PowerShell

Modifier `$codesTournees` pour tester 5, 10 ou 20 tournées.

```powershell
$baseUrl = "http://localhost:5120"
$dateTournee = "2026-05-07"
$codeLivreur = "2"

$codesTournees = @(
    "4001",
    "4002",
    "4003",
    "4004",
    "4005"
)

$jobs = @()
$globalWatch = [System.Diagnostics.Stopwatch]::StartNew()
$numero = 0

foreach ($codeTournee in $codesTournees) {
    $numero++

    $url = "$baseUrl/api/tournees/jour?dateTournee=$dateTournee&codeTournee=$codeTournee&codeLivreur=$codeLivreur"

    $jobs += Start-Job -ScriptBlock {
        param($numero, $codeTournee, $url)

        $watch = [System.Diagnostics.Stopwatch]::StartNew()

        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60
            $watch.Stop()

            [PSCustomObject]@{
                Numero = $numero
                CodeTournee = $codeTournee
                Succes = $true
                StatusCode = [int]$response.StatusCode
                TempsMs = $watch.ElapsedMilliseconds
                Erreur = ""
            }
        }
        catch {
            $watch.Stop()

            $statusCode = 0

            if ($_.Exception.Response -ne $null) {
                try {
                    $statusCode = [int]$_.Exception.Response.StatusCode
                }
                catch {
                    $statusCode = 0
                }
            }

            [PSCustomObject]@{
                Numero = $numero
                CodeTournee = $codeTournee
                Succes = $false
                StatusCode = $statusCode
                TempsMs = $watch.ElapsedMilliseconds
                Erreur = $_.Exception.Message
            }
        }
    } -ArgumentList $numero, $codeTournee, $url
}

$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job
$globalWatch.Stop()

$success = @($results | Where-Object { $_.Succes -eq $true })
$failed = @($results | Where-Object { $_.Succes -eq $false })
$times = @($success | Select-Object -ExpandProperty TempsMs | Sort-Object)

$p95Index = [Math]::Ceiling($times.Count * 0.95) - 1
if ($p95Index -lt 0) { $p95Index = 0 }

[PSCustomObject]@{
    Requetes = $results.Count
    Succes = $success.Count
    Echecs = $failed.Count
    TempsTotalMs = $globalWatch.ElapsedMilliseconds
    TempsMoyenMs = if ($success.Count -gt 0) { [Math]::Round(($success | Measure-Object TempsMs -Average).Average, 2) } else { 0 }
    TempsMinMs = if ($success.Count -gt 0) { ($success | Measure-Object TempsMs -Minimum).Minimum } else { 0 }
    TempsMaxMs = if ($success.Count -gt 0) { ($success | Measure-Object TempsMs -Maximum).Maximum } else { 0 }
    Percentile95Ms = if ($success.Count -gt 0) { $times[$p95Index] } else { 0 }
} | Format-List

$results | Sort-Object Numero | Format-Table Numero, CodeTournee, Succes, StatusCode, TempsMs -AutoSize

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Erreurs détectées :"
    $failed | Format-Table Numero, CodeTournee, StatusCode, Erreur -AutoSize
}
```

### Résultat avec 10 chargements simultanés

```text
Requêtes       : 10
Succès         : 10
Échecs         : 0
Temps total    : 34746 ms
Temps moyen    : 12015,4 ms
Temps min      : 9507 ms
Temps max      : 14119 ms
Percentile 95  : 14119 ms
```

### Résultat avec 20 chargements simultanés

```text
Requêtes       : 20
Succès         : 20
Échecs         : 0
Temps total    : 243762 ms
Temps moyen    : 19000,55 ms
Temps min      : 12304 ms
Temps max      : 22192 ms
Percentile 95  : 21970 ms
```

### Conclusion

L’API supporte 20 chargements complets simultanés dans l’environnement de développement.

Ce test est représentatif du besoin métier : plusieurs livreurs chargent leur tournée au dépôt.

## 7. Stress test à 100 chargements complets simultanés

### Résultat obtenu

```text
Requêtes       : 100
Succès         : 42
Échecs         : 58
Temps total    : 292428 ms
Temps moyen    : 39651,05 ms
Temps min      : 9076 ms
Temps max      : 73516 ms
Percentile 95  : 73289 ms
```

### Erreur observée

```text
Microsoft.Data.SqlClient.SqlException :
Le délai d'exécution a expiré.
Le délai d'attente s'est écoulé avant la fin de l'opération ou le serveur ne répond pas.
```

Localisation :

```text
TourneesRepository.GetTourneeLinesAsync
Dapper.QueryAsync
```

### Interprétation

100 chargements complets simultanés dépassent le scénario métier attendu.

Le problème observé est un timeout SQL côté chargement complet.

Ce résultat ne remet pas en cause le fonctionnement normal de l’application mobile.

## 8. Test 4 — Envoi du soir avec 10 synchronisations différentes

### Objectif

Simuler 10 téléphones qui envoient chacun une tournée différente en même temps.

Route :

```text
POST /api/synchronisations
```

### Script PowerShell

```powershell
$baseUrl = "http://localhost:5120"
$dateTournee = "2099-01-01"
$nbRequetes = 10
$runId = Get-Date -Format "HHmmss"

$requests = @()

for ($i = 1; $i -le $nbRequetes; $i++) {
    $numero = $i.ToString("000")
    $codeTournee = "TS$runId$numero"
    $codeLivreur = "TEST$numero"
    $idSynchronisation = [guid]::NewGuid().ToString()

    $payload = [PSCustomObject]@{
        schemaVersion = "1.2"
        idSynchronisation = $idSynchronisation
        dateTournee = $dateTournee
        codeTournee = $codeTournee
        libelleTournee = "TEST SOIR $numero"
        livreur = [PSCustomObject]@{
            codeLivreur = $codeLivreur
            nomLivreur = "LIVREUR TEST $numero"
        }
        mobile = [PSCustomObject]@{
            nomAppareil = "TEST-POSTE-$numero"
            versionApplication = "1.0.0"
            dateChargementMobile = "2099-01-01T07:30:00+02:00"
            dateEnvoiMobile = "2099-01-01T17:30:00+02:00"
        }
        commentaireGlobal = $null
        lignes = @(
            [PSCustomObject]@{
                idLigneSource = "$dateTournee|$codeTournee|$codeLivreur|CLIENT$numero|PDL$numero|1"
                ordreArret = 1
                horaire = 1
                client = [PSCustomObject]@{
                    numClient = "CLIENT$numero"
                    nomClient = "CLIENT TEST $numero"
                    nomAffiche = "CLIENT TEST $numero"
                }
                pointLivraison = [PSCustomObject]@{
                    codePDL = "PDL$numero"
                    descriptionPDL = "POINT TEST $numero"
                }
                tournee = [PSCustomObject]@{
                    codeTournee = $codeTournee
                    libelleTournee = "TEST SOIR $numero"
                    jourTournee = 1
                    jourLibelle = "Lundi"
                    schemaLivraison = "TEST"
                }
                retour = [PSCustomObject]@{
                    jourTourneeRetour = 1
                    jourRetourLibelle = "Lundi"
                    codeTourneeRetour = $codeTournee
                    libelleTourneeRetour = "TEST SOIR $numero"
                }
                infosLivreur = [PSCustomObject]@{
                    instructions = $null
                    commentaireExceptionnel = $null
                    zoneDechargement = $null
                    zoneDechargementAffichee = $null
                    zone = $null
                    precision = $null
                    cle = $null
                    estFerme = $false
                    dateFermeture = $null
                    motifFermeture = $null
                }
                saisie = [PSCustomObject]@{
                    precisionLivreur = $null
                    statutPassage = "FAIT"
                    commentaireLivreur = $null
                    heureValidation = "2099-01-01T12:00:00+02:00"
                    estValidee = $true
                    quantites = @(
                        [PSCustomObject]@{
                            codeArticle = "ROLLS"
                            libelle = "Rolls"
                            quantiteLivreePrevue = $null
                            quantiteLivree = 1
                            quantiteRecuperee = 1
                        },
                        [PSCustomObject]@{
                            codeArticle = "TAPIS"
                            libelle = "Tapis"
                            quantiteLivreePrevue = 0
                            quantiteLivree = 2
                            quantiteRecuperee = 0
                        },
                        [PSCustomObject]@{
                            codeArticle = "SACS"
                            libelle = "Sacs"
                            quantiteLivreePrevue = 0
                            quantiteLivree = 0
                            quantiteRecuperee = 1
                        }
                    )
                }
            }
        )
    }

    $requests += [PSCustomObject]@{
        Numero = $i
        CodeTournee = $codeTournee
        CodeLivreur = $codeLivreur
        Body = ($payload | ConvertTo-Json -Depth 20)
    }
}

$jobs = @()
$globalWatch = [System.Diagnostics.Stopwatch]::StartNew()

foreach ($request in $requests) {
    $jobs += Start-Job -ScriptBlock {
        param($numero, $codeTournee, $codeLivreur, $body, $baseUrl)

        $watch = [System.Diagnostics.Stopwatch]::StartNew()

        try {
            $response = Invoke-WebRequest `
                -Uri "$baseUrl/api/synchronisations" `
                -Method Post `
                -Body $body `
                -ContentType "application/json; charset=utf-8" `
                -UseBasicParsing `
                -TimeoutSec 60

            $watch.Stop()

            [PSCustomObject]@{
                Numero = $numero
                CodeTournee = $codeTournee
                CodeLivreur = $codeLivreur
                Succes = $true
                StatusCode = [int]$response.StatusCode
                TempsMs = $watch.ElapsedMilliseconds
                Erreur = ""
            }
        }
        catch {
            $watch.Stop()

            $statusCode = 0

            if ($_.Exception.Response -ne $null) {
                try {
                    $statusCode = [int]$_.Exception.Response.StatusCode
                }
                catch {
                    $statusCode = 0
                }
            }

            [PSCustomObject]@{
                Numero = $numero
                CodeTournee = $codeTournee
                CodeLivreur = $codeLivreur
                Succes = $false
                StatusCode = $statusCode
                TempsMs = $watch.ElapsedMilliseconds
                Erreur = $_.Exception.Message
            }
        }
    } -ArgumentList $request.Numero, $request.CodeTournee, $request.CodeLivreur, $request.Body, $baseUrl
}

$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job
$globalWatch.Stop()

$success = @($results | Where-Object { $_.Succes -eq $true })
$failed = @($results | Where-Object { $_.Succes -eq $false })
$times = @($success | Select-Object -ExpandProperty TempsMs | Sort-Object)

$p95Index = [Math]::Ceiling($times.Count * 0.95) - 1
if ($p95Index -lt 0) { $p95Index = 0 }

[PSCustomObject]@{
    Requetes = $results.Count
    Succes = $success.Count
    Echecs = $failed.Count
    TempsTotalMs = $globalWatch.ElapsedMilliseconds
    TempsMoyenMs = if ($success.Count -gt 0) { [Math]::Round(($success | Measure-Object TempsMs -Average).Average, 2) } else { 0 }
    TempsMinMs = if ($success.Count -gt 0) { ($success | Measure-Object TempsMs -Minimum).Minimum } else { 0 }
    TempsMaxMs = if ($success.Count -gt 0) { ($success | Measure-Object TempsMs -Maximum).Maximum } else { 0 }
    Percentile95Ms = if ($success.Count -gt 0) { $times[$p95Index] } else { 0 }
} | Format-List

$results | Sort-Object Numero | Format-Table Numero, CodeTournee, CodeLivreur, Succes, StatusCode, TempsMs -AutoSize

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Erreurs détectées :"
    $failed | Format-Table Numero, CodeTournee, CodeLivreur, StatusCode, Erreur -AutoSize
}
```

### Résultat obtenu

```text
Requêtes       : 10
Succès         : 10
Échecs         : 0
Temps total    : 23668 ms
Temps moyen    : 1387,7 ms
Temps min      : 284 ms
Temps max      : 3810 ms
Percentile 95  : 3810 ms
```

### Conclusion

L’API supporte 10 envois simultanés différents sur `POST /api/synchronisations`.

## 9. Test 5 — Anti-doublon sur un envoi identique

### Objectif

Envoyer le même JSON de synchronisation 5 fois en parallèle.

Comportement attendu :

```text
1 requête acceptée en HTTP 200
4 requêtes refusées en HTTP 409
```

### Script PowerShell

```powershell
$baseUrl = "http://localhost:5120"
$dateTournee = "2099-01-02"
$nbRequetes = 5

$idSynchronisation = [guid]::NewGuid().ToString()
$codeTournee = "DUPTEST001"
$codeLivreur = "DUP001"

$payload = [PSCustomObject]@{
    schemaVersion = "1.2"
    idSynchronisation = $idSynchronisation
    dateTournee = $dateTournee
    codeTournee = $codeTournee
    libelleTournee = "TEST DOUBLON"
    livreur = [PSCustomObject]@{
        codeLivreur = $codeLivreur
        nomLivreur = "LIVREUR DOUBLON"
    }
    mobile = [PSCustomObject]@{
        nomAppareil = "TEST-DOUBLON"
        versionApplication = "1.0.0"
        dateChargementMobile = "2099-01-02T07:30:00+02:00"
        dateEnvoiMobile = "2099-01-02T17:30:00+02:00"
    }
    commentaireGlobal = $null
    lignes = @(
        [PSCustomObject]@{
            idLigneSource = "$dateTournee|$codeTournee|$codeLivreur|CLIENT001|PDL001|1"
            ordreArret = 1
            horaire = 1
            client = [PSCustomObject]@{
                numClient = "CLIENT001"
                nomClient = "CLIENT DOUBLON"
                nomAffiche = "CLIENT DOUBLON"
            }
            pointLivraison = [PSCustomObject]@{
                codePDL = "PDL001"
                descriptionPDL = "POINT DOUBLON"
            }
            tournee = [PSCustomObject]@{
                codeTournee = $codeTournee
                libelleTournee = "TEST DOUBLON"
                jourTournee = 1
                jourLibelle = "Lundi"
                schemaLivraison = "TEST"
            }
            retour = [PSCustomObject]@{
                jourTourneeRetour = 1
                jourRetourLibelle = "Lundi"
                codeTourneeRetour = $codeTournee
                libelleTourneeRetour = "TEST DOUBLON"
            }
            infosLivreur = [PSCustomObject]@{
                instructions = $null
                commentaireExceptionnel = $null
                zoneDechargement = $null
                zoneDechargementAffichee = $null
                zone = $null
                precision = $null
                cle = $null
                estFerme = $false
                dateFermeture = $null
                motifFermeture = $null
            }
            saisie = [PSCustomObject]@{
                precisionLivreur = $null
                statutPassage = "FAIT"
                commentaireLivreur = $null
                heureValidation = "2099-01-02T12:00:00+02:00"
                estValidee = $true
                quantites = @(
                    [PSCustomObject]@{
                        codeArticle = "ROLLS"
                        libelle = "Rolls"
                        quantiteLivreePrevue = $null
                        quantiteLivree = 1
                        quantiteRecuperee = 1
                    }
                )
            }
        }
    )
}

$body = $payload | ConvertTo-Json -Depth 20

$jobs = @()
$globalWatch = [System.Diagnostics.Stopwatch]::StartNew()

for ($i = 1; $i -le $nbRequetes; $i++) {
    $jobs += Start-Job -ScriptBlock {
        param($numero, $body, $baseUrl)

        $watch = [System.Diagnostics.Stopwatch]::StartNew()

        try {
            $response = Invoke-WebRequest `
                -Uri "$baseUrl/api/synchronisations" `
                -Method Post `
                -Body $body `
                -ContentType "application/json; charset=utf-8" `
                -UseBasicParsing `
                -TimeoutSec 60

            $watch.Stop()

            [PSCustomObject]@{
                Numero = $numero
                Succes = $true
                StatusCode = [int]$response.StatusCode
                TempsMs = $watch.ElapsedMilliseconds
                Erreur = ""
            }
        }
        catch {
            $watch.Stop()

            $statusCode = 0

            if ($_.Exception.Response -ne $null) {
                try {
                    $statusCode = [int]$_.Exception.Response.StatusCode
                }
                catch {
                    $statusCode = 0
                }
            }

            [PSCustomObject]@{
                Numero = $numero
                Succes = $false
                StatusCode = $statusCode
                TempsMs = $watch.ElapsedMilliseconds
                Erreur = $_.Exception.Message
            }
        }
    } -ArgumentList $i, $body, $baseUrl
}

$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job
$globalWatch.Stop()

$results | Sort-Object Numero | Format-Table Numero, Succes, StatusCode, TempsMs -AutoSize

$results |
Group-Object StatusCode |
Select-Object Name, Count |
Format-Table -AutoSize
```

### Résultat obtenu

```text
Numero Succes StatusCode TempsMs
------ ------ ---------- -------
1      True   200        3827
2      False  409        3578
3      False  409        3054
4      False  409        2489
5      False  409        388
```

Regroupement :

```text
Name Count
---- -----
200      1
409      4
```

### Conclusion

La protection anti-doublon fonctionne correctement.

Une seule synchronisation est enregistrée.

Les autres requêtes sont refusées en `409 Conflict`.

## 10. Nettoyage des données de test

Les tests `POST /api/synchronisations` écrivent en base.

Nettoyage des données artificielles :

```sql
DECLARE @DateDebutTest date = '2099-01-01';
DECLARE @DateFinTest date = '2099-01-03';

DELETE q
FROM Mobile_TourneeLigneQuantite q
INNER JOIN Mobile_TourneeLigne l
    ON l.IdTourneeLigne = q.IdTourneeLigne
INNER JOIN Mobile_Tournee t
    ON t.IdTourneeMobile = l.IdTourneeMobile
WHERE t.DateTournee >= @DateDebutTest
  AND t.DateTournee < @DateFinTest;

DELETE l
FROM Mobile_TourneeLigne l
INNER JOIN Mobile_Tournee t
    ON t.IdTourneeMobile = l.IdTourneeMobile
WHERE t.DateTournee >= @DateDebutTest
  AND t.DateTournee < @DateFinTest;

DELETE logSync
FROM Mobile_LogSynchronisation logSync
INNER JOIN Mobile_Tournee t
    ON t.IdTourneeMobile = logSync.IdTourneeMobile
WHERE t.DateTournee >= @DateDebutTest
  AND t.DateTournee < @DateFinTest;

DELETE
FROM Mobile_Tournee
WHERE DateTournee >= @DateDebutTest
  AND DateTournee < @DateFinTest;

DELETE
FROM Mobile_Livreur
WHERE CodeLivreur LIKE 'TEST%'
   OR CodeLivreur LIKE 'DUP%';
```

## 11. Analyse générale

### GET `/api/tournees/disponibles`

Cette route est légère.

Elle supporte 20 requêtes concurrentes sans erreur.

Elle est adaptée à l’écran de choix de tournée.

### GET `/api/tournees/jour`

Cette route est plus lourde.

Elle supporte :

```text
10 chargements simultanés  -> OK
20 chargements simultanés  -> OK
100 chargements simultanés -> timeout SQL
```

### POST `/api/synchronisations`

Cette route écrit en base.

Elle supporte :

```text
10 envois différents simultanés -> OK
5 envois identiques simultanés  -> 1 succès + 4 conflits 409
```

### Interprétation métier

Les résultats les plus importants sont :

```text
20 chargements complets simultanés réussis le matin
10 envois différents simultanés réussis le soir
Protection anti-doublon validée
```

Cela valide un scénario réaliste de dépôt.

## 12. Limites

Ces tests ont été réalisés en environnement de développement local.

Les temps sont indicatifs :

- PowerShell `Start-Job` ajoute du coût ;
- le PC de développement n’est pas un serveur de production ;
- SQL Server et l’API tournent dans un environnement de test ;
- les vues ABSSolute peuvent être coûteuses selon la charge ;
- les tests POST utilisent des données artificielles.

## 13. Conclusion finale

L’API est fonctionnelle et stable pour un scénario réaliste de chargement simultané.

```text
20 chargements complets simultanés : OK
10 envois différents simultanés   : OK
anti-doublon concurrent           : OK
```

## 14. Améliorations possibles

Pistes à étudier plus tard :

- optimiser la requête SQL de chargement complet ;
- analyser les performances des vues ABSSolute ;
- vérifier les index sur les colonnes utilisées pour les jointures ;
- mesurer les temps côté SQL Server ;
- gérer proprement les timeouts SQL côté API ;
- retourner un message JSON lisible au lieu d’une exception non contrôlée ;
- tester sur une VM proche de l’environnement final ;
- éviter que tous les téléphones chargent exactement au même instant si le nombre de livreurs augmente ;
- envisager une mise en cache courte si le besoin apparaît.

Exemple de réponse plus propre en cas de timeout SQL :

```json
{
  "statut": "ERROR",
  "code": "DATABASE_TIMEOUT",
  "message": "Le chargement de la tournée a pris trop de temps. Veuillez réessayer."
}
```
