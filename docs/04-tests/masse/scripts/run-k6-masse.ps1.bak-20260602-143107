[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl,

    [ValidateRange(1, 500)]
    [int]$Count = 20,

    [ValidateRange(1, 100)]
    [int]$Vus = 5,

    [string]$DateTournee = "",

    [string]$RunId = "",

    [int]$LineCount = 5,

    [int]$ThinkTimeMilliseconds = 50,

    [string]$MaxDuration = "3m"
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$K6Script = Join-Path $Root "k6\post-synchronisations-masse.js"
$ResultDir = Join-Path $Root "resultats"
$ReportDir = Join-Path $Root "rapports"

New-Item -ItemType Directory -Force $ResultDir | Out-Null
New-Item -ItemType Directory -Force $ReportDir | Out-Null

$K6Command = Get-Command "k6" -ErrorAction SilentlyContinue
if (-not $K6Command) {
    throw "k6 est introuvable. Installez-le avec : winget install k6.k6"
}

function Get-ParisDate {
    try {
        $tz = [TimeZoneInfo]::FindSystemTimeZoneById("Romance Standard Time")
    }
    catch {
        $tz = [TimeZoneInfo]::FindSystemTimeZoneById("Europe/Paris")
    }

    return [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, $tz).ToString("yyyy-MM-dd")
}

if ([string]::IsNullOrWhiteSpace($DateTournee)) {
    $DateTournee = Get-ParisDate
}

if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = Get-Date -Format "yyyyMMddHHmmss"
}

$runSuffix = $RunId.Substring([Math]::Max(0, $RunId.Length - 6))
$CodeTourneePrefix = "K6$runSuffix"
$ExpectedLignes = $Count * $LineCount
$ArticlesPerLine = 4
$ExpectedQuantites = $ExpectedLignes * $ArticlesPerLine
$ThinkTimeSeconds = [Math]::Max(0, $ThinkTimeMilliseconds) / 1000

$metadata = [ordered]@{
    runId = $RunId
    apiBaseUrl = $ApiBaseUrl
    dateTournee = $DateTournee
    codeTourneePrefix = $CodeTourneePrefix
    expectedTournees = $Count
    lineCount = $LineCount
    articlesPerLine = $ArticlesPerLine
    expectedLignes = $ExpectedLignes
    expectedQuantites = $ExpectedQuantites
    vus = $Vus
    maxDuration = $MaxDuration
    generatedAt = (Get-Date).ToString("s")
}

$metadataPath = Join-Path $ResultDir "metadata-$RunId.json"
$metadataLatestPath = Join-Path $ResultDir "metadata-latest.json"
$summaryPath = Join-Path $ResultDir "k6-summary-$RunId.json"
$consoleLogPath = Join-Path $ResultDir "k6-console-$RunId.log"

$metadata | ConvertTo-Json -Depth 8 | Set-Content -Path $metadataPath -Encoding UTF8
$metadata | ConvertTo-Json -Depth 8 | Set-Content -Path $metadataLatestPath -Encoding UTF8

Write-Host "=== Test k6 de masse MobileSLI ==="
Write-Host "API                : $ApiBaseUrl"
Write-Host "RunId              : $RunId"
Write-Host "DateTournee        : $DateTournee"
Write-Host "CodeTourneePrefix  : $CodeTourneePrefix"
Write-Host "Synchronisations   : $Count"
Write-Host "VUs                : $Vus"
Write-Host "Lignes attendues   : $ExpectedLignes"
Write-Host "Quantités attendues: $ExpectedQuantites"
Write-Host ""

$env:API_BASE_URL = $ApiBaseUrl
$env:DATE_TOURNEE = $DateTournee
$env:RUN_ID = $RunId
$env:CODE_TOURNEE_PREFIX = $CodeTourneePrefix
$env:SYNC_COUNT = [string]$Count
$env:VUS = [string]$Vus
$env:LINE_COUNT = [string]$LineCount
$env:THINK_TIME_SECONDS = $ThinkTimeSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture)
$env:MAX_DURATION = $MaxDuration

& $K6Command.Source run --summary-export $summaryPath $K6Script 2>&1 | Tee-Object -FilePath $consoleLogPath
$exitCode = $LASTEXITCODE

& (Join-Path $PSScriptRoot "build-rapport-k6.ps1") `
    -SummaryJson $summaryPath `
    -MetadataJson $metadataPath `
    -ConsoleLog $consoleLogPath

Write-Host ""
Write-Host "=== Fichiers générés ==="
Write-Host "Métadonnées : $metadataPath"
Write-Host "Résumé k6   : $summaryPath"
Write-Host "Console k6  : $consoleLogPath"
Write-Host "Rapports    : $ReportDir"

if ($exitCode -ne 0) {
    throw "k6 a terminé avec le code $exitCode. Consulter $consoleLogPath et le rapport généré."
}
