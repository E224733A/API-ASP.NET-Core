param(
    [Parameter(Mandatory = $false)]
    [string]$ApiBaseUrl = 'http://192.168.1.233:5000',

    [Parameter(Mandatory = $false)]
    [string]$Endpoint = '/api/synchronisations',

    [Parameter(Mandatory = $false)]
    [string]$PayloadsRoot = '',

    [Parameter(Mandatory = $false)]
    [string]$ReportsRoot = '',

    [Parameter(Mandatory = $false)]
    [string]$DateTournee = '',

    [Parameter(Mandatory = $false)]
    [switch]$StopOnFirstFailure
)

$ErrorActionPreference = 'Stop'

function Get-ScriptDirectory {
    if ($PSScriptRoot -and $PSScriptRoot.Trim().Length -gt 0) {
        return $PSScriptRoot
    }

    return Split-Path -Parent $MyInvocation.MyCommand.Path
}

function Join-Url {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$relativeUrl
    )

    return $BaseUrl.TrimEnd('/') + '/' + $RelativeUrl.TrimStart('/')
}

function Get-FrenchDayNumber {
    param([Parameter(Mandatory = $true)][datetime]$Date)

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

function Get-FrenchDayName {
    param([Parameter(Mandatory = $true)][datetime]$date)

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

function Set-JsonProperty {
    param(
        [Parameter(Mandatory = $true)][object]$Object,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $false)][object]$Value
    )

    if ($null -eq $Object) {
        return
    }

    if ($Object.PSObject.Properties.Name -contains $Name) {
        $Object.$Name = $Value
    }
    else {
        $Object | Add-Member -MemberType NoteProperty -Name $Name -Value $Value
    }
}

function Get-JsonPropertyText {
    param(
        [Parameter(Mandatory = $false)][object]$Object,
        [Parameter(Mandatory = $true)][string]$name
    )

    if ($null -eq $Object) {
        return ''
    }

    if ($Object.PSObject.Properties.Name -contains $Name) {
        $value = $Object.$Name

        if ($null -eq $value) {
            return ''
        }

        return [string]$value
    }

    return ''
}

function Test-JsonArrayProperty {
    param(
        [Parameter(Mandatory = $false)][object]$Object,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $Object) {
        return $false
    }

    if (-not ($Object.PSObject.Properties.Name -contains $Name)) {
        return $false
    }

    $value = $Object.$Name

    if ($null -eq $value) {
        return $false
    }

    if ($value -is [System.Array]) {
        return $true
    }

    if (($value -is [System.Collections.IEnumerable]) -and (-not ($value -is [string])) {
        return $true
    }

    return $false
}

function Find-PayloadFile {
    param(
        [Parameter(Mandatory = $true)][string]$root,
        [Parameter(Mandatory = $true)][string]$FileName
    )

    $directPath = Join-Path $Root $FileName

    if (Test-Path $directPath) {
        return $directPath
    }

    $leafName = Split-Path -Leaf $FileName
    $match = Get-ChildItem -Path $Root -Recurse -File -Filter $leafName -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($null -ne $match) {
        return $match.FullName
    }

    return ''
}

function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $raw = Get-Content -Path $Path -Raw -Encoding UTF8
    return $raw | ConvertFrom-Json
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)][object]$Value,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $json = $Value | ConvertTo-Json -Depth 100
    Set-Content -Path $Path -Value $json -Encoding UTF8
}

function Convert-BodyToJsonObject {
    param([Parameter(Mandatory = $false)][string]$body)

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $null
    }

    try {
        return $Body | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Invoke-ApiRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $false)][string]$JsonBody = ''
    )

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
            try {
                $statusCode = [int]$exception.Response.StatusCode
            }
            catch {
                $statusCode = 0
            }

            try {
                $stream = $exception.Response.GetResponseStream()
                if ($null -ne $stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $responseBody = $reader.ReadToEnd()
                    $reader.Close()
                }
            }
            catch {
                $responseBody = ''
            }
        }

        if ([string]::IsNullOrWhiteSpace($responseBody) -and $_.ErrorDetails -and $_.ErrorDetails.Message) {
            $responseBody = $_.ErrorDetails.Message
        }
    }

    return [pscustomobject]@{
        StatusCode = $statusCode
        Body = $responseBody
        ErrorMessage = $errorMessage
    }
}

function Update-MobilePayloadForTestRun {
    param(
        [Parameter(Mandatory = $true)][object]$Payload,
        [Parameter(Mandatory = $true)][object]$TestCase,
        [Parameter(Mandatory = $true)][string]$DateTourneeText,
        [Parameter(Mandatory = $true)][datetime]$DateTourneeValue,
        [Parameter(Mandatory = $true)][string]$CodeTournee,
        [Parameter(Mandatory = $true)][string]$IdSynchronisation,
        [Parameter(Mandatory = $true)][string]$RunId
    )

    $jourNumero = Get-FrenchDayNumber -Date $DateTourneeValue
    $jourLibelle = Get-FrenchDayName -Date $DateTourneeValue

    Set-JsonProperty -Object $Payload -Name 'idSynchronisation' -Value $IdSynchronisation
    Set-JsonProperty -Object $Payload -Name 'dateTournee' -Value $DateTourneeText
    Set-JsonProperty -Object $Payload -Name 'codeTournee' -Value $CodeTourne
    Set-JsonProperty -Object $Payload -Name 'libelleTournee' -Value ('TEST API MOBILE ' + $TestCase.Id)
    Set-JsonProperty -Object $Payload -Name 'commentaireGlobal' -Value ('TEST API MOBILE AUTOMATISE ' + $RunId + ' ' + $TestCase.Id)

    if ($null -ne $Payload.mobile) {
        Set-JsonProperty -Object $Payload.mobile -Name 'nomAppareil' -Value ('Test PowerShell ' + $RunId)
        Set-JsonProperty -Object $Payload.mobile -Name 'versionApplication' -Value '1.0.0-test'
        Set-JsonProperty -Object $Payload.mobile -Name 'dateChargementMobile' -Value ($DateTourneeText + 'T07:30:00+02:00')
        Set-JsonProperty -Object $Payload.mobile -Name 'dateEnvoiMobile' -Value ($DateTourneeText + 'T16:45:00+02:00')
    }

    if ($null -ne $Payload.trajet) {
        $dateDepartMobile = Get-JsonPropertyText -Object $Payload.trajet -Name 'dateDepartMobile'
        if (-not [string]::IsNullOrWhiteSpace($dateDepartMobile)) {
            Set-JsonProperty -Object $Payload.trajet -Name 'dateDepartMobile' -Value ($DateTourneeText + 'T07:45:00+02:00')
        }

        $dateArriveeMobile = Get-JsonPropertyText -Object $Payload.trajet -Name 'dateArriveeMobile'
        if (-not [string]::IsNullOrWhiteSpace($dateArriveeMobile)) {
            Set-JsonProperty -Object $Payload.trajet -Name 'dateArriveeMobile' -Value ($DateTourneeText + 'T16:30:00+02:00')
        }
    }

    $lignes = @($Payload.lignes)

    for ($indexLigne = 0; $indexLigne -lt $lignes.Count; $indexLigne++) {
        $ligne = $lignes[$indexLigne]
        $numClient = 'CLIENT' + $indexLigne
        $codePDL = 'PDL' + $indexLigne

        if ($null -ne $ligne.client) {
            $clientValue = Get-JsonPropertyText -Object $ligne.client -Name 'numClient'
            if (-not [string]::IsNullOrWhiteSpace($clientValue)) {
                $numClient = $clientValue
            }
        }

        if ($null -ne $ligne.pointLivraison) {
            $pdlValue = Get-JsonPropertyText -Object $ligne.pointLivraison -Name 'codePDL'
            if (-not [string]::IsNullOrWhiteSpace($pdlValue)) {
                $codePDL = $pdlValue
            }
        }

        $lineIndexForSource = $indexLigne
        if ($TestCase.Mode -eq 'DuplicateLine') {
            $lineIndexForSource = 0
        }

        $idLigneSource = $DateTourneeText + '|' + $CodeTournee + '|' + $jourNumero + '|' + $numClient + '|' + $codePDL + '|' + $lineIndexForSource
        if ($TestCase.Mode -eq 'DuplicateLine') {
            $idLigneSource = $DateTourneeText + '|' + $CodeTournee + '|' + $jourNumero + '|DUPLICATE|PDT|1'
        }

        Set-JsonProperty -Object $ligne -Name 'idLigneSource' -Value $idLigneSource

        if ($null -ne $ligne.tournee) {
            Set-JsonProperty -Object $ligne.tournee -Name 'codeTournee' -Value $CodeTourne
            Set-JsonProperty -Object $ligne.tournee -Name 'libelleTournee' -Value ('TEST API MOBILE ' + $TestCase.Id)
            Set-JsonProperty -Object $ligne.tournee -Name 'jourTournee' -Value $jourNumero
            Set-JsonProperty -Object $ligne.tournee -Name 'jourLibelle' -Value $jourLibelle
        }

        if ($null -ne $ligne.retour) {
            Set-JsonProperty -Object $ligne.retour -Name 'jourTourneeRetour' -Value $jourNumero
            Set-JsonProperty -Object $ligne.retour -Name 'jourRetourLibelle' -Value $jourLibelle
            Set-JsonProperty -Object $ligne.retour -Name 'codeTourneeRetour' -Value $CodeTournee
            Set-JsonProperty -Object $ligne.retour -Name 'libelleTourneeRetour' -Value ('TEST API MOBILE ' + $TestCase.Id)
        }

        if ($null -ne $ligne.saisie) {
            $heureValidation = Get-JsonPropertyText -Object $ligne.saisie -Name 'heureValidation'
            if (-not [string]::IsNullOrWhiteSpace($heureValidation)) {
                Set-JsonProperty -Object $ligne.saisie -Name 'heureValidation' -Value ($DateTourneeText + 'T09:12:00+02:00')
            }
        }
    }

    return $Payload
}

function New-TestCase {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][int]$Order,
        [Parameter(Mandatory = $true)][string]$Scenario,
        [Parameter(Mandatory = $false)][string]$FileName = '',
        [Parameter(Mandatory = $true)][int]$ExpectedHttp,
        [Parameter(Mandatory = $false)][string]$ExpectedStatut = '',
        [Parameter(Mandatory = $false)][string]$ExpectedCode = '',
        [Parameter(Mandatory = $false)][string]$Mode = 'Normal',
        [Parameter(Mandatory = $false)][string]$CodeSuffix = '',
        [Parameter(Mandatory = $false)][string]$Method = 'POST',
        [Parameter(Mandatory = $false)][string]$RelativeUrl = '',
        [Parameter(Mandatory = $false)][switch]$RequiresJson,
        [Parameter(Mandatory = $false)][switch]$requiresSchemaVersion,
        [Parameter(Mandatory = $false)][switch]$RequiresCamionsArray
    )

    return [pscustomobject]@{
        Id = $Id
        Order = $Order
        Scenario = $Scenario
        FileName = $FileName
        ExpectedHttp = $ExpectedHttp
        ExpectedStatut = $ExpectedStatut
        ExpectedCode = $ExpectedCode
        Mode = $Mode
        CodeSuffix = $CodeSuffix
        Method = $Method
        RelativeUrl = $RelativeUrl
        RequiresJson = [bool]$requiresJson
        RequiresSchemaVersion = [bool]$RequiresSchemaVersion
        RequiresCamionsArray = [bool]$RequiresCamionsArray
    }
}

$scriptDirectory = Get-ScriptDirectory
$mobileTestsDirectory = Split-Path -Parent $scriptDirectory

if ([string]::IsNullOrWhiteSpace($PayloadsRoot)) {
    $PayloadsRoot = Join-Path $mobileTestsDirectory 'payloads'
}

if ([string]::IsNullOrWhiteSpace($ReportsRoot)) {
    $ReportsRoot = Join-Path $mobileTestsDirectory 'rapports'
}

if (-not (Test-Path $PayloadsRoot)) {
    throw ('Dossier payloads introuvable : ' + $PayloadsRoot)
}

New-Item -ItemType Directory -Force -Path $ReportsRoot | Out-Null

$sentPayloadsDirectory = Join-Path $ReportsRoot 'payloads-envoyes'
$responsesDirectory = Join-Path $ReportsRoot 'responses'
New-Item -ItemType Directory -Force -Path $sentPayloadsDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $responsesDirectory | Out-Null

$apiUrl = Join-Url -BaseUrl $ApiBaseUrl -RelativeUrl $Endpoint

if ([string]::IsNullOrWhiteSpace($DateTournee)) {
    $dateTourneeValue = Get-Date
}
else {
    $dateTourneeValue = [datetime]::Parse($DateTournee)
}

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

    # Tests preparatoires couvrant la future version 1.3 trajet/camion.
    # Ils peuvent rester KO tant que l'API 1.3 et la route GET /api/camions/disponibles ne sont pas encore codees.
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
    if (-not [string]::IsNullOrWhiteSpace($testCase.RelativeUrl)) {
        $targetUrl = Join-Url -BaseUrl $ApiBaseUrl -RelativeUrl $testCase.RelativeUrl
    }

    $sentPayloadFile = ''
    $safeId = $testCase.Id.Replace('/', '-').Replace('\', '-')
    $safeFileName = ([string]$testCase.FileName).Replace('/', '-').Replace('\', '-')
    if ([string]::IsNullOrWhiteSpace($safeFileName)) {
        $safeFileName = 'request'
    }

    $responseFile = Join-Path $responsesDirectory ($safeId + '-response.json')
    $jsonBody = ''

    if ([string]::Equals($testCase.Method, 'POST', [System.StringComparison]::OrdinalIgnoreCase)) {
        $payloadPath = Find-PayloadFile -Root $PayloadsRoot -FileName $testCase.FileName

        if ([string]::IsNullOrWhiteSpace($payloadPath)) {
            $result = [pscustomobject]@ {
                Id = $testCase.Id
                Scenario = $testCase.Scenario
                FileName = $testCase.FileName
                ExpectedHttp = $testCase.ExpectedHttp
                ActualHttp = 0
                ExpectedStatut = $testCase.ExpectedStatut
                ActualStatut = ''
                ExpectedCode = $testCase.ExpectedCode
                ActualCode = ''
                Result = 'SKIPPED'
                Message = 'Payload introuvable'
                SentPayloadFile = ''
                ResponseFile = ''
            }
            $results += $result
            Write-Host ('SKIP ' + $testCase.Id + ' - payload introuvable : ' + $testCase.FileName) -ForegroundColor Yellow
            continue
        }

        $payload = Read-JsonFile -Path $payloadPath
        $codeTournee = $runCodePrefix + $testCase.CodeSuffix
        $idSynchronisation = [guid]::NewGuid().ToString()

        if ($testCase.Mode -eq 'BaseSuccess') {
            $codeTournee = $baseSuccessCodeTournee
            $idSynchronisation = $baseSuccessIdSynchronisation
        }

        if ($testCase.Mode -eq 'TechnicalDuplicate') {
            $codeTournee = $baseSuccessCodeTournee
            $idSynchronisation = $baseSuccessIdSynchronisation
        }

        if ($testCase.Mode -eq 'BusinessDuplicate') {
            $codeTournee = $baseSuccessCodeTournee
            $idSynchronisation = [guid]::NewGuid().ToString()
        }

        $payload = Update-MobilePayloadForTestRun -Payload $payload -TestCase $testCase -DateTourneeText $dateTourneeText -DateTourneeValue $dateTourneeValue -CodeTournee $codeTournee -IdSynchronisation $idSynchronisation -RunId $runId

        $sentPayloadFile = Join-Path $sentPayloadsDirectory ($safeId + '-' + $safeFileName)
        Write-JsonFile -Value $payload -Path $sentPayloadFile
        $jsonBody = Get-Content -Path $sentPayloadFile -Raw -Encoding UTF8
    }

    Write-Host ('RUN  ' + $testCase.Id + ' - ' + $testCase.Scenario)
    $response = Invoke-ApiRequest -Url $targetUrl -Method $testCase.Method -JsonBody $jsonBody
    Set-Content -Path $responseFile -Value $response.Body -Encoding UTF8

    $responseJson = Convert-BodyToJsonObject -Body $response.Body
    $actualStatut = Get-JsonPropertyText -Object $responseJson -Name 'statut'
    $actualCode = Get-JsonPropertyText -Object $responseJson -Name 'code'

    $httpOk = $response.StatusCode -eq $testCase.ExpectedHttp
    $statutOk = $true
    $codeOk = $true
    $jsonOk = $true
    $schemaVersionOk = $true
    $camionsArrayOk = $true

    if (-not [string]::IsNullOrWhiteSpace($testCase.ExpectedStatut)) {
        $statutOk = [string]::Equals($actualStatut, $testCase.ExpectedStatut, [System.StringComparison]::OrdinalIgnoreCase)
    }

    if (-not [string]::IsNullOrWhiteSpace($testCase.ExpectedCode)) {
        $codeOk = [string]::Equals($actualCode, $testCase.ExpectedCode, [System.StringComparison]::OrdinalIgnoreCase)
    }

    if ($testCase.RequiresJson) {
        $jsonOk = $null -ne $responseJson
    }

    if ($testCase.RequiresSchemaVersion) {
        $schemaVersion = Get-JsonPropertyText -Object $responseJson -Name 'schemaVersion'
        $schemaVersionOk = -not [string]::IsNullOrWhiteSpace($schemaVersion)
    }

    if ($testCase.RequiresCamionsArray) {
        $camionsArrayOk = Test-JsonArrayProperty -Object $responseJson -Name 'camions'
    }

    $resultStatus = 'OK'
    if (-not ($httpOk -and $statutOk -and $codeOk -and $jsonOk -and $schemaVersionOk -and $camionsArrayOk)) {
        $resultStatus = 'KO'
    }

    $message = ''
    if ($resultStatus -eq 'KO') {
        $message = 'Attendu HTTP=' + $testCase.ExpectedHttp + ', statut=' + $testCase.ExpectedStatut + ', code=' + $testCase.ExpectedCode + '. Obtenu HTTP=' + $response.StatusCode + ', statut=' + $actualStatut + ', code=' + $actualCode + '.'

        if ($testCase.RequiresJson -and (-not $jsonOk)) {
            $message = $message + ' JSON non parseable.'
        }

        if ($testCase.RequiresSchemaVersion -and (-not $schemaVersionOk)) {
            $message = $message + ' schemaVersion absent.'
        }

        if ($testCase.RequiresCamionsArray -and (-not $camionsArrayOk)) {
            $message = $message + ' camions absent ou non tableau.'
        }

        if (-not [string]::IsNullOrWhiteSpace($response.ErrorMessage)) {
            $message = $message + ' Erreur=' + $response.ErrorMessage
        }
    }

    $result = [pscustomobject]@ {
        Id = $testCase.Id
        Scenario = $testCase.Scenario
        FileName = $testCase.FileName
        ExpectedHttp = $testCase.ExpectedHttp
        ActualHttp = $response.StatusCode
        ExpectedStatut = $testCase.ExpectedStatut
        ActualStatut = $actualStatut
        ExpectedCode = $testCase.ExpectedCode
        ActualCode = $actualCode
        Result = $resultStatus
        Message = $message
        SentPayloadFile = $sentPayloadFile
        ResponseFile = $responseFile
    }
    $results += $result

    if ($resultStatus -eq 'OK') {
        Write-Host ('OK   ' + $testCase.Id + ' HTTP ' + $response.StatusCode) -ForegroundColor Green
    }
    else {
        Write-Host ('KO   ' + $testCase.Id + ' HTTP ' + $response.StatusCode) -ForegroundColor Red
        Write-Host $message -ForegroundColor Red

        if ($StopOnFirstFailure) {
            break
        }
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

    if ([string]::IsNullOrWhiteSpace($expectedCode)) {
        $expectedCode = '-'
    }

    if ([string]::IsNullOrWhiteSpace($actualCode)) {
        $actualCode = '-'
    }

    $line = '| ' + $result.Id + ' | ' + $scenario + ' | ' + $result.ExpectedHttp + ' | ' + $result.ActualHttp + ' | ' + $result.ExpectedStatut + ' | ' + $result.ActualStatut + ' | ' + $expectedCode + ' | ' + $actualCode + ' | ' + $result.Result + ' |'
    $reportLines += $line
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

        if (-not [string]::IsNullOrWhiteSpace($failed.SentPayloadFile)) {
            $reportLines += ('Payload envoye : ' + $failed.SentPayloadFile)
            $reportLines += ''
        }

        $reportLines += ('Reponse API : ' + $failed.ResponseFile)
        $reportLines += ''
    }
}

$reportLines | Set-Content -Path $mdPath -Encoding UTF8

Write-Host ''
Write-Host '=== Fin des tests API Mobile ==='
Write-Host ('Total   : ' + $totalCount)
Write-Host ('OK       : ' + $okCount) -ForegroundColor Green
Write-Host ('KO      : ' + $koCount) -ForegroundColor Red
Write-Host ('SKIPPED : ' + $skippedCount) -ForegroundColor Yellow
Write-Host ''
Write-Host ('Rapport : ' + $mdPath)
Write-Host ('CSV     : ' + $csvPath)
Write-Host ('JSON    : ' + $jsonPath)
Write-Host ''

if ($koCount -gt 0) {
    exit 1
}

exit 0
