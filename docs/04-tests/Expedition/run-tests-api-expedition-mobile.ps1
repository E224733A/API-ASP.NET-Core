param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5000",
    [string]$DateTournee = "2026-05-07",
    [string]$CodeTournee = "4006",
    [string]$CodeLivreur = "2",
    [int]$MaxLignes = 2
)

$ErrorActionPreference = "Stop"

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkCyan
    Write-Host $Title -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor DarkCyan
}

function ConvertFrom-JsonSafe {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $null
    }

    try {
        return $Text | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Read-ErrorResponseBody {
    param($Response)

    if ($null -eq $Response) {
        return ""
    }

    try {
        $stream = $Response.GetResponseStream()
        if ($null -eq $stream) {
            return ""
        }

        $reader = New-Object System.IO.StreamReader($stream)
        return $reader.ReadToEnd()
    }
    catch {
        return ""
    }
}

function Invoke-Api {
    param(
        [ValidateSet("GET", "POST")]
        [string]$Method,

        [string]$Url,

        [object]$Body = $null
    )

    try {
        if ($null -eq $Body) {
            $response = Invoke-WebRequest `
                -Method $Method `
                -Uri $Url `
                -UseBasicParsing
        }
        else {
            $json = $Body | ConvertTo-Json -Depth 50

            $response = Invoke-WebRequest `
                -Method $Method `
                -Uri $Url `
                -Body $json `
                -ContentType "application/json; charset=utf-8" `
                -UseBasicParsing
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Body       = [string]$response.Content
            Json       = ConvertFrom-JsonSafe ([string]$response.Content)
        }
    }
    catch {
        $response = $_.Exception.Response
        $statusCode = 0

        if ($response -ne $null) {
            $statusCode = [int]$response.StatusCode
        }

        $bodyText = Read-ErrorResponseBody $response

        return [pscustomobject]@{
            StatusCode = $statusCode
            Body       = $bodyText
            Json       = ConvertFrom-JsonSafe $bodyText
        }
    }
}

function Assert-Status {
    param(
        [object]$Response,
        [int[]]$Expected,
        [string]$Label
    )

    if ($Expected -contains $Response.StatusCode) {
        Write-Host "OK   [$($Response.StatusCode)] $Label" -ForegroundColor Green
        return
    }

    Write-Host "KO   [$($Response.StatusCode)] $Label" -ForegroundColor Red
    Write-Host "Réponse brute :" -ForegroundColor Yellow
    Write-Host $Response.Body
    throw "Statut HTTP inattendu pour : $Label"
}

function Get-JsonValue {
    param(
        [object]$Object,
        [string[]]$Names
    )

    if ($null -eq $Object) {
        return $null
    }

    foreach ($name in $Names) {
        if ($Object.PSObject.Properties.Name -contains $name) {
            return $Object.$name
        }
    }

    return $null
}

function Get-LinesFromPreparationResponse {
    param([object]$Json)

    $lines = Get-JsonValue $Json @("lignes", "Lignes")
    if ($null -eq $lines) {
        return @()
    }

    if ($lines -is [System.Array]) {
        return $lines
    }

    return @($lines)
}

function Get-QuantitesFromLine {
    param([object]$Line)

    $quantites = Get-JsonValue $Line @("quantites", "Quantites")
    if ($null -eq $quantites) {
        return @()
    }

    if ($quantites -is [System.Array]) {
        return $quantites
    }

    return @($quantites)
}

function Build-VerrouillagePayload {
    param(
        [object[]]$PreparationLines,
        [string]$DateTournee,
        [string]$CodeTournee,
        [string]$LotId,
        [bool]$IncludeRollsVidesInvalid = $false,
        [int]$ForcedQuantity = 2
    )

    $payloadLines = @()
    $lineIndex = 0

    foreach ($line in $PreparationLines) {
        $lineIndex++

        $idLigneSource = Get-JsonValue $line @("idLigneSource", "IdLigneSource")
        $ordreArret = Get-JsonValue $line @("ordreArret", "OrdreArret")
        $horaire = Get-JsonValue $line @("horaire", "Horaire")
        $numClient = Get-JsonValue $line @("numClient", "NumClient")
        $nomClient = Get-JsonValue $line @("nomClient", "NomClient")
        $nomAffiche = Get-JsonValue $line @("nomAffiche", "NomAffiche")
        $codePdl = Get-JsonValue $line @("codePDL", "codePdl", "CodePDL", "CodePdl")
        $descriptionPdl = Get-JsonValue $line @("descriptionPDL", "descriptionPdl", "DescriptionPDL", "DescriptionPdl")

        $quantites = @()

        if ($IncludeRollsVidesInvalid) {
            $quantites += [pscustomobject]@{
                codeArticle = "ROLLS_VIDES"
                libelle = "Rolls vides"
                quantiteLivreePrevue = 2
            }
        }
        else {
            $sourceQuantites = Get-QuantitesFromLine $line
            $articleIndex = 0

            foreach ($q in $sourceQuantites) {
                $codeArticle = Get-JsonValue $q @("codeArticle", "CodeArticle")
                $libelle = Get-JsonValue $q @("libelle", "Libelle", "libelleArticle", "LibelleArticle")

                if ([string]::IsNullOrWhiteSpace($codeArticle)) {
                    continue
                }

                if ($codeArticle.Trim().ToUpperInvariant() -eq "ROLLS_VIDES") {
                    continue
                }

                $articleIndex++

                # On met une quantité prévue sur le premier article de la première ligne.
                # Le reste reste à 0 pour créer un payload simple et non ambigu.
                $quantitePrevue = 0
                if ($lineIndex -eq 1 -and $articleIndex -eq 1) {
                    $quantitePrevue = $ForcedQuantity
                }

                $quantites += [pscustomobject]@{
                    codeArticle = $codeArticle
                    libelle = $libelle
                    quantiteLivreePrevue = $quantitePrevue
                }
            }

            if ($quantites.Count -eq 0) {
                $quantites += [pscustomobject]@{
                    codeArticle = "ROLLS"
                    libelle = "Rolls"
                    quantiteLivreePrevue = $ForcedQuantity
                }
            }
        }

        $commentaire = $null
        if ($lineIndex -eq 1 -and -not $IncludeRollsVidesInvalid) {
            $commentaire = "TEST API - commentaire exceptionnel généré par run-tests-api-expedition-mobile.ps1"
        }

        $payloadLines += [pscustomobject]@{
            idLigneSource = $idLigneSource
            ordreArret = $ordreArret
            horaire = $horaire
            numClient = $numClient
            nomClient = $nomClient
            nomAffiche = $nomAffiche
            codePDL = $codePdl
            descriptionPDL = $descriptionPdl
            commentaireExceptionnel = $commentaire
            quantites = $quantites
        }
    }

    return [pscustomobject]@{
        schemaVersion = "1.0"
        idLotVerrouillage = $LotId
        dateTournee = $DateTournee
        codeTournee = $CodeTournee
        libelleTournee = "TEST API EXPEDITION"
        utilisateur = [pscustomobject]@{
            identifiant = "test.api"
            nomAffiche = "Test API"
        }
        lignes = $payloadLines
    }
}

function Save-JsonFile {
    param(
        [object]$Object,
        [string]$Path
    )

    $Object | ConvertTo-Json -Depth 50 | Out-File -FilePath $Path -Encoding utf8
}

$ApiBaseUrl = $ApiBaseUrl.TrimEnd("/")
$encodedDate = [uri]::EscapeDataString($DateTournee)
$encodedTournee = [uri]::EscapeDataString($CodeTournee)
$encodedLivreur = [uri]::EscapeDataString($CodeLivreur)

Write-Section "Configuration"
Write-Host "API         : $ApiBaseUrl"
Write-Host "DateTournee : $DateTournee"
Write-Host "CodeTournee : $CodeTournee"
Write-Host "CodeLivreur : $CodeLivreur"
Write-Host "MaxLignes   : $MaxLignes"

Write-Section "1 - Health check"
$health = Invoke-Api -Method GET -Url "$ApiBaseUrl/api/health"
Assert-Status -Response $health -Expected @(200) -Label "GET /api/health"
Write-Host $health.Body

Write-Section "2 - GET Expédition : préparations à préparer"
$getExpeditionUrl = "$ApiBaseUrl/api/expedition/preparations/a-preparer?dateTournee=$encodedDate&codeTournee=$encodedTournee"
$getExpedition = Invoke-Api -Method GET -Url $getExpeditionUrl
Assert-Status -Response $getExpedition -Expected @(200) -Label "GET /api/expedition/preparations/a-preparer"

$getExpedition.Body | Out-File -FilePath ".\result-get-expedition-a-preparer.json" -Encoding utf8

$preparationLines = @(Get-LinesFromPreparationResponse $getExpedition.Json)

if ($preparationLines.Count -eq 0) {
    throw "Aucune ligne retournée par le GET Expédition. Vérifie la date et le code tournée."
}

Write-Host "Nombre de lignes retournées : $($preparationLines.Count)" -ForegroundColor Green

$selectedLines = @($preparationLines | Select-Object -First $MaxLignes)

Write-Section "3 - POST Expédition invalide : ROLLS_VIDES préparé"
$invalidLotId = [guid]::NewGuid().ToString("D")
$invalidPayload = Build-VerrouillagePayload `
    -PreparationLines @($selectedLines | Select-Object -First 1) `
    -DateTournee $DateTournee `
    -CodeTournee $CodeTournee `
    -LotId $invalidLotId `
    -IncludeRollsVidesInvalid $true

Save-JsonFile -Object $invalidPayload -Path ".\payload-expedition-rolls-vides-invalide.json"

$invalidPost = Invoke-Api `
    -Method POST `
    -Url "$ApiBaseUrl/api/expedition/preparations/verrouiller" `
    -Body $invalidPayload

Assert-Status -Response $invalidPost -Expected @(400) -Label "POST verrouiller avec ROLLS_VIDES > 0 doit être refusé"
Write-Host $invalidPost.Body

Write-Section "4 - POST Expédition valide : verrouillage"
$validLotId = [guid]::NewGuid().ToString("D")
$validPayload = Build-VerrouillagePayload `
    -PreparationLines $selectedLines `
    -DateTournee $DateTournee `
    -CodeTournee $CodeTournee `
    -LotId $validLotId `
    -ForcedQuantity 2

Save-JsonFile -Object $validPayload -Path ".\payload-expedition-verrouillage-valide.json"

$validPost = Invoke-Api `
    -Method POST `
    -Url "$ApiBaseUrl/api/expedition/preparations/verrouiller" `
    -Body $validPayload

# Si la préparation est déjà verrouillée, le test n'est pas forcément faux.
# Cela veut juste dire que cette date/tournée a déjà été préparée dans ta base.
Assert-Status -Response $validPost -Expected @(200, 409) -Label "POST verrouiller préparation valide"
Write-Host $validPost.Body

if ($validPost.StatusCode -eq 409) {
    Write-Host ""
    Write-Host "La tournée semble déjà verrouillée. Le script continue avec le GET mobile pour vérifier l'injection existante." -ForegroundColor Yellow
}
else {
    Write-Section "5 - POST Expédition idempotent : même lot, même contenu"
    $samePost = Invoke-Api `
        -Method POST `
        -Url "$ApiBaseUrl/api/expedition/preparations/verrouiller" `
        -Body $validPayload

    Assert-Status -Response $samePost -Expected @(200) -Label "Même idLotVerrouillage + même contenu"
    Write-Host $samePost.Body

    Write-Section "6 - POST Expédition conflit : même lot, contenu différent"
    $differentPayload = Build-VerrouillagePayload `
        -PreparationLines $selectedLines `
        -DateTournee $DateTournee `
        -CodeTournee $CodeTournee `
        -LotId $validLotId `
        -ForcedQuantity 3

    Save-JsonFile -Object $differentPayload -Path ".\payload-expedition-meme-lot-contenu-different.json"

    $differentPost = Invoke-Api `
        -Method POST `
        -Url "$ApiBaseUrl/api/expedition/preparations/verrouiller" `
        -Body $differentPayload

    Assert-Status -Response $differentPost -Expected @(409) -Label "Même idLotVerrouillage + contenu différent"
    Write-Host $differentPost.Body
}

Write-Section "7 - GET mobile : tournée du jour"
$getMobileUrl = "$ApiBaseUrl/api/tournees/jour?dateTournee=$encodedDate&codeTournee=$encodedTournee&codeLivreur=$encodedLivreur"
$getMobile = Invoke-Api -Method GET -Url $getMobileUrl
Assert-Status -Response $getMobile -Expected @(200) -Label "GET /api/tournees/jour"

$getMobile.Body | Out-File -FilePath ".\result-get-mobile-tournee-jour.json" -Encoding utf8

Write-Host "Réponse sauvegardée dans result-get-mobile-tournee-jour.json" -ForegroundColor Green

Write-Section "8 - Vérification rapide dans le JSON mobile"
$mobileLines = Get-JsonValue $getMobile.Json @("lignes", "Lignes")
if ($null -eq $mobileLines) {
    throw "Le GET mobile ne contient pas lignes[]."
}

$firstPayloadLine = $validPayload.lignes[0]
$targetIdLigneSource = $firstPayloadLine.idLigneSource

$matchingLine = $null
foreach ($line in @($mobileLines)) {
    $id = Get-JsonValue $line @("idLigneSource", "IdLigneSource")
    if ($id -eq $targetIdLigneSource) {
        $matchingLine = $line
        break
    }
}

if ($null -eq $matchingLine) {
    Write-Host "Impossible de retrouver la ligne préparée dans le GET mobile par idLigneSource." -ForegroundColor Yellow
    Write-Host "Id recherché : $targetIdLigneSource"
    Write-Host "Vérifie manuellement result-get-mobile-tournee-jour.json."
}
else {
    $infosLivreur = Get-JsonValue $matchingLine @("infosLivreur", "InfosLivreur")
    $saisie = Get-JsonValue $matchingLine @("saisie", "Saisie")
    $quantitesMobile = Get-JsonValue $saisie @("quantites", "Quantites")

    Write-Host "Ligne trouvée : $targetIdLigneSource" -ForegroundColor Green

    $commentaireExceptionnel = Get-JsonValue $infosLivreur @("commentaireExceptionnel", "CommentaireExceptionnel")
    Write-Host "commentaireExceptionnel : $commentaireExceptionnel"

    Write-Host ""
    Write-Host "Quantités mobile :"
    foreach ($q in @($quantitesMobile)) {
        $code = Get-JsonValue $q @("codeArticle", "CodeArticle")
        $prevue = Get-JsonValue $q @("quantiteLivreePrevue", "QuantiteLivreePrevue")
        $livree = Get-JsonValue $q @("quantiteLivree", "QuantiteLivree")
        $recup = Get-JsonValue $q @("quantiteRecuperee", "QuantiteRecuperee")

        Write-Host "  $code -> prévue=$prevue ; livrée=$livree ; récupérée=$recup"
    }
}

Write-Section "Fin des tests"
Write-Host "Fichiers générés :" -ForegroundColor Green
Write-Host " - result-get-expedition-a-preparer.json"
Write-Host " - payload-expedition-rolls-vides-invalide.json"
Write-Host " - payload-expedition-verrouillage-valide.json"
Write-Host " - payload-expedition-meme-lot-contenu-different.json si applicable"
Write-Host " - result-get-mobile-tournee-jour.json"
