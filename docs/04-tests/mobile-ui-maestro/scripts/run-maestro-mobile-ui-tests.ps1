param(
    [ValidateSet("texte", "automationid")]
    [string]$FlowProfile = "texte",

    [string]$AppId = "fr.sli.mobiletournee",

    [switch]$SkipEnvironmentCheck
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$ReportsDir = Join-Path $Root "reports"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$RunDir = Join-Path $ReportsDir "run-$Timestamp-$FlowProfile"
New-Item -ItemType Directory -Force -Path $RunDir | Out-Null

$FlowDirName = if ($FlowProfile -eq "automationid") { "flows-stables-automationid" } else { "flows-texte" }
$FlowDir = Join-Path $Root $FlowDirName

if (-not (Test-Path $FlowDir)) {
    throw "Dossier de flows introuvable : $FlowDir"
}

Write-Host "=== Tests UI Maestro Mobile SLI ===" -ForegroundColor Cyan
Write-Host "Profil       : $FlowProfile"
Write-Host "Application  : $AppId"
Write-Host "Flows        : $FlowDir"
Write-Host "Rapports     : $RunDir"
Write-Host ""

if (-not $SkipEnvironmentCheck) {
    & (Join-Path $PSScriptRoot "check-maestro-environment.ps1") -AppId $AppId
    Write-Host ""
}

$FlowFiles = Get-ChildItem -Path $FlowDir -Filter "*.yaml" | Sort-Object Name

if ($FlowFiles.Count -eq 0) {
    throw "Aucun fichier .yaml trouvé dans $FlowDir"
}

$Results = @()

foreach ($FlowFile in $FlowFiles) {
    Write-Host "--- Exécution : $($FlowFile.Name) ---" -ForegroundColor Yellow

    $LogFile = Join-Path $RunDir ($FlowFile.BaseName + ".log")
    $Start = Get-Date

    $output = & maestro test $FlowFile.FullName 2>&1
    $exitCode = $LASTEXITCODE

    $End = Get-Date
    $DurationSeconds = [math]::Round(($End - $Start).TotalSeconds, 2)

    $output | Out-File -FilePath $LogFile -Encoding UTF8

    $status = if ($exitCode -eq 0) { "SUCCESS" } else { "FAILED" }

    if ($exitCode -eq 0) {
        Write-Host "OK : $($FlowFile.Name) en $DurationSeconds s" -ForegroundColor Green
    }
    else {
        Write-Host "ECHEC : $($FlowFile.Name) en $DurationSeconds s" -ForegroundColor Red
        Write-Host "Voir log : $LogFile"
    }

    $Results += [pscustomobject]@{
        Flow = $FlowFile.Name
        Status = $status
        ExitCode = $exitCode
        DurationSeconds = $DurationSeconds
        LogFile = $LogFile
    }
}

$SummaryFile = Join-Path $RunDir "rapport-maestro-mobile-ui.md"
$successCount = ($Results | Where-Object { $_.Status -eq "SUCCESS" }).Count
$failedCount = ($Results | Where-Object { $_.Status -ne "SUCCESS" }).Count

$summary = @()
$summary += "# Rapport Maestro Mobile UI"
$summary += ""
$summary += "Date : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$summary += "Profil : `$FlowProfile`"
$summary += "Application : `$AppId`"
$summary += ""
$summary += "## Synthèse"
$summary += ""
$summary += "| Total | Succès | Échecs |"
$summary += "|---:|---:|---:|"
$summary += "| $($Results.Count) | $successCount | $failedCount |"
$summary += ""
$summary += "## Détail"
$summary += ""
$summary += "| Flow | Statut | Durée secondes | Log |"
$summary += "|---|---|---:|---|"

foreach ($result in $Results) {
    $logName = Split-Path -Leaf $result.LogFile
    $summary += "| $($result.Flow) | $($result.Status) | $($result.DurationSeconds) | `$logName` |"
}

$summary += ""
$summary += "## Analyse à compléter"
$summary += ""
$summary += "```text"
$summary += "Indiquer si les échecs éventuels viennent :"
$summary += "- d'une précondition non respectée ;"
$summary += "- d'un problème d'environnement Android ;"
$summary += "- d'un texte d'interface modifié ;"
$summary += "- d'une vraie régression applicative."
$summary += "```"

$summary | Out-File -FilePath $SummaryFile -Encoding UTF8

Write-Host ""
Write-Host "Rapport généré : $SummaryFile" -ForegroundColor Cyan

if ($failedCount -gt 0) {
    exit 1
}
