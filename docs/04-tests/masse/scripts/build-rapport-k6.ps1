[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SummaryJson,

    [Parameter(Mandatory = $true)]
    [string]$MetadataJson,

    [string]$ConsoleLog = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SummaryJson)) {
    throw "Résumé k6 introuvable : $SummaryJson"
}

if (-not (Test-Path $MetadataJson)) {
    throw "Métadonnées introuvables : $MetadataJson"
}

$Root = Split-Path -Parent $PSScriptRoot
$ReportDir = Join-Path $Root "rapports"
New-Item -ItemType Directory -Force $ReportDir | Out-Null

$summary = Get-Content $SummaryJson -Raw | ConvertFrom-Json
$metadata = Get-Content $MetadataJson -Raw | ConvertFrom-Json

function Get-MetricValue {
    param(
        [object]$Summary,
        [string]$MetricName,
        [string]$ValueName
    )

    $metric = $Summary.metrics.$MetricName
    if ($null -eq $metric -or $null -eq $metric.values) {
        return $null
    }

    return $metric.values.$ValueName
}

function Format-Number {
    param(
        [object]$Value,
        [int]$Decimals = 2
    )

    if ($null -eq $Value) {
        return "n/a"
    }

    try {
        return ([double]$Value).ToString("N$Decimals", [System.Globalization.CultureInfo]::GetCultureInfo("fr-FR"))
    }
    catch {
        return "n/a"
    }
}

$httpReqs = Get-MetricValue $summary "http_reqs" "count"
$durationAvg = Get-MetricValue $summary "http_req_duration" "avg"
$durationP95 = Get-MetricValue $summary "http_req_duration" "p(95)"
$checksRate = Get-MetricValue $summary "checks" "rate"
$failedRate = Get-MetricValue $summary "http_req_failed" "rate"
$successes = Get-MetricValue $summary "mobile_sync_success_200" "count"
$validation400 = Get-MetricValue $summary "mobile_sync_validation_400" "count"
$conflict409 = Get-MetricValue $summary "mobile_sync_conflict_409" "count"
$server500 = Get-MetricValue $summary "mobile_sync_server_error_500" "count"
$unexpected = Get-MetricValue $summary "mobile_sync_unexpected" "count"

if ($null -eq $successes) { $successes = 0 }
if ($null -eq $validation400) { $validation400 = 0 }
if ($null -eq $conflict409) { $conflict409 = 0 }
if ($null -eq $server500) { $server500 = 0 }
if ($null -eq $unexpected) { $unexpected = 0 }

$expected = [int]$metadata.expectedTournees
$success = ([int]$successes -eq $expected -and [int]$server500 -eq 0 -and [int]$unexpected -eq 0)
$conclusion = if ($success) {
    "Test de masse réussi côté API. Toutes les synchronisations attendues sont acceptées. La vérification SQL doit confirmer les volumes sauvegardés."
} else {
    "Test de masse à analyser. Le nombre de succès ou d'erreurs ne correspond pas au résultat attendu. Vérifier le journal k6 et les logs API."
}

$reportPath = Join-Path $ReportDir "rapport-k6-masse-$($metadata.runId)-powershell.md"

$report = @"
# Rapport k6 - tests de masse API Mobile SLI

## Campagne

| Élément | Valeur |
|---|---|
| RunId | `$($metadata.runId)` |
| API | `$($metadata.apiBaseUrl)` |
| Date tournée | `$($metadata.dateTournee)` |
| Préfixe tournées | `$($metadata.codeTourneePrefix)` |
| Synchronisations attendues | $($metadata.expectedTournees) |
| Lignes attendues | $($metadata.expectedLignes) |
| Quantités attendues | $($metadata.expectedQuantites) |
| VUs k6 | $($metadata.vus) |
| Généré le | $($metadata.generatedAt) |

## Résultats HTTP

| Indicateur | Valeur |
|---|---:|
| Requêtes HTTP | $(Format-Number $httpReqs 0) |
| Succès HTTP 200 | $(Format-Number $successes 0) |
| Erreurs validation HTTP 400 | $(Format-Number $validation400 0) |
| Conflits HTTP 409 | $(Format-Number $conflict409 0) |
| Erreurs serveur HTTP 500 | $(Format-Number $server500 0) |
| Réponses inattendues | $(Format-Number $unexpected 0) |
| Taux de requêtes échouées k6 | $(Format-Number ([double]$failedRate * 100) 2) % |
| Taux de checks OK | $(Format-Number ([double]$checksRate * 100) 2) % |

## Temps de réponse

| Indicateur | Valeur |
|---|---:|
| Temps moyen | $(Format-Number $durationAvg 2) ms |
| p95 | $(Format-Number $durationP95 2) ms |

## Conclusion

$conclusion

## Fichiers associés

| Fichier | Rôle |
|---|---|
| `$SummaryJson` | Résumé JSON k6 |
| `$MetadataJson` | Métadonnées de campagne |
| `$ConsoleLog` | Journal console k6 |

## Vérification SQL à effectuer

Lancer ensuite :

```powershell
.\scripts\run-verification-sql-masse.ps1 -ServerInstance "NOM_SERVEUR_SQL" -Database "NOM_BASE_SQL"
```
"@

$report | Set-Content -Path $reportPath -Encoding UTF8
Write-Host "Rapport PowerShell généré : $reportPath"
