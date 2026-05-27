[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerInstance,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [string]$MetadataJson = "",

    [string]$DateTournee = "",

    [string]$CodeTourneePrefix = "",

    [int]$ExpectedTournees = 0,

    [int]$ExpectedLignes = 0,

    [int]$ExpectedQuantites = 0,

    [switch]$TrustServerCertificate
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$SqlFile = Join-Path $Root "sql\verification-apres-k6.sql"
$ReportDir = Join-Path $Root "rapports"
$ResultDir = Join-Path $Root "resultats"
New-Item -ItemType Directory -Force $ReportDir | Out-Null

if ([string]::IsNullOrWhiteSpace($MetadataJson)) {
    $MetadataJson = Join-Path $ResultDir "metadata-latest.json"
}

if (Test-Path $MetadataJson) {
    $metadata = Get-Content $MetadataJson -Raw | ConvertFrom-Json

    if ([string]::IsNullOrWhiteSpace($DateTournee)) {
        $DateTournee = [string]$metadata.dateTournee
    }

    if ([string]::IsNullOrWhiteSpace($CodeTourneePrefix)) {
        $CodeTourneePrefix = [string]$metadata.codeTourneePrefix
    }

    if ($ExpectedTournees -le 0) {
        $ExpectedTournees = [int]$metadata.expectedTournees
    }

    if ($ExpectedLignes -le 0) {
        $ExpectedLignes = [int]$metadata.expectedLignes
    }

    if ($ExpectedQuantites -le 0) {
        $ExpectedQuantites = [int]$metadata.expectedQuantites
    }

    $RunId = [string]$metadata.runId
}
else {
    if ([string]::IsNullOrWhiteSpace($DateTournee) -or [string]::IsNullOrWhiteSpace($CodeTourneePrefix) -or $ExpectedTournees -le 0) {
        throw "Aucun fichier metadata trouvé. Fournir DateTournee, CodeTourneePrefix, ExpectedTournees, ExpectedLignes et ExpectedQuantites."
    }

    $RunId = Get-Date -Format "yyyyMMddHHmmss"
}

if ($ExpectedLignes -le 0) {
    $ExpectedLignes = $ExpectedTournees * 5
}

if ($ExpectedQuantites -le 0) {
    $ExpectedQuantites = $ExpectedLignes * 4
}

$reportPath = Join-Path $ReportDir "verification-sql-masse-$RunId.txt"

Write-Host "=== Vérification SQL masse ==="
Write-Host "ServerInstance     : $ServerInstance"
Write-Host "Database           : $Database"
Write-Host "DateTournee        : $DateTournee"
Write-Host "CodeTourneePrefix  : $CodeTourneePrefix"
Write-Host "ExpectedTournees   : $ExpectedTournees"
Write-Host "ExpectedLignes     : $ExpectedLignes"
Write-Host "ExpectedQuantites  : $ExpectedQuantites"
Write-Host "Rapport            : $reportPath"
Write-Host ""

$sqlcmd = Get-Command "sqlcmd" -ErrorAction SilentlyContinue

if ($sqlcmd) {
    $args = @(
        "-S", $ServerInstance,
        "-d", $Database,
        "-b",
        "-i", $SqlFile,
        "-v", "DateTournee=$DateTournee",
        "-v", "CodeTourneePrefix=$CodeTourneePrefix",
        "-v", "ExpectedTournees=$ExpectedTournees",
        "-v", "ExpectedLignes=$ExpectedLignes",
        "-v", "ExpectedQuantites=$ExpectedQuantites"
    )

    if ($TrustServerCertificate) {
        $args += "-C"
    }

    & $sqlcmd.Source @args 2>&1 | Tee-Object -FilePath $reportPath
    if ($LASTEXITCODE -ne 0) {
        throw "La vérification SQL a échoué. Consulter $reportPath"
    }
}
else {
    $invokeSqlcmd = Get-Command "Invoke-Sqlcmd" -ErrorAction SilentlyContinue
    if (-not $invokeSqlcmd) {
        throw "Ni sqlcmd ni Invoke-Sqlcmd ne sont disponibles. Installer sqlcmd ou le module SqlServer PowerShell."
    }

    $variables = @(
        "DateTournee=$DateTournee",
        "CodeTourneePrefix=$CodeTourneePrefix",
        "ExpectedTournees=$ExpectedTournees",
        "ExpectedLignes=$ExpectedLignes",
        "ExpectedQuantites=$ExpectedQuantites"
    )

    $params = @{
        ServerInstance = $ServerInstance
        Database = $Database
        InputFile = $SqlFile
        Variable = $variables
        ErrorAction = "Stop"
    }

    if ($TrustServerCertificate) {
        $params.TrustServerCertificate = $true
    }

    $result = Invoke-Sqlcmd @params
    $text = $result | Format-Table -AutoSize | Out-String -Width 220
    $text | Tee-Object -FilePath $reportPath
}

Write-Host "Vérification SQL terminée : $reportPath"
