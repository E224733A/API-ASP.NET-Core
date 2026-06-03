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
    throw "Métadonnées k6 introuvables : $MetadataJson"
}

$Root = Split-Path -Parent $PSScriptRoot
$ReportDir = Join-Path $Root "rapports"
New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null

$summary = Get-Content -Path $SummaryJson -Raw -Encoding UTF8 | ConvertFrom-Json
$metadata = Get-Content -Path $MetadataJson -Raw -Encoding UTF8 | ConvertFrom-Json

function Get-MetricValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Summary,

        [Parameter(Mandatory = $true)]
        [string]$MetricName,

        [Parameter(Mandatory = $true)]
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

$httpReqs = Get-MetricValue -Summary $summary -MetricName "http_reqs" -ValueName "count"
$durationAvg = Get-MetricValue -Summary $summary -MetricName "http_req_duration" -ValueName "avg"
$durationP95 = Get-MetricValue -Summary $summary -MetricName "http_req_duration" -ValueName "p(95)"
$checksRate = Get-MetricValue -Summary $summary -MetricName "checks" -ValueName "rate"
$failedRate = Get-MetricValue -Summary $summary -MetricName "http_req_failed" -ValueName "rate"
$successes = Get-MetricValue -Summary $summary -MetricName "mobile_sync_success_200" -ValueName "count"
$validation400 = Get-MetricValue -Summary $summary -MetricName "mobile_sync_validation_400" -ValueName "count"
$conflict409 = Get-MetricValue -Summary $summary -MetricName "mobile_sync_conflict_409" -ValueName "count"
$server500 = Get-MetricValue -Summary $summary -MetricName "mobile_sync_server_error_500" -ValueName "count"
$unexpected = Get-MetricValue -Summary $summary -MetricName "mobile_sync_unexpected" -ValueName "count"

if ($null -eq $httpReqs) { $httpReqs = 0 }
if ($null -eq $successes) { $successes = 0 }
if ($null -eq $validation400) { $validation400 = 0 }
if ($null -eq $conflict409) { $conflict409 = 0 }
if ($null -eq $server500) { $server500 = 0 }
if ($null -eq $unexpected) { $unexpected = 0 }
if ($null -eq $checksRate) { $checksRate = 0 }
if ($null -eq $failedRate) { $failedRate = 0 }

$expected = [int]$metadata.expectedTournees
$success = ([int]$successes -eq $expected -and [int]$server500 -eq 0 -and [int]$unexpected -eq 0)

$conclusion = if ($success) {
    "Le test de masse est réussi côté API : toutes les synchronisations attendues sont acceptées, aucune erreur serveur n'a été détectée. La vérification SQL doit confirmer les volumes sauvegardés."
}
else {
    "Le test de masse est à analyser : le nombre de succès ou d'erreurs ne correspond pas au résultat attendu. Vérifier le journal k6 et les logs API."
}

$reportPath = Join-Path $ReportDir "rapport-k6-masse-$($metadata.runId)-powershell.md"

# Construction sans here-string et sans paramètre de type collection.
# Cela évite les erreurs de terminateur @" / "@ et l'erreur PowerShell
# "Impossible de lier l'argument au paramètre Lines, car il s'agit d'une collection vide".
$lines = New-Object 'System.Collections.Generic.List[string]'

$lines.Add("# Rapport k6 - tests de masse API Mobile SLI") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("## Campagne") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("| Élément | Valeur |") | Out-Null
$lines.Add("|---|---|") | Out-Null
$lines.Add("| RunId | ``$($metadata.runId)`` |") | Out-Null
$lines.Add("| API | ``$($metadata.apiBaseUrl)`` |") | Out-Null
$lines.Add("| Date tournée | ``$($metadata.dateTournee)`` |") | Out-Null
$lines.Add("| Préfixe tournées | ``$($metadata.codeTourneePrefix)`` |") | Out-Null
$lines.Add("| Synchronisations attendues | $($metadata.expectedTournees) |") | Out-Null
$lines.Add("| Lignes attendues | $($metadata.expectedLignes) |") | Out-Null
$lines.Add("| Quantités attendues | $($metadata.expectedQuantites) |") | Out-Null
$lines.Add("| VUs k6 | $($metadata.vus) |") | Out-Null
$lines.Add("| Seuil p95 | $($metadata.responseP95ThresholdMs) ms |") | Out-Null
$lines.Add("| Généré le | $($metadata.generatedAt) |") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("## Résultats HTTP") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("| Indicateur | Valeur |") | Out-Null
$lines.Add("|---|---:|") | Out-Null
$lines.Add("| Requêtes HTTP | $(Format-Number -Value $httpReqs -Decimals 0) |") | Out-Null
$lines.Add("| Succès HTTP 200 | $(Format-Number -Value $successes -Decimals 0) |") | Out-Null
$lines.Add("| Erreurs validation HTTP 400 | $(Format-Number -Value $validation400 -Decimals 0) |") | Out-Null
$lines.Add("| Conflits HTTP 409 | $(Format-Number -Value $conflict409 -Decimals 0) |") | Out-Null
$lines.Add("| Erreurs serveur HTTP 500 | $(Format-Number -Value $server500 -Decimals 0) |") | Out-Null
$lines.Add("| Réponses inattendues | $(Format-Number -Value $unexpected -Decimals 0) |") | Out-Null
$lines.Add("| Taux de requêtes échouées k6 | $(Format-Number -Value ([double]$failedRate * 100) -Decimals 2) % |") | Out-Null
$lines.Add("| Taux de checks OK | $(Format-Number -Value ([double]$checksRate * 100) -Decimals 2) % |") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("## Temps de réponse") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("| Indicateur | Valeur |") | Out-Null
$lines.Add("|---|---:|") | Out-Null
$lines.Add("| Temps moyen | $(Format-Number -Value $durationAvg -Decimals 2) ms |") | Out-Null
$lines.Add("| p95 | $(Format-Number -Value $durationP95 -Decimals 2) ms |") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("## Conclusion") | Out-Null
$lines.Add("") | Out-Null
$lines.Add($conclusion) | Out-Null
$lines.Add("") | Out-Null
$lines.Add("## Fichiers associés") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("| Fichier | Rôle |") | Out-Null
$lines.Add("|---|---|") | Out-Null
$lines.Add("| ``$SummaryJson`` | Résumé JSON k6 |") | Out-Null
$lines.Add("| ``$MetadataJson`` | Métadonnées de campagne |") | Out-Null
$lines.Add("| ``$ConsoleLog`` | Journal console k6 |") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("## Vérification SQL à effectuer") | Out-Null
$lines.Add("") | Out-Null
$lines.Add("Lancer ensuite :") | Out-Null
$lines.Add("") | Out-Null
$lines.Add('```powershell') | Out-Null
$lines.Add('.\scripts\run-verification-sql-masse.ps1 -ServerInstance "NOM_SERVEUR_SQL" -Database "NOM_BASE_SQL"') | Out-Null
$lines.Add('```') | Out-Null

$report = $lines -join [Environment]::NewLine
Set-Content -Path $reportPath -Value $report -Encoding UTF8

Write-Host "Rapport PowerShell généré : $reportPath"
