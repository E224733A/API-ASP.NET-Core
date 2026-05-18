param(
    [Parameter(Mandatory = $false)]
    [string]$BaseUrl = "http://127.0.0.1:5000",

    [Parameter(Mandatory = $false)]
    [switch]$RunWriteTests
)

$ErrorActionPreference = "Stop"

$BaseUrl = $BaseUrl.TrimEnd("/")
$JsonDir = Join-Path $PSScriptRoot "..\json"

$script:TotalTests = 0
$script:FailedTests = 0
$script:GetResponseJson = $null

function Write-Section {
    param([string]$Title)

    Write-Host ""
    Write-Host "============================================================"
    Write-Host $Title
    Write-Host "============================================================"
}

function Write-TestResult {
    param(
        [string]$Name,
        [bool]$Success,
        [string]$Details = ""
    )

    $script:TotalTests++

    if ($Success) {
        Write-Host "[OK]   $Name"
    }
    else {
        $script:FailedTests++
        Write-Host "[FAIL] $Name"
        if (-not [string]::IsNullOrWhiteSpace($Details)) {
            Write-Host "       $Details"
        }
    }
}

function Invoke-ApiRaw {
    param(
        [string]$Method,
        [string]$Uri,
        [string]$Body = $null
    )

    try {
        $UpperMethod = $Method.ToUpperInvariant()

        if ([string]::IsNullOrWhiteSpace($Body) -or $UpperMethod -eq "GET" -or $UpperMethod -eq "HEAD") {
            $Response = Invoke-WebRequest -Method $Method -Uri $Uri -UseBasicParsing
        }
        else {
            $Response = Invoke-WebRequest -Method $Method -Uri $Uri -UseBasicParsing -ContentType "application/json; charset=utf-8" -Body $Body
        }

        return [pscustomobject]@{
            StatusCode = [int]$Response.StatusCode
            Content = [string]$Response.Content
        }
    }
    catch {
        $HttpResponse = $_.Exception.Response

        if ($null -eq $HttpResponse) {
            return [pscustomobject]@{
                StatusCode = 0
                Content = $_.Exception.Message
            }
        }

        try {
            $Stream = $HttpResponse.GetResponseStream()
            $Reader = New-Object System.IO.StreamReader($Stream)
            $Content = $Reader.ReadToEnd()
            $StatusCode = [int]$HttpResponse.StatusCode
        }
        catch {
            $Content = $_.Exception.Message
            $StatusCode = 0
        }

        return [pscustomobject]@{
            StatusCode = $StatusCode
            Content = [string]$Content
        }
    }
}

function ConvertFrom-JsonSafe {
    param([string]$Content)

    if ([string]::IsNullOrWhiteSpace($Content)) {
        return $null
    }

    try {
        return $Content | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Assert-Status {
    param(
        [string]$Name,
        [object]$Response,
        [int[]]$ExpectedStatusCodes
    )

    $success = $ExpectedStatusCodes -contains $Response.StatusCode
    $expected = ($ExpectedStatusCodes -join ", ")
    Write-TestResult -Name $Name -Success $success -Details "Expected $expected, got $($Response.StatusCode). Content: $($Response.Content)"
}

function Get-EuropeParisOffsetString {
    param([string]$DateTournee)

    $date = [datetime]::ParseExact("$DateTournee 00:05:00", "yyyy-MM-dd HH:mm:ss", [System.Globalization.CultureInfo]::InvariantCulture)

    try {
        $tz = [System.TimeZoneInfo]::FindSystemTimeZoneById("Romance Standard Time")
    }
    catch {
        $tz = [System.TimeZoneInfo]::Local
    }

    $offset = $tz.GetUtcOffset($date)
    $sign = "+"
    if ($offset.TotalMinutes -lt 0) {
        $sign = "-"
        $offset = $offset.Negate()
    }

    return "{0}{1:00}:{2:00}" -f $sign, [int]$offset.TotalHours, $offset.Minutes
}

function New-DeepClone {
    param([object]$Value)

    return ($Value | ConvertTo-Json -Depth 100) | ConvertFrom-Json
}

function New-ExpeditionPayloadFromGet {
    param(
        [object]$GetJson,
        [string]$IdLotVerrouillage,
        [string]$Mode = "VALID"
    )

    if ($null -eq $GetJson -or $null -eq $GetJson.tournees -or $GetJson.tournees.Count -eq 0) {
        throw "Le GET ne retourne aucune tournee preparable."
    }

    $tournee = $GetJson.tournees | Where-Object { $_.lignes -and $_.lignes.Count -gt 0 } | Select-Object -First 1

    if ($null -eq $tournee) {
        throw "Le GET retourne des tournees mais aucune ligne preparable."
    }

    $line = $tournee.lignes | Select-Object -First 1
    $dateTournee = [string]$GetJson.dateTournee

    if ([string]::IsNullOrWhiteSpace($dateTournee)) {
        $dateTournee = [string]$GetJson.datePreparable
    }

    $offset = Get-EuropeParisOffsetString -DateTournee $dateTournee

    $quantites = @(
        [pscustomobject]@{
            codeArticle = "ROLLS"
            quantiteLivreePrevue = 2
        },
        [pscustomobject]@{
            codeArticle = "TAPIS"
            quantiteLivreePrevue = 0
        },
        [pscustomobject]@{
            codeArticle = "SACS"
            quantiteLivreePrevue = $null
        }
    )

    if ($line.preparationInitiale -and $line.preparationInitiale.quantitesPrevues -and $line.preparationInitiale.quantitesPrevues.Count -gt 0) {
        $quantites = @()
        foreach ($q in $line.preparationInitiale.quantitesPrevues) {
            if ($q.codeArticle -in @("ROLLS", "TAPIS", "SACS")) {
                $value = $q.quantiteLivreePrevue
                if ($null -eq $value) {
                    $value = 1
                }
                $quantites += [pscustomobject]@{
                    codeArticle = [string]$q.codeArticle
                    quantiteLivreePrevue = $value
                }
            }
        }
    }

    if ($Mode -eq "ROLLS_VIDES") {
        $quantites += [pscustomobject]@{
            codeArticle = "ROLLS_VIDES"
            quantiteLivreePrevue = 1
        }
    }

    if ($Mode -eq "NEGATIVE") {
        $quantites[0].quantiteLivreePrevue = -1
    }

    $payload = [ordered]@{
        schemaVersion = "1.2"
        idLotVerrouillage = $IdLotVerrouillage
        source = "APPLICATION_WEB_EXPEDITION"
        dateTournee = $dateTournee
        dateVerrouillageDemandee = "$($dateTournee)T00:05:00$offset"
        fuseauHoraireMetier = "Europe/Paris"
        tournees = @(
            [ordered]@{
                codeTournee = [string]$tournee.codeTournee
                libelleTournee = $tournee.libelleTournee
                statutPreparationWeb = "PRETE_VERROUILLAGE"
                lignes = @(
                    [ordered]@{
                        idLigneSource = [string]$line.idLigneSource
                        ordreArret = $line.ordreArret
                        client = $line.client
                        pointLivraison = $line.pointLivraison
                        commentaireExceptionnel = $line.preparationInitiale.commentaireExceptionnel
                        derniereModification = [ordered]@{
                            date = (Get-Date).ToString("yyyy-MM-ddTHH:mm:sszzz")
                            utilisateur = "TEST_API_EXPEDITION"
                        }
                        quantitesPrevues = $quantites
                    }
                )
            }
        )
    }

    if ($Mode -eq "NO_ID_LOT") {
        $payload.Remove("idLotVerrouillage")
    }

    return [pscustomobject]$payload
}

Write-Section "Tests Expedition"
Write-Host "Base URL : $BaseUrl"
Write-Host "JSON dir : $JsonDir"
Write-Host ""

Write-Section "Tests GET"

$getUri = "$BaseUrl/api/expedition/preparations/a-preparer"
$getResponse = Invoke-ApiRaw -Method "GET" -Uri $getUri
Assert-Status -Name "GET global sans filtre" -Response $getResponse -ExpectedStatusCodes @(200)

if ($getResponse.StatusCode -eq 200) {
    $script:GetResponseJson = ConvertFrom-JsonSafe -Content $getResponse.Content

    if ($null -ne $script:GetResponseJson) {
        Write-TestResult -Name "GET JSON parseable" -Success $true

        Write-TestResult -Name "schemaVersion = 1.2" -Success ([string]$script:GetResponseJson.schemaVersion -eq "1.2") -Details "schemaVersion=$($script:GetResponseJson.schemaVersion)"

        $articles = @()
        if ($script:GetResponseJson.regles -and $script:GetResponseJson.regles.articlesAutorises) {
            $articles = @($script:GetResponseJson.regles.articlesAutorises)
        }

        Write-TestResult -Name "ROLLS_VIDES interdit dans les regles" -Success ($script:GetResponseJson.regles.articlesInterdits -contains "ROLLS_VIDES")
        Write-TestResult -Name "Articles Expedition autorises" -Success (($articles -contains "ROLLS") -and ($articles -contains "TAPIS") -and ($articles -contains "SACS"))
    }
    else {
        Write-TestResult -Name "GET JSON parseable" -Success $false -Details "Le contenu de la reponse GET n'est pas un JSON valide."
    }
}

$forbiddenQueries = @(
    "?dateTournee=2026-05-19",
    "?codeTournee=2023",
    "?codeLivreur=2"
)

foreach ($query in $forbiddenQueries) {
    $response = Invoke-ApiRaw -Method "GET" -Uri "$getUri$query"
    Assert-Status -Name "GET avec parametre interdit $query" -Response $response -ExpectedStatusCodes @(400)

    $json = ConvertFrom-JsonSafe -Content $response.Content
    if ($null -ne $json -and $json.code) {
        Write-TestResult -Name "Code erreur attendu pour $query" -Success ([string]$json.code -eq "EXPEDITION_GET_QUERY_PARAMS_FORBIDDEN") -Details "code=$($json.code)"
    }
}

if (-not $RunWriteTests) {
    Write-Section "Tests POST ignores"
    Write-Host "Les tests POST ne sont pas lances."
    Write-Host "Pour les lancer :"
    Write-Host ".\tests\Run-ExpeditionTests.ps1 -BaseUrl `"$BaseUrl`" -RunWriteTests"
}
else {
    Write-Section "Tests POST"
    Write-Host "Attention : ces tests peuvent ecrire en base de developpement."

    if ($null -eq $script:GetResponseJson) {
        Write-TestResult -Name "Preparation du payload POST" -Success $false -Details "Impossible de construire le payload sans reponse GET valide."
    }
    else {
        $timestamp = Get-Date -Format "yyyyMMddHHmmss"

        try {
            $validPayload = New-ExpeditionPayloadFromGet -GetJson $script:GetResponseJson -IdLotVerrouillage "TEST-EXPEDITION-$timestamp-VALID" -Mode "VALID"
            $validBody = $validPayload | ConvertTo-Json -Depth 100

            $validResponse = Invoke-ApiRaw -Method "POST" -Uri "$BaseUrl/api/expedition/preparations/verrouiller" -Body $validBody
            Assert-Status -Name "POST verrouiller valide" -Response $validResponse -ExpectedStatusCodes @(200)

            $replayResponse = Invoke-ApiRaw -Method "POST" -Uri "$BaseUrl/api/expedition/preparations/verrouiller" -Body $validBody
            Assert-Status -Name "POST rejoue identique" -Response $replayResponse -ExpectedStatusCodes @(200)

            $replayJson = ConvertFrom-JsonSafe -Content $replayResponse.Content
            if ($null -ne $replayJson -and $replayJson.code) {
                Write-TestResult -Name "POST rejoue identique retourne ALREADY_PROCESSED" -Success ([string]$replayJson.code -eq "ALREADY_PROCESSED") -Details "code=$($replayJson.code)"
            }

            $mismatchPayload = New-DeepClone -Value $validPayload
            $mismatchPayload.tournees[0].lignes[0].quantitesPrevues[0].quantiteLivreePrevue = 99
            $mismatchBody = $mismatchPayload | ConvertTo-Json -Depth 100
            $mismatchResponse = Invoke-ApiRaw -Method "POST" -Uri "$BaseUrl/api/expedition/preparations/verrouiller" -Body $mismatchBody
            Assert-Status -Name "POST meme idLot avec contenu different" -Response $mismatchResponse -ExpectedStatusCodes @(409)

            $rollsVidesPayload = New-ExpeditionPayloadFromGet -GetJson $script:GetResponseJson -IdLotVerrouillage "TEST-EXPEDITION-$timestamp-ROLLS-VIDES" -Mode "ROLLS_VIDES"
            $rollsVidesBody = $rollsVidesPayload | ConvertTo-Json -Depth 100
            $rollsVidesResponse = Invoke-ApiRaw -Method "POST" -Uri "$BaseUrl/api/expedition/preparations/verrouiller" -Body $rollsVidesBody
            Assert-Status -Name "POST avec ROLLS_VIDES interdit" -Response $rollsVidesResponse -ExpectedStatusCodes @(400)

            $negativePayload = New-ExpeditionPayloadFromGet -GetJson $script:GetResponseJson -IdLotVerrouillage "TEST-EXPEDITION-$timestamp-NEGATIVE" -Mode "NEGATIVE"
            $negativeBody = $negativePayload | ConvertTo-Json -Depth 100
            $negativeResponse = Invoke-ApiRaw -Method "POST" -Uri "$BaseUrl/api/expedition/preparations/verrouiller" -Body $negativeBody
            Assert-Status -Name "POST avec quantite negative" -Response $negativeResponse -ExpectedStatusCodes @(400)

            $noIdPayload = New-ExpeditionPayloadFromGet -GetJson $script:GetResponseJson -IdLotVerrouillage "TEST-EXPEDITION-$timestamp-NO-ID" -Mode "NO_ID_LOT"
            $noIdBody = $noIdPayload | ConvertTo-Json -Depth 100
            $noIdResponse = Invoke-ApiRaw -Method "POST" -Uri "$BaseUrl/api/expedition/preparations/verrouiller" -Body $noIdBody
            Assert-Status -Name "POST sans idLotVerrouillage" -Response $noIdResponse -ExpectedStatusCodes @(400)
        }
        catch {
            Write-TestResult -Name "Execution des tests POST" -Success $false -Details $_.Exception.Message
        }
    }
}

Write-Section "Resultat"
Write-Host "Tests executes : $script:TotalTests"
Write-Host "Echecs        : $script:FailedTests"

if ($script:FailedTests -gt 0) {
    exit 1
}

exit 0
