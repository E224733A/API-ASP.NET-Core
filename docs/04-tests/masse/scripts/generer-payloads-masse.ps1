[CmdletBinding()]
param(
    [int]$Count = 20,
    [string]$DateTournee = "",
    [string]$RunId = "",
    [int]$LineCount = 5
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$OutputRoot = Join-Path $Root "payloads\generated"

function Get-ParisDate {
    try {
        $tz = [TimeZoneInfo]::FindSystemTimeZoneById("Romance Standard Time")
    }
    catch {
        $tz = [TimeZoneInfo]::FindSystemTimeZoneById("Europe/Paris")
    }

    return [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, $tz).ToString("yyyy-MM-dd")
}

function New-TestGuid {
    param([string]$RunId, [int]$Index)

    $digits = ($RunId -replace "\D", "")
    if ([string]::IsNullOrWhiteSpace($digits)) {
        $digits = "10000000"
    }

    if ($digits.Length -gt 8) {
        $digits = $digits.Substring($digits.Length - 8)
    }

    $digits = $digits.PadLeft(8, "0")
    $tailNumber = ([int64]$digits.Substring([Math]::Max(0, $digits.Length - 6))) * 1000 + $Index + 1
    $tail = $tailNumber.ToString("x").PadLeft(12, "0")
    if ($tail.Length -gt 12) {
        $tail = $tail.Substring($tail.Length - 12)
    }

    return "$digits-0000-4000-8000-$tail"
}

function Get-JourIso {
    param([string]$DateTournee)
    $date = [datetime]::ParseExact($DateTournee, "yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture)
    $day = [int]$date.DayOfWeek
    if ($day -eq 0) { return 7 }
    return $day
}

function Get-JourLibelle {
    param([int]$Jour)
    switch ($Jour) {
        1 { "Lundi" }
        2 { "Mardi" }
        3 { "Mercredi" }
        4 { "Jeudi" }
        5 { "Vendredi" }
        6 { "Samedi" }
        7 { "Dimanche" }
        default { "Jour inconnu" }
    }
}

if ([string]::IsNullOrWhiteSpace($DateTournee)) {
    $DateTournee = Get-ParisDate
}

if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = Get-Date -Format "yyyyMMddHHmmss"
}

$runSuffix = $RunId.Substring([Math]::Max(0, $RunId.Length - 6))
$codeTourneePrefix = "K6$runSuffix"
$jourTournee = Get-JourIso -DateTournee $DateTournee
$jourLibelle = Get-JourLibelle -Jour $jourTournee
$outputDir = Join-Path $OutputRoot $RunId
New-Item -ItemType Directory -Force $outputDir | Out-Null

for ($i = 0; $i -lt $Count; $i++) {
    $sequence = $i + 1
    $codeTournee = "$codeTourneePrefix$($sequence.ToString("000"))"
    $lignes = @()

    for ($lineIndex = 0; $lineIndex -lt $LineCount; $lineIndex++) {
        $ordre = $lineIndex + 1
        $clientNumber = [string](7000 + $sequence * 10 + $lineIndex)
        $pdlCode = "PDL-K6-$($sequence.ToString("000"))-$($ordre.ToString("00"))"

        $quantites = @(
            [ordered]@{
                codeArticle = "ROLLS"
                libelle = "Rolls"
                quantiteLivreePrevue = 1 + ($lineIndex % 3)
                quantiteLivree = 1 + ($lineIndex % 3)
                quantiteRecuperee = $lineIndex % 2
            },
            [ordered]@{
                codeArticle = "ROLLS_VIDES"
                libelle = "Chariots vides"
                quantiteLivreePrevue = $null
                quantiteLivree = $lineIndex % 2
                quantiteRecuperee = 1 + ($lineIndex % 4)
            },
            [ordered]@{
                codeArticle = "TAPIS"
                libelle = "Tapis"
                quantiteLivreePrevue = $lineIndex % 2
                quantiteLivree = $lineIndex % 2
                quantiteRecuperee = 0
            },
            [ordered]@{
                codeArticle = "SACS"
                libelle = "Sacs"
                quantiteLivreePrevue = 0
                quantiteLivree = if ($lineIndex % 3 -eq 0) { 1 } else { 0 }
                quantiteRecuperee = if ($lineIndex % 3 -eq 1) { 1 } else { 0 }
            }
        )

        $lignes += [ordered]@{
            idLigneSource = "$DateTournee|$codeTournee|$jourTournee|$clientNumber|$pdlCode|$ordre"
            ordreArret = $ordre
            horaire = [string]$ordre
            client = [ordered]@{
                numClient = $clientNumber
                nomClient = "CLIENT TEST MASSE $($sequence.ToString("000"))-$($ordre.ToString("00"))"
                nomAffiche = "CLIENT TEST MASSE $($sequence.ToString("000"))-$($ordre.ToString("00"))"
            }
            pointLivraison = [ordered]@{
                codePDL = $pdlCode
                descriptionPDL = "Point test masse $($sequence.ToString("000"))-$($ordre.ToString("00"))"
                adresseLigne1 = "$ordre RUE DU TEST DE MASSE"
                adresseLigne2 = $null
                adresseLigne3 = $null
                ville = "NANTES"
                codePostal = "44000"
            }
            tournee = [ordered]@{
                codeTournee = $codeTournee
                libelleTournee = "TEST MASSE $RunId"
                jourTournee = $jourTournee
                jourLibelle = $jourLibelle
                schemaLivraison = "1W1"
            }
            retour = [ordered]@{
                jourTourneeRetour = $jourTournee
                jourRetourLibelle = $jourLibelle
                codeTourneeRetour = $codeTournee
                libelleTourneeRetour = "TEST MASSE $RunId"
            }
            infosLivreur = [ordered]@{
                instructions = if ($lineIndex % 2 -eq 0) { "Instruction test masse" } else { $null }
                commentaireExceptionnel = $null
                zoneDechargement = if ($lineIndex % 2 -eq 0) { "EHPAD" } else { $null }
                zoneDechargementAffichee = if ($lineIndex % 2 -eq 0) { "EHPAD" } else { [string]$jourTournee }
                zone = $null
                precision = $null
                cle = $null
                estFerme = $false
                dateFermeture = $null
                motifFermeture = $null
            }
            saisie = [ordered]@{
                precisionLivreur = "Test k6 masse $RunId"
                statutPassage = "FAIT"
                commentaireLivreur = $null
                heureValidation = "$DateTournee`T09:$((10 + $lineIndex).ToString("00")):00+02:00"
                estValidee = $true
                quantites = $quantites
            }
        }
    }

    $payload = [ordered]@{
        schemaVersion = "1.2"
        idSynchronisation = New-TestGuid -RunId $RunId -Index $i
        dateTournee = $DateTournee
        codeTournee = $codeTournee
        libelleTournee = "TEST MASSE $RunId"
        livreur = [ordered]@{
            codeLivreur = "K6"
            nomLivreur = "LIVREUR TEST MASSE"
        }
        mobile = [ordered]@{
            nomAppareil = "k6-$RunId"
            versionApplication = "1.0.0-k6"
            dateChargementMobile = "$DateTournee`T07:30:00+02:00"
            dateEnvoiMobile = "$DateTournee`T16:45:00+02:00"
        }
        commentaireGlobal = "Test de masse k6 $RunId"
        lignes = $lignes
    }

    $file = Join-Path $outputDir "sync-masse-$($sequence.ToString("000")).json"
    $payload | ConvertTo-Json -Depth 20 | Set-Content -Path $file -Encoding UTF8
}

Write-Host "Payloads générés : $outputDir"
Write-Host "DateTournee       : $DateTournee"
Write-Host "RunId             : $RunId"
Write-Host "CodeTourneePrefix : $codeTourneePrefix"
