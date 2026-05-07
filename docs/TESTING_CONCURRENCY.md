# Tests de concurrence — API tournées mobile

## 1. Objectif des tests

Ces tests servent à vérifier le comportement de l’API ASP.NET Core lorsque plusieurs livreurs chargent leur tournée en même temps le matin.

Dans le fonctionnement prévu de l’application mobile, les livreurs utilisent principalement deux routes au moment du chargement :

```text
GET /api/tournees/disponibles
GET /api/tournees/jour
```

La route `GET /api/tournees/disponibles` est utilisée pour afficher la liste des tournées disponibles pour une date et un livreur.

La route `GET /api/tournees/jour` est utilisée pour charger le détail complet d’une tournée dans l’application mobile. Elle est plus lourde, car elle récupère les clients, les points de livraison, les informations de retour, les zones, les fermetures, les articles et les données nécessaires à la saisie mobile.

L’objectif n’est pas de faire un test de production complet, mais de vérifier que l’API reste stable dans un scénario réaliste de chargement simultané.


## 2. Environnement de test

Les tests ont été exécutés en environnement de développement local.

API utilisée :

```text
http://localhost:5120
```

Vérification de disponibilité :

```powershell
curl.exe "http://localhost:5120/api/health"
```

Réponse obtenue :

```json
{
  "service": "API-ASP.NET-Core",
  "status": "ok",
  "environment": "Development",
  "date": "2026-05-07T11:31:03.1368547+02:00"
}
```

L’API doit rester lancée dans un terminal avec :

```powershell
dotnet run
```

Le fait que l’API continue de tourner est normal : une API web reste en écoute pour répondre aux requêtes HTTP tant qu’elle n’est pas arrêtée avec `Ctrl + C`.


## 3. Points importants avant de tester

### 3.1 Vérifier le bon port

Le port utilisé pendant les tests est :

```text
5120
```

Une erreur de port provoque des échecs de connexion.

Exemple d’erreur rencontrée :

```text
Impossible de se connecter au serveur distant
```

Cause observée :

```text
http://localhost:5012
```

au lieu de :

```text
http://localhost:5120
```

Avant de lancer un test de concurrence, toujours vérifier :

```powershell
curl.exe "http://localhost:5120/api/health"
```

### 3.2 Différence entre 404 et 500

Une erreur `404` signifie généralement que la combinaison testée n’existe pas :

```text
dateTournee + codeTournee + codeLivreur
```

Ce n’est pas forcément un problème de concurrence.

Une erreur `500` est plus importante. Dans les tests réalisés, les erreurs `500` observées pendant le stress test venaient de timeouts SQL.


## 4. Test 1 — 20 requêtes concurrentes sur `/api/tournees/disponibles`

### 4.1 Objectif

Ce test simule plusieurs téléphones qui demandent en même temps la liste des tournées disponibles.

Route testée :

```text
GET /api/tournees/disponibles?dateTournee=2026-05-07&codeLivreur=2
```

### 4.2 Script PowerShell

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

### 4.3 Résultat obtenu

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

### 4.4 Conclusion

La route `GET /api/tournees/disponibles` supporte 20 requêtes concurrentes sans erreur.

Cette route est adaptée à l’écran de choix de tournée.


## 5. Test 2 — Vérification individuelle du chargement complet

### 5.1 Objectif

Avant de tester la concurrence sur le chargement complet, il faut vérifier que chaque tournée existe bien et répond correctement seule.

Route testée :

```text
GET /api/tournees/jour?dateTournee=2026-05-07&codeTournee=XXXX&codeLivreur=2
```

### 5.2 Tournées disponibles trouvées

```text
4001 — POUZAUGES LES HERBIERS
4002 — MORBIHAN SAINT NAZAIRE
4003 — ST NAZAIRE
4004 — LES SABLES PL
4005 — TOURNEE VTS PL
4006 — BOUAYE
4007 — FONTENAY CHATAIGNERAIE
4011 — LES SABLES
4012 — MORBIHAN VL 2
4013 — TOURNEE VETEMENT MONTAIGU
4014 — MDR VENDEE
4015 — MORBIHAN VL
4016 — SABLES VL 2
4017 — NANTES
4018 — CHALLANS
4019 — SAINT GILLES
4020 — ECOLES NANTES
4021 — SAINT JEAN
4022 — MDR CHANTONNAY
4023 — MOTHE ACHARD
```

### 5.3 Résultat obtenu

Chaque tournée testée individuellement a répondu :

```text
HTTP 200
```

### 5.4 Conclusion

Les tournées existent bien pour la date `2026-05-07` et le livreur `2`.

La route `GET /api/tournees/jour` fonctionne correctement hors surcharge.


## 6. Test 3 — Chargements complets simultanés sur `/api/tournees/jour`

### 6.1 Objectif

Ce test simule plusieurs livreurs qui chargent chacun une tournée complète en même temps.

Route testée :

```text
GET /api/tournees/jour
```

Contrairement à `/api/tournees/disponibles`, cette route est plus coûteuse car elle construit tout le JSON nécessaire à l’application mobile.

### 6.2 Script PowerShell

Le script ci-dessous peut être utilisé avec 5, 10 ou 20 tournées en modifiant simplement la variable `$codesTournees`.

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


## 7. Résultat avec 10 chargements complets simultanés

### 7.1 Tournées testées

```text
4001
4002
4003
4004
4005
4006
4007
4011
4012
4013
```

### 7.2 Résultat obtenu

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

### 7.3 Détail

```text
4001 — HTTP 200
4002 — HTTP 200
4003 — HTTP 200
4004 — HTTP 200
4005 — HTTP 200
4006 — HTTP 200
4007 — HTTP 200
4011 — HTTP 200
4012 — HTTP 200
4013 — HTTP 200
```

### 7.4 Conclusion

L’API supporte 10 chargements complets simultanés de tournées dans l’environnement de développement.


## 8. Résultat avec 20 chargements complets simultanés

### 8.1 Tournées testées

```text
4001
4002
4003
4004
4005
4006
4007
4011
4012
4013
4014
4015
4016
4017
4018
4019
4020
4021
4022
4023
```

### 8.2 Résultat obtenu

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

### 8.3 Détail

```text
4001 — HTTP 200
4002 — HTTP 200
4003 — HTTP 200
4004 — HTTP 200
4005 — HTTP 200
4006 — HTTP 200
4007 — HTTP 200
4011 — HTTP 200
4012 — HTTP 200
4013 — HTTP 200
4014 — HTTP 200
4015 — HTTP 200
4016 — HTTP 200
4017 — HTTP 200
4018 — HTTP 200
4019 — HTTP 200
4020 — HTTP 200
4021 — HTTP 200
4022 — HTTP 200
4023 — HTTP 200
```

### 8.4 Conclusion

L’API supporte 20 chargements complets simultanés de tournées dans l’environnement de développement.

Ce test est le plus représentatif du besoin métier : plusieurs livreurs chargent leur tournée en même temps au dépôt.


## 9. Stress test à 100 chargements complets simultanés

### 9.1 Objectif

Ce test a été lancé pour pousser volontairement l’API au-delà d’un scénario métier classique.

Il a consisté à envoyer 100 chargements complets simultanés sur :

```text
GET /api/tournees/jour
```

### 9.2 Résultat obtenu

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

### 9.3 Erreur observée

Les échecs ont retourné des erreurs HTTP 500.

Erreur côté API :

```text
Microsoft.Data.SqlClient.SqlException :
Le délai d'exécution a expiré.
Le délai d'attente s'est écoulé avant la fin de l'opération ou le serveur ne répond pas.
```

Erreur localisée dans :

```text
TourneesRepository.GetTourneeLinesAsync
Dapper.QueryAsync
```

### 9.4 Interprétation

Le stress test à 100 chargements complets simultanés dépasse les capacités actuelles de l’environnement de développement ou des vues SQL utilisées.

Le problème observé est un timeout SQL.

Ce résultat ne remet pas en cause le fonctionnement normal de l’application mobile, car 100 chargements complets simultanés représentent une charge très supérieure au scénario métier attendu.


## 10. Test 4 — Envoi du soir avec 10 synchronisations différentes

### 10.1 Objectif

Ce test simule plusieurs livreurs qui envoient chacun une tournée différente en même temps en fin de journée.

Route testée :

```text
POST /api/synchronisations
```

Contrairement aux tests du matin, cette route écrit en base.

Le test utilise donc :

- une date de test ;
- des codes tournées de test ;
- des codes livreurs de test ;
- des identifiants de synchronisation uniques.

### 10.2 Script PowerShell

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
        schemaVersion = "1.1"
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
                client = [PSCustomObject]@{
                    numClient = "CLIENT$numero"
                    nomClient = "CLIENT TEST $numero"
                    nomAffiche = "CLIENT TEST $numero"
                }
                pointLivraison = [PSCustomObject]@{
                    codePDL = "PDL$numero"
                    descriptionPDL = "POINT TEST $numero"
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
                            quantiteLivree = 1
                            quantiteRecuperee = 1
                        },
                        [PSCustomObject]@{
                            codeArticle = "TAPIS"
                            libelle = "Tapis"
                            quantiteLivree = 2
                            quantiteRecuperee = 0
                        },
                        [PSCustomObject]@{
                            codeArticle = "SACS"
                            libelle = "Sacs"
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

### 10.3 Résultat obtenu

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

### 10.4 Détail

```text
TS142442001 — TEST001 — HTTP 200
TS142442002 — TEST002 — HTTP 200
TS142442003 — TEST003 — HTTP 200
TS142442004 — TEST004 — HTTP 200
TS142442005 — TEST005 — HTTP 200
TS142442006 — TEST006 — HTTP 200
TS142442007 — TEST007 — HTTP 200
TS142442008 — TEST008 — HTTP 200
TS142442009 — TEST009 — HTTP 200
TS142442010 — TEST010 — HTTP 200
```

### 10.5 Conclusion

L’API supporte 10 envois simultanés différents sur `POST /api/synchronisations`.

Ce résultat est satisfaisant pour le scénario d’envoi du soir.


## 11. Test 5 — Anti-doublon sur un envoi identique

### 11.1 Objectif

Ce test vérifie que l’API bloque correctement les doubles envois.

Le même JSON de synchronisation est envoyé 5 fois en parallèle.

Comportement attendu :

```text
1 requête acceptée en HTTP 200
4 requêtes refusées en HTTP 409
```

### 11.2 Script PowerShell

```powershell
$baseUrl = "http://localhost:5120"
$dateTournee = "2099-01-02"
$nbRequetes = 5

$idSynchronisation = [guid]::NewGuid().ToString()
$codeTournee = "DUPTEST001"
$codeLivreur = "DUP001"

$payload = [PSCustomObject]@{
    schemaVersion = "1.1"
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
            client = [PSCustomObject]@{
                numClient = "CLIENT001"
                nomClient = "CLIENT DOUBLON"
                nomAffiche = "CLIENT DOUBLON"
            }
            pointLivraison = [PSCustomObject]@{
                codePDL = "PDL001"
                descriptionPDL = "POINT DOUBLON"
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

### 11.3 Résultat obtenu

```text
Numero Succes StatusCode TempsMs
------ ------ ---------- -------
1      True   200        3827
2      False  409        3578
3      False  409        3054
4      False  409        2489
5      False  409        388
```

Regroupement par code HTTP :

```text
Name Count
---- -----
200      1
409      4
```

### 11.4 Interprétation

Une seule synchronisation est enregistrée.

Les autres requêtes sont refusées avec le code HTTP `409 Conflict`.

Dans ce test, PowerShell affiche `Succes = False` pour les réponses `409`, car `Invoke-WebRequest` considère les codes d’erreur HTTP comme des exceptions.

Fonctionnellement, les `409` sont attendus et valident la protection anti-doublon.

### 11.5 Conclusion

La protection anti-doublon fonctionne correctement.

L’API empêche qu’une même synchronisation soit enregistrée plusieurs fois.


## 12. Nettoyage des données de test

Les tests sur `POST /api/synchronisations` écrivent en base.

Après les tests, les données de test peuvent être supprimées avec le script SQL suivant.

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


## 13. Analyse générale

### 13.1 Route `/api/tournees/disponibles`

Cette route est légère.

Elle supporte 20 requêtes concurrentes sans erreur.

Elle est adaptée à l’écran de choix de tournée.

### 13.2 Route `/api/tournees/jour`

Cette route est plus lourde.

Elle charge une tournée complète et construit le JSON utilisé par l’application mobile.

Elle supporte :

```text
10 chargements simultanés : OK
20 chargements simultanés : OK
100 chargements simultanés : timeout SQL
```

### 13.3 Route `/api/synchronisations`

Cette route écrit en base.

Elle supporte :

```text
10 envois différents simultanés : OK
5 envois identiques simultanés : 1 succès + 4 conflits 409
```

### 13.4 Interprétation métier

Les résultats les plus importants sont :

```text
20 chargements complets simultanés réussis le matin
10 envois différents simultanés réussis le soir
Protection anti-doublon validée
```

Cela permet de valider que l’API peut gérer un scénario réaliste où plusieurs livreurs chargent leur tournée au dépôt le matin, puis renvoient leurs données en fin de journée.


## 14. Limites du test

Ces tests ont été réalisés en environnement de développement local.

Les temps mesurés sont indicatifs pour plusieurs raisons :

- PowerShell `Start-Job` ajoute du coût ;
- les jobs sont lancés dans des processus séparés ;
- le PC de développement n’est pas un serveur de production ;
- SQL Server et l’API tournent dans un environnement de test ;
- les vues ABSSolute peuvent être plus ou moins coûteuses selon la charge ;
- les tests POST utilisent des données artificielles.

Le temps total affiché par le script peut être supérieur au temps maximal d’une requête, car il inclut aussi le lancement, l’attente et la récupération des jobs PowerShell.


## 12. Conclusion finale

Les tests de concurrence montrent que l’API est fonctionnelle et stable pour un scénario réaliste de chargement simultané.


```text
L’API supporte 20 chargements complets simultanés de tournées dans l’environnement de développement.
Ce résultat est satisfaisant pour une première version destinée aux livreurs, si le nombre réel de chargements simultanés reste proche de cet ordre de grandeur.
```


## 13. Améliorations possibles

Pour améliorer encore la robustesse sous forte charge, plusieurs pistes peuvent être étudiées plus tard :

- optimiser la requête SQL de chargement complet ;
- analyser les performances des vues ABSSolute ;
- vérifier les index sur les colonnes utilisées pour les jointures ;
- mesurer les temps directement côté SQL Server ;
- ajouter une gestion propre des timeouts SQL côté API ;
- retourner un message JSON lisible au lieu d’une exception non gérée ;
- tester sur une VM ou un serveur plus proche de l’environnement final ;
- éviter que tous les téléphones chargent exactement au même instant si l’entreprise a beaucoup de livreurs ;
- envisager une mise en cache courte des données de tournée si le besoin apparaît.


## 14. Exemple d’erreur à gérer plus proprement plus tard

Actuellement, un timeout SQL peut provoquer une erreur HTTP 500 non contrôlée.

À terme, l’API pourrait retourner une réponse plus lisible :

```json
{
  "statut": "ERROR",
  "code": "DATABASE_TIMEOUT",
  "message": "Le chargement de la tournée a pris trop de temps. Veuillez réessayer."
}
```

Cela permettrait à l’application mobile d’afficher un message compréhensible pour le livreur.