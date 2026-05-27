param(
    [Parameter(Mandatory = $false)]
    [string]$ApiBaseUrl = 'http://localhost:5120',

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
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,

        [Parameter(Mandatory = $true)]
        [string]$RelativeUrl
    )

    $cleanBase = $BaseUrl.TrimEnd('/')
    $cleanRelative = $RelativeUrl.TrimStart('/')
    return $cleanBase + '/' + $cleanRelative
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
    param([Parameter(Mandatory = $true)][datetime]$Date)

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
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $false)]
        [object]$Value
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
        [Parameter(Mandatory = $false)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name
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

function Find-PayloadFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    $directPath = Join-Path $Root $FileName

    if (Test-Path $directPath) {
        return $directPath
    }

    $match = Get-ChildItem -Path $Root -Recurse -File -Filter $FileName -ErrorAction SilentlyContinue | Select-Object -First 1

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
        [Parameter(Mandatory = $true)]
        [object]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $json = $Value | ConvertTo-Json -Depth 100
    Set-Content -Path $Path -Value $json -Encoding UTF8
}

function Convert-BodyToJsonObject {
    param([Parameter(Mandatory = $false)][string]$Body)

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

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,

        [Parameter(Mandatory = $true)]
        [string]$JsonBody
    )

    $statusCode = 0
    $responseBody = ''
    $errorMessage = ''

    try {
        $response = Invoke-WebRequest -Uri $Url -Method Post -ContentType 'application/json; charset=utf-8' -Body $JsonBody -UseBasicParsing
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
        [Parameter(Mandatory = $true)]
        [object]$Payload,

        [Parameter(Mandatory = $true)]
        [object]$TestCase,

        [Parameter(Mandatory = $true)]
        [string]$DateTourneeText,

        [Parameter(Mandatory = $true)]
        [datetime]$DateTourneeValue,

        [Parameter(Mandatory = $true)]
        [string]$CodeTournee,

        [Parameter(Mandatory = $true)]
        [string]$IdSynchronisation,

        [Parameter(Mandatory = $true)]
        [string]$RunId
    )

    $jourNumero = Get-FrenchDayNumber -Date $DateTourneeValue
    $jourLibelle = Get-FrenchDayName -Date $DateTourneeValue

    Set-JsonProperty -Object $Payload -Name 'idSynchronisation' -Value $IdSynchronisation
    Set-JsonProperty -Object $Payload -Name 'dateTournee' -Value $DateTourneeText
    Set-JsonProperty -Object $Payload -Name 'codeTournee' -Value $CodeTournee
    Set-JsonProperty -Object $Payload -Name 'libelleTournee' -Value ('TEST API MOBILE ' + $TestCase.Id)
    Set-JsonProperty -Object $Payload -Name 'commentaireGlobal' -Value ('TEST API MOBILE AUTOMATISE ' + $RunId + ' ' + $TestCase.Id)

    if ($null -ne $Payload.mobile) {
        Set-JsonProperty -Object $Payload.mobile -Name 'nomAppareil' -Value ('Test PowerShell ' + $RunId)
        Set-JsonProperty -Object $Payload.mobile -Name 'versionApplication' -Value '1.0.0-test'
        Set-JsonProperty -Object $Payload.mobile -Name 'dateChargementMobile' -Value ($DateTourneeText + 'T07:30:00+02:00')
        Set-JsonProperty -Object $Payload.mobile -Name 'dateEnvoiMobile' -Value ($DateTourneeText + 'T16:45:00+02:00')
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
            $idLigneSource = $DateTourneeText + '|' + $CodeTournee + '|' + $jourNumero + '|DUPLICATE|PDL|1'
        }

        Set-JsonProperty -Object $ligne -Name 'idLigneSource' -Value $idLigneSource

        if ($null -ne $ligne.tournee) {
            Set-JsonProperty -Object $ligne.tournee -Name 'codeTournee' -Value $CodeTournee
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
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][int]$ExpectedHttp,
        [Parameter(Mandatory = $true)][string]$ExpectedStatut,
        [Parameter(Mandatory = $false)][string]$ExpectedCode = '',
        [Parameter(Mandatory = $false)][string]$Mode = 'Normal',
        [Parameter(Mandatory = $false)][string]$CodeSuffix = ''
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
    (New-TestCase -Id 'MOB-API-018' -Order 18 -Scenario 'ROLLS_VIDES avec quantite prevue positive accepte' -FileName 'sync-rolls-vides-prevue-invalide.json' -ExpectedHttp 200 -ExpectedStatut 'SUCCESS' -CodeSuffix '18')
)

Write-Host ''
Write-Host '=== Tests API Mobile SLI v1.2 ==='
Write-Host ('API          : ' + $apiUrl)
Write-Host ('Payloads     : ' + $PayloadsRoot)
Write-Host ('Rapports     : ' + $ReportsRoot)
Write-Host ('Date tournee : ' + $dateTourneeText)
Write-Host ('RunId        : ' + $runId)
Write-Host ''

$results = @()

foreach ($testCase in ($testCases | Sort-Object Order)) {
    $payloadPath = Find-PayloadFile -Root $PayloadsRoot -FileName $testCase.FileName

    if ([string]::IsNullOrWhiteSpace($payloadPath)) {
        $result = [pscustomobject]@{
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

    $safeId = $testCase.Id.Replace('/', '-').Replace('\', '-')
    $sentPayloadFile = Join-Path $sentPayloadsDirectory ($safeId + '-' + $testCase.FileName)
    $responseFile = Join-Path $responsesDirectory ($safeId + '-response.json')

    Write-JsonFile -Value $payload -Path $sentPayloadFile
    $jsonBody = Get-Content -Path $sentPayloadFile -Raw -Encoding UTF8

    Write-Host ('RUN  ' + $testCase.Id + ' - ' + $testCase.Scenario)
    $response = Invoke-JsonPost -Url $apiUrl -JsonBody $jsonBody
    Set-Content -Path $responseFile -Value $response.Body -Encoding UTF8

    $responseJson = Convert-BodyToJsonObject -Body $response.Body
    $actualStatut = Get-JsonPropertyText -Object $responseJson -Name 'statut'
    $actualCode = Get-JsonPropertyText -Object $responseJson -Name 'code'

    $httpOk = $response.StatusCode -eq $testCase.ExpectedHttp
    $statutOk = $true
    $codeOk = $true

    if (-not [string]::IsNullOrWhiteSpace($testCase.ExpectedStatut)) {
        $statutOk = [string]::Equals($actualStatut, $testCase.ExpectedStatut, [System.StringComparison]::OrdinalIgnoreCase)
    }

    if (-not [string]::IsNullOrWhiteSpace($testCase.ExpectedCode)) {
        $codeOk = [string]::Equals($actualCode, $testCase.ExpectedCode, [System.StringComparison]::OrdinalIgnoreCase)
    }

    $resultStatus = 'OK'
    if (-not ($httpOk -and $statutOk -and $codeOk)) {
        $resultStatus = 'KO'
    }

    $message = ''
    if ($resultStatus -eq 'KO') {
        $message = 'Attendu HTTP=' + $testCase.ExpectedHttp + ', statut=' + $testCase.ExpectedStatut + ', code=' + $testCase.ExpectedCode + '. Obtenu HTTP=' + $response.StatusCode + ', statut=' + $actualStatut + ', code=' + $actualCode + '.'
        if (-not [string]::IsNullOrWhiteSpace($response.ErrorMessage)) {
            $message = $message + ' Erreur=' + $response.ErrorMessage
        }
    }

    $result = [pscustomobject]@{
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
$reportLines += '# Rapport tests API Mobile SLI v1.2'
$reportLines += ''
$reportLines += ('Date execution : ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$reportLines += ''
$reportLines += ('API testee : ' + $apiUrl)
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
        $reportLines += ('Payload envoye : ' + $failed.SentPayloadFile)
        $reportLines += ''
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

if ($koCount -gt 0) {
    exit 1
}

exit 0
