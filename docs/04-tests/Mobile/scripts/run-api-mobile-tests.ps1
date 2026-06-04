param(
    [string]$ApiBaseUrl = 'http://192.168.1.233:5000',
    [string]$Endpoint = '/api/synchronisations',
    [string]$PayloadsRoot = '',
    [string]$ReportsRoot = '',
    [string]$DateTournee = '',
    [switch]$StopOnFirstFailure
)

$ErrorActionPreference = 'Stop'

function Get-ScriptDirectory {
    if ($PSScriptRoot -and $PSScriptRoot.Trim().Length -gt 0) { return $PSScriptRoot }
    return Split-Path -Parent $MyInvocation.MyCommand.Path
}

function Join-Url([string]$BaseUrl, [string]$RelativeUrl) {
    return $BaseUrl.TrimEnd('/') + '/' + $RelativeUrl.TrimStart('/')
}

function Get-FrenchDayNumber([datetime]$Date) {
    switch ($Date.DayOfWeek.ToString()) {
        'Monday' { return 1 }
        'Tuesday' { return 2 }
        'Wednesday' { return 3 }
        'Thursday' { return 4 }
        'Friday' { return 5 }
        'Saturday' { return 6 }
        'Sunday' { return 7 }
        default { return 0 }
    }
}

function Get-FrenchDayName([datetime]$Date) {
    switch ($Date.DayOfWeek.ToString()) {
        'Monday' { return 'Lundi' }
        'Tuesday' { return 'Mardi' }
        'Wednesday' { return 'Mercredi' }
        'Thursday' { return 'Jeudi' }
        'Friday' { return 'Vendredi' }
        'Saturday' { return 'Samedi' }
        'Sunday' { return 'Dimanche' }
        default { return '' }
    }
}

function Set-JsonProperty([object]$Object, [string]$Name, [object]$Value) {
    if ($null -eq $Object) { return }
    if ($Object.PSObject.Properties.Name -contains $Name) { $Object.$Name = $Value }
    else { $Object | Add-Member -MemberType NoteProperty -Name $Name -Value $Value }
}

function Get-JsonPropertyText([object]$Object, [string]$Name) {
    if ($null -eq $Object) { return '' }
    if (-not ($Object.PSObject.Properties.Name -contains $Name)) { return '' }
    if ($null -eq $Object.$Name) { return '' }
    return [string]$Object.$Name
}

function Test-JsonArrayProperty([object]$Object, [string]$Name) {
    if ($null -eq $Object) { return $false }
    if (-not ($Object.PSObject.Properties.Name -contains $Name)) { return $false }
    $value = $Object.$Name
    if ($null -eq $value) { return $false }
    if ($value -is [System.Array]) { return $true }
    if (($value -is [System.Collections.IEnumerable]) -and (-not ($value -is [string]))) { return $true }
    return $false
}

function Find-PayloadFile([string]$Root, [string]$FileName) {
    $directPath = Join-Path $Root $FileName
    if (Test-Path $directPath) { return $directPath }
    $leafName = Split-Path -Leaf $FileName
    $match = Get-ChildItem -Path $Root -Recurse -File -Filter $leafName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $match) { return $match.FullName }
    return ''
}

function Read-JsonFile([string]$Path) {
    return (Get-Content -Path $Path -Raw -Encoding UTF8) | ConvertFrom-Json
}

function Write-JsonFile([object]$Value, [string]$Path) {
    $Value | ConvertTo-Json -Depth 100 | Set-Content -Path $Path -Encoding UTF8
}

function Convert-BodyToJsonObject([string]$Body) {
    if ([string]::IsNullOrWhiteSpace($Body)) { return $null }
    try { return $Body | ConvertFrom-Json } catch { return $null }
}

function Invoke-ApiRequest([string]$Url, [string]$Method, [string]$JsonBody = '') {
    $statusCode = 0
    $responseBody = ''
    $errorMessage = ''
    try {
        if ([string]::Equals($Method, 'GET', [System.StringComparison]::OrdinalIgnoreCase)) {
            $response = Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing
        }
        else {
            $response = Invoke-WebRequest -Uri $Url -Method Post -ContentType 'application/json; charset=utf-8' -Body $JsonBody -UseBasicParsing
        }
        $statusCode = [int]$response.StatusCode
        $responseBody = [string]$response.Content
    }
    catch {
        $exception = $_.Exception
        $errorMessage = $exception.Message
        if ($null -ne $exception.Response) {
            try { $statusCode = [int]$exception.Response.StatusCode } catch { $statusCode = 0 }
            try {
                $stream = $exception.Response.GetResponseStream()
                if ($null -ne $stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $responseBody = $reader.ReadToEnd()
                    $reader.Close()
                }
            }
            catch { $responseBody = '' }
        }
        if ([string]::IsNullOrWhiteSpace($responseBody) -and $_.ErrorDetails -and $_.ErrorDetails.Message) { $responseBody = $_.ErrorDetails.Message }
    }
    return [pscustomobject]@{ StatusCode = $statusCode; Body = $responseBody; ErrorMessage = $errorMessage }
}

function Update-MobilePayloadForTestRun([object]$Payload, [object]$TestCase, [string]$DateTourneeText, [datetime]$DateTourneeValue, [string]$CodeTournee, [string]$IdSynchronisation, [string]$RunId) {
    $jourNumero = Get-FrenchDayNumber $DateTourneeValue
    $jourLibelle = Get-FrenchDayName $DateTourneeValue
    Set-JsonProperty $Payload 'idSynchronisation' $IdSynchronisation
    Set-JsonProperty $Payload 'dateTournee' $DateTourneeText
    Set-JsonProperty $Payload 'codeTournee' $CodeTournee
    Set-JsonProperty $Payload 'libelleTournee' ('TEST API MOBILE ' + $TestCase.Id)
    Set-JsonProperty $Payload 'commentaireGlobal' ('TEST API MOBILE AUTOMATISE ' + $RunId + ' ' + $TestCase.Id)
    if ($null -ne $Payload.mobile) {
        Set-JsonProperty $Payload.mobile 'nomAppareil' ('Test PowerShell ' + $RunId)
        Set-JsonProperty $Payload.mobile 'versionApplication' '1.0.0-test'
        Set-JsonProperty $Payload.mobile 'dateChargementMobile' ($DateTourneeText + 'T07:30:00+02:00')
        Set-JsonProperty $Payload.mobile 'dateEnvoiMobile' ($DateTourneeText + 'T16:45:00+02:00')
    }
    if ($null -ne $Payload.trajet) {
        if (-not [string]::IsNullOrWhiteSpace((Get-JsonPropertyText $Payload.trajet 'dateDepartMobile'))) { Set-JsonProperty $Payload.trajet 'dateDepartMobile' ($DateTourneeText + 'T07:45:00+02:00') }
        if (-not [string]::IsNullOrWhiteSpace((Get-JsonPropertyText $Payload.trajet 'dateArriveeMobile'))) { Set-JsonProperty $Payload.trajet 'dateArriveeMobile' ($DateTourneeText + 'T16:30:00+02:00') }
    }
    $lignes = @($Payload.lignes)
    for ($indexLigne = 0; $indexLigne -lt $lignes.Count; $indexLigne++) {
        $ligne = $lignes[$indexLigne]
        $numClient = 'CLIENT' + $indexLigne
        $codePDL = 'PDL' + $indexLigne
        if ($null -ne $ligne.client) {
            $clientValue = Get-JsonPropertyText $ligne.client 'numClient'
            if (-not [string]::IsNullOrWhiteSpace($clientValue)) { $numClient = $clientValue }
        }
        if ($null -ne $ligne.pointLivraison) {
            $pdlValue = Get-JsonPropertyText $ligne.pointLivraison 'codePDL'
            if (-not [string]::IsNullOrWhiteSpace($pdlValue)) { $codePDL = $pdlValue }
        }
        $lineIndexForSource = $indexLigne
        if ($TestCase.Mode -eq 'DuplicateLine') { $lineIndexForSource = 0 }
        $idLigneSource = $DateTourneeText + '|' + $CodeTournee + '|' + $jourNumero + '|' + $numClient + '|' + $codePDL + '|' + $lineIndexForSource
        if ($TestCase.Mode -eq 'DuplicateLine') { $idLigneSource = $DateTourneeText + '|' + $CodeTournee + '|' + $jourNumero + '|DUPLICATE|PDL|1' }
        Set-JsonProperty $ligne 'idLigneSource' $idLigneSource
        if ($null -ne $ligne.tournee) {
            Set-JsonProperty $ligne.tournee 'codeTournee' $CodeTournee
            Set-JsonProperty $ligne.tournee 'libelleTournee' ('TEST API MOBILE ' + $TestCase.Id)
            Set-JsonProperty $ligne.tournee 'jourTournee' $jourNumero
            Set-JsonProperty $ligne.tournee 'jourLibelle' $jourLibelle
        }
        if ($null -ne $ligne.retour) {
            Set-JsonProperty $ligne.retour 'jourTourneeRetour' $jourNumero
            Set-JsonProperty $ligne.retour 'jourRetourLibelle' $jourLibelle
            Set-JsonProperty $ligne.retour 'codeTourneeRetour' $CodeTournee
            Set-JsonProperty $ligne.retour 'libelleTourneeRetour' ('TEST API MOBILE ' + $TestCase.Id)
        }
        if ($null -ne $ligne.saisie) {
            $heureValidation = Get-JsonPropertyText $ligne.saisie 'heureValidation'
            if (-not [string]::IsNullOrWhiteSpace($heureValidation)) { Set-JsonProperty $ligne.saisie 'heureValidation' ($DateTourneeText + 'T09:12:00+02:00') }
        }
    }
    return $Payload
}

function New-TestCase {
    param([string]$Id,[int]$Order,[string]$Scenario,[string]$FileName = '',[int]$ExpectedHttp,[string]$ExpectedStatut = '',[string]$ExpectedCode = '',[string]$Mode = 'Normal',[string]$CodeSuffix = '',[string]$Method = 'POST',[string]$RelativeUrl = '',[switch]$RequiresJson,[switch]$RequiresSchemaVersion,[switch]$RequiresCamionsArray)
    return [pscustomobject]@{ Id = $Id; Order = $Order; Scenario = $Scenario; FileName = $FileName; ExpectedHttp = $ExpectedHttp; ExpectedStatut = $ExpectedStatut; ExpectedCode = $ExpectedCode; Mode = $Mode; CodeSuffix = $CodeSuffix; Method = $Method; RelativeUrl = $RelativeUrl; RequiresJson = [bool]$RequiresJson; RequiresSchemaVersion = [bool]$RequiresSchemaVersion; RequiresCamionsArray = [bool]$RequiresCamionsArray }
}

$scriptDirectory = Get-ScriptDirectory
$mobileTestsDirectory = Split-Path -Parent $scriptDirectory
if ([string]::IsNullOrWhiteSpace($PayloadsRoot)) { $PayloadsRoot = Join-Path $mobileTestsDirectory 'payloads' }
if ([string]::IsNullOrWhiteSpace($ReportsRoot)) { $ReportsRoot = Join-Path $mobileTestsDirectory 'rapports' }
if (-not (Test-Path $PayloadsRoot)) { throw ('Dossier payloads introuvable : ' + $PayloadsRoot) }
New-Item -ItemType Directory -Force -Path $ReportsRoot | Out-Null
$sentPayloadsDirectory = Join-Path $ReportsRoot 'payloads-envoyes'
$responsesDirectory = Join-Path $ReportsRoot 'responses'
New-Item -ItemType Directory -Force -Path $sentPayloadsDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $responsesDirectory | Out-Null
$apiUrl = Join-Url $ApiBaseUrl $Endpoint
if ([string]::IsNullOrWhiteSpace($DateTournee)) { $dateTourneeValue = Get-Date } else { $dateTourneeValue = [datetime]::Parse($DateTournee) }
$dateTourneeText = $dateTourneeValue.ToString('yyyy-MM-dd')
$runId = Get-Date -Format 'yyyyMMddHHmmss'
$runCodePrefix = 'M' + (Get-Date -Format 'HHmmss')
$baseSuccessCodeTournee = $runCodePrefix + '01'
$baseSuccessIdSynchronisation = [guid]::NewGuid().ToString()

$testCases = @(
    (New-TestCase -Id 'MOB-API-001' -Order 1 -Scenario 'Synchronisation valide' -FileName 'sync-valide.json' -ExpectedHttp 200 -ExpectedStatut 'SUCCESS' -Mode 'BaseSuccess' -CodeSuffix '01'),
    (New-TestCase -Id 'MOB-API-002' -Order 2 -Scenario 'Doublon technique idSynchronisation' -FileName 'sync-doublon.json' -ExpectedHttp 409 -ExpectedStatut 'CONFLICT' -ExpectedCode 'SYNCHRONISATION_ALREADY_EXISTS' -Mode 'TechnicalDuplicate' -CodeSuffix '01'),
    (New-TestCase -Id 'MOB-API-003' -Order 3 -Scenario 'Double envoi metier date plus tournee' -FileName 'sync-double-envoi-tournee.json' -ExpectedHttp 409 -ExpectedStatut 'CONFLICT' -ExpectedCode 'TOURNEE_ALREADY_SENT' -Mode 'BusinessDuplicate' -CodeSuffix '01'),
    (New-TestCase -Id 'MOB-API-004' -Order 4 -Scenario 'Quantite livree negative' -FileName 'sync-quantite-negative.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '04'),
    (New-TestCase -Id 'MOB-API-005' -Order 5 -Scenario 'NON_FAIT sans commentaire' -FileName 'sync-non-fait-sans-commentaire.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '05'),
    (New-TestCase -Id 'MOB-API-006' -Order 6 -Scenario 'ANOMALIE sans commentaire' -FileName 'sync-anomalie-sans-commentaire.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '06'),
    (New-TestCase -Id 'MOB-API-007' -Order 7 -Scenario 'Ligne validee sans heureValidation' -FileName 'sync-validee-sans-heure.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '07'),
    (New-TestCase -Id 'MOB-API-008' -Order 8 -Scenario 'estValidee false dans envoi final' -FileName 'sync-est-validee-false.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '08'),
    (New-TestCase -Id 'MOB-API-009' -Order 9 -Scenario 'Statut A_FAIRE dans envoi final' -FileName 'sync-a-faire.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '09'),
    (New-TestCase -Id 'MOB-API-010' -Order 10 -Scenario 'idLigneSource duplique' -FileName 'sync-idligne-duplique.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -Mode 'DuplicateLine' -CodeSuffix '10'),
    (New-TestCase -Id 'MOB-API-011' -Order 11 -Scenario 'codeArticle duplique' -FileName 'sync-code-article-duplique.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '11'),
    (New-TestCase -Id 'MOB-API-012' -Order 12 -Scenario 'schemaVersion invalide' -FileName 'sync-schema-version-invalide.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '12'),
    (New-TestCase -Id 'MOB-API-013' -Order 13 -Scenario 'quantites vide' -FileName 'sync-quantites-vide.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '13'),
    (New-TestCase -Id 'MOB-API-014' -Order 14 -Scenario 'quantiteLivreePrevue negative' -FileName 'sync-quantite-prevue-negative.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '14'),
    (New-TestCase -Id 'MOB-API-015' -Order 15 -Scenario 'quantiteLivreePrevue zero acceptee' -FileName 'sync-prevu-zero.json' -ExpectedHttp 200 -ExpectedStatut 'SUCCESS' -CodeSuffix '15'),
    (New-TestCase -Id 'MOB-API-016' -Order 16 -Scenario 'ROLLS_VIDES livre accepte' -FileName 'sync-valide-rolls-vides.json' -ExpectedHttp 200 -ExpectedStatut 'SUCCESS' -CodeSuffix '16'),
    (New-TestCase -Id 'MOB-API-017' -Order 17 -Scenario 'ROLLS_VIDES avec quantite livree positive accepte' -FileName 'sync-rolls-vides-livree-invalide.json' -ExpectedHttp 200 -ExpectedStatut 'SUCCESS' -CodeSuffix '17'),
    (New-TestCase -Id 'MOB-API-018' -Order 18 -Scenario 'ROLLS_VIDES avec quantite prevue positive accepte' -FileName 'sync-rolls-vides-prevue-invalide.json' -ExpectedHttp 200 -ExpectedStatut 'SUCCESS' -CodeSuffix '18'),
    # Tests preparatoires couvrant la future version 1.3 trajet/camion. Ils peuvent rester KO tant que l'API 1.3 et la route GET /api/camions/disponibles ne sont pas encore codees.
    (New-TestCase -Id 'MOB-API-019' -Order 19 -Scenario 'GET camions disponibles' -ExpectedHttp 200 -Method 'GET' -RelativeUrl '/api/camions/disponibles' -RequiresJson -RequiresSchemaVersion -RequiresCamionsArray),
    (New-TestCase -Id 'MOB-API-020' -Order 20 -Scenario 'schemaVersion 1.3 avec trajet et camion complet' -FileName 'valides/sync-valide-v13-trajet-camion.json' -ExpectedHttp 200 -ExpectedStatut 'SUCCESS' -CodeSuffix '20'),
    (New-TestCase -Id 'MOB-API-021' -Order 21 -Scenario 'schemaVersion 1.3 sans objet trajet' -FileName 'invalides/sync-v13-sans-trajet.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '21'),
    (New-TestCase -Id 'MOB-API-022' -Order 22 -Scenario 'schemaVersion 1.3 avec trajet sans camion' -FileName 'invalides/sync-v13-sans-camion.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '22'),
    (New-TestCase -Id 'MOB-API-023' -Order 23 -Scenario 'schemaVersion 1.3 avec camion sans idCamion' -FileName 'invalides/sync-v13-sans-id-camion.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '23'),
    (New-TestCase -Id 'MOB-API-024' -Order 24 -Scenario 'schemaVersion 1.3 sans kilometrageDepart' -FileName 'invalides/sync-v13-sans-km-depart.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '24'),
    (New-TestCase -Id 'MOB-API-025' -Order 25 -Scenario 'schemaVersion 1.3 sans kilometrageArrivee' -FileName 'invalides/sync-v13-sans-km-arrivee.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '25'),
    (New-TestCase -Id 'MOB-API-026' -Order 26 -Scenario 'schemaVersion 1.3 avec kilometrageDepart negatif' -FileName 'invalides/sync-v13-km-depart-negatif.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '26'),
    (New-TestCase -Id 'MOB-API-027' -Order 27 -Scenario 'schemaVersion 1.3 avec kilometrageArrivee negatif' -FileName 'invalides/sync-v13-km-arrivee-negatif.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '27'),
    (New-TestCase -Id 'MOB-API-028' -Order 28 -Scenario 'schemaVersion 1.3 avec kilometrageArrivee inferieur au depart' -FileName 'invalides/sync-v13-km-arrivee-inferieur-depart.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '28'),
    (New-TestCase -Id 'MOB-API-029' -Order 29 -Scenario 'schemaVersion 1.3 sans dateDepartMobile' -FileName 'invalides/sync-v13-sans-date-depart.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '29'),
    (New-TestCase -Id 'MOB-API-030' -Order 30 -Scenario 'schemaVersion 1.3 sans dateArriveeMobile' -FileName 'invalides/sync-v13-sans-date-arrivee.json' -ExpectedHttp 400 -ExpectedStatut 'VALIDATION_ERROR' -CodeSuffix '30'),
    (New-TestCase -Id 'MOB-API-031' -Order 31 -Scenario 'schemaVersion 1.2 sans trajet compatible phase transitoire' -FileName 'valides/sync-v12-sans-trajet-compatible.json' -ExpectedHttp 200 -ExpectedStatut 'SUCCESS' -CodeSuffix '31')
)

Write-Host ''
Write-Host '=== Tests API Mobile SLI v1.2 / v1.3 ==='
Write-Host ('API          : ' + $ApiBaseUrl.TrimEnd('/'))
Write-Host ('POST sync    : ' + $apiUrl)
Write-Host ('Payloads     : ' + $PayloadsRoot)
Write-Host ('Rapports     : ' + $ReportsRoot)
Write-Host ('Date tournee : ' + $dateTourneeText)
Write-Host ('RunId        : ' + $runId)
Write-Host ''

$results = @()
foreach ($testCase in ($testCases | Sort-Object Order)) {
    $targetUrl = $apiUrl
    if (-not [string]::IsNullOrWhiteSpace($testCase.RelativeUrl)) { $targetUrl = Join-Url $ApiBaseUrl $testCase.RelativeUrl }
    $safeId = $testCase.Id.Replace('/', '-').Replace('\', '-')
    $safeFileName = ([string]$testCase.FileName).Replace('/', '-').Replace('\', '-')
    if ([string]::IsNullOrWhiteSpace($safeFileName)) { $safeFileName = 'request' }
    $responseFile = Join-Path $responsesDirectory ($safeId + '-response.json')
    $sentPayloadFile = ''
    $jsonBody = ''
    if ([string]::Equals($testCase.Method, 'POST', [System.StringComparison]::OrdinalIgnoreCase)) {
        $payloadPath = Find-PayloadFile $PayloadsRoot $testCase.FileName
        if ([string]::IsNullOrWhiteSpace($payloadPath)) {
            $result = [pscustomobject]@{ Id = $testCase.Id; Scenario = $testCase.Scenario; FileName = $testCase.FileName; ExpectedHttp = $testCase.ExpectedHttp; ActualHttp = 0; ExpectedStatut = $testCase.ExpectedStatut; ActualStatut = ''; ExpectedCode = $testCase.ExpectedCode; ActualCode = ''; Result = 'SKIPPED'; Message = 'Payload introuvable'; SentPayloadFile = ''; ResponseFile = '' }
            $results += $result
            Write-Host ('SKIP ' + $testCase.Id + ' - payload introuvable : ' + $testCase.FileName) -ForegroundColor Yellow
            continue
        }
        $payload = Read-JsonFile $payloadPath
        $codeTournee = $runCodePrefix + $testCase.CodeSuffix
        $idSynchronisation = [guid]::NewGuid().ToString()
        if ($testCase.Mode -eq 'BaseSuccess') { $codeTournee = $baseSuccessCodeTournee; $idSynchronisation = $baseSuccessIdSynchronisation }
        if ($testCase.Mode -eq 'TechnicalDuplicate') { $codeTournee = $baseSuccessCodeTournee; $idSynchronisation = $baseSuccessIdSynchronisation }
        if ($testCase.Mode -eq 'BusinessDuplicate') { $codeTournee = $baseSuccessCodeTournee; $idSynchronisation = [guid]::NewGuid().ToString() }
        $payload = Update-MobilePayloadForTestRun $payload $testCase $dateTourneeText $dateTourneeValue $codeTournee $idSynchronisation $runId
        $sentPayloadFile = Join-Path $sentPayloadsDirectory ($safeId + '-' + $safeFileName)
        Write-JsonFile $payload $sentPayloadFile
        $jsonBody = Get-Content -Path $sentPayloadFile -Raw -Encoding UTF8
    }
    Write-Host ('RUN  ' + $testCase.Id + ' - ' + $testCase.Scenario)
    $response = Invoke-ApiRequest $targetUrl $testCase.Method $jsonBody
    Set-Content -Path $responseFile -Value $response.Body -Encoding UTF8
    $responseJson = Convert-BodyToJsonObject $response.Body
    $actualStatut = Get-JsonPropertyText $responseJson 'statut'
    $actualCode = Get-JsonPropertyText $responseJson 'code'
    $httpOk = $response.StatusCode -eq $testCase.ExpectedHttp
    $statutOk = $true
    $codeOk = $true
    $jsonOk = $true
    $schemaVersionOk = $true
    $camionsArrayOk = $true
    if (-not [string]::IsNullOrWhiteSpace($testCase.ExpectedStatut)) { $statutOk = [string]::Equals($actualStatut, $testCase.ExpectedStatut, [System.StringComparison]::OrdinalIgnoreCase) }
    if (-not [string]::IsNullOrWhiteSpace($testCase.ExpectedCode)) { $codeOk = [string]::Equals($actualCode, $testCase.ExpectedCode, [System.StringComparison]::OrdinalIgnoreCase) }
    if ($testCase.RequiresJson) { $jsonOk = $null -ne $responseJson }
    if ($testCase.RequiresSchemaVersion) { $schemaVersionOk = -not [string]::IsNullOrWhiteSpace((Get-JsonPropertyText $responseJson 'schemaVersion')) }
    if ($testCase.RequiresCamionsArray) { $camionsArrayOk = Test-JsonArrayProperty $responseJson 'camions' }
    $resultStatus = 'OK'
    if (-not ($httpOk -and $statutOk -and $codeOk -and $jsonOk -and $schemaVersionOk -and $camionsArrayOk)) { $resultStatus = 'KO' }
    $message = ''
    if ($resultStatus -eq 'KO') {
        $message = 'Attendu HTTP=' + $testCase.ExpectedHttp + ', statut=' + $testCase.ExpectedStatut + ', code=' + $testCase.ExpectedCode + '. Obtenu HTTP=' + $response.StatusCode + ', statut=' + $actualStatut + ', code=' + $actualCode + '.'
        if ($testCase.RequiresJson -and (-not $jsonOk)) { $message += ' JSON non parseable.' }
        if ($testCase.RequiresSchemaVersion -and (-not $schemaVersionOk)) { $message += ' schemaVersion absent.' }
        if ($testCase.RequiresCamionsArray -and (-not $camionsArrayOk)) { $message += ' camions absent ou non tableau.' }
        if (-not [string]::IsNullOrWhiteSpace($response.ErrorMessage)) { $message += ' Erreur=' + $response.ErrorMessage }
    }
    $result = [pscustomobject]@{ Id = $testCase.Id; Scenario = $testCase.Scenario; FileName = $testCase.FileName; ExpectedHttp = $testCase.ExpectedHttp; ActualHttp = $response.StatusCode; ExpectedStatut = $testCase.ExpectedStatut; ActualStatut = $actualStatut; ExpectedCode = $testCase.ExpectedCode; ActualCode = $actualCode; Result = $resultStatus; Message = $message; SentPayloadFile = $sentPayloadFile; ResponseFile = $responseFile }
    $results += $result
    if ($resultStatus -eq 'OK') { Write-Host ('OK   ' + $testCase.Id + ' HTTP ' + $response.StatusCode) -ForegroundColor Green }
    else {
        Write-Host ('KO   ' + $testCase.Id + ' HTTP ' + $response.StatusCode) -ForegroundColor Red
        Write-Host $message -ForegroundColor Red
        if ($StopOnFirstFailure) { break }
    }
}

$csvPath = Join-Path $ReportsRoot 'resultats-tests-api-mobile.csv'
$jsonPath = Join-Path $ReportsRoot 'resultats-tests-api-mobile.json'
$mdPath = Join-Path $ReportsRoot 'rapport-tests-api-mobile.md'
$results | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8 -Delimiter ';'
$results | ConvertTo-Json -Depth 10 | Set-Content -Path $jsonPath -Encoding UTF8
$totalCount = @($results).Count
$okCount = @($results | Where-Object { $_.Result -eq 'OK' }).Count
$koCount = @($results | Where-Object { $_.Result -eq 'KO' }).Count
$skippedCount = @($results | Where-Object { $_.Result -eq 'SKIPPED' }).Count
$reportLines = @()
$reportLines += '# Rapport tests API Mobile SLI v1.2 / v1.3'
$reportLines += ''
$reportLines += ('Date execution : ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$reportLines += ''
$reportLines += ('API testee : ' + $ApiBaseUrl.TrimEnd('/'))
$reportLines += ''
$reportLines += ('Endpoint POST synchronisation : ' + $apiUrl)
$reportLines += ''
$reportLines += ('Date tournee utilisee : ' + $dateTourneeText)
$reportLines += ''
$reportLines += ('RunId : ' + $runId)
$reportLines += ''
$reportLines += '## Synthese'
$reportLines += ''
$reportLines += '| Indicateur | Valeur |'
$reportLines += '|---|---:|'
$reportLines += ('| Total | ' + $totalCount + ' |')
$reportLines += ('| OK | ' + $okCount + ' |')
$reportLines += ('| KO | ' + $koCount + ' |')
$reportLines += ('| SKIPPED | ' + $skippedCount + ' |')
$reportLines += ''
$reportLines += '## Detail'
$reportLines += ''
$reportLines += '| ID | Scenario | HTTP attendu | HTTP obtenu | Statut attendu | Statut obtenu | Code attendu | Code obtenu | Resultat |'
$reportLines += '|---|---|---:|---:|---|---|---|---|---|'
foreach ($result in $results) {
    $scenario = ([string]$result.Scenario).Replace('|', '/')
    $expectedCode = [string]$result.ExpectedCode
    $actualCode = [string]$result.ActualCode
    if ([string]::IsNullOrWhiteSpace($expectedCode)) { $expectedCode = '-' }
    if ([string]::IsNullOrWhiteSpace($actualCode)) { $actualCode = '-' }
    $reportLines += ('| ' + $result.Id + ' | ' + $scenario + ' | ' + $result.ExpectedHttp + ' | ' + $result.ActualHttp + ' | ' + $result.ExpectedStatut + ' | ' + $result.ActualStatut + ' | ' + $expectedCode + ' | ' + $actualCode + ' | ' + $result.Result + ' |')
}
$reportLines += ''
$reportLines += '## Fichiers generes'
$reportLines += ''
$reportLines += ('- CSV : ' + $csvPath)
$reportLines += ('- JSON : ' + $jsonPath)
$reportLines += ('- Payloads envoyes : ' + $sentPayloadsDirectory)
$reportLines += ('- Reponses API : ' + $responsesDirectory)
$reportLines += ''
$failedResults = @($results | Where-Object { $_.Result -eq 'KO' })
if ($failedResults.Count -gt 0) {
    $reportLines += '## Erreurs'
    $reportLines += ''
    foreach ($failed in $failedResults) {
        $reportLines += ('### ' + $failed.Id)
        $reportLines += ''
        $reportLines += $failed.Message
        $reportLines += ''
        if (-not [string]::IsNullOrWhiteSpace($failed.SentPayloadFile)) { $reportLines += ('Payload envoye : ' + $failed.SentPayloadFile); $reportLines += '' }
        $reportLines += ('Reponse API : ' + $failed.ResponseFile)
        $reportLines += ''
    }
}
$reportLines | Set-Content -Path $mdPath -Encoding UTF8
Write-Host ''
Write-Host '=== Fin des tests API Mobile ==='
Write-Host ('Total   : ' + $totalCount)
Write-Host ('OK      : ' + $okCount) -ForegroundColor Green
Write-Host ('KO      : ' + $koCount) -ForegroundColor Red
Write-Host ('SKIPPED : ' + $skippedCount) -ForegroundColor Yellow
Write-Host ''
Write-Host ('Rapport : ' + $mdPath)
Write-Host ('CSV     : ' + $csvPath)
Write-Host ('JSON    : ' + $jsonPath)
Write-Host ''
if ($koCount -gt 0) { exit 1 }
exit 0
