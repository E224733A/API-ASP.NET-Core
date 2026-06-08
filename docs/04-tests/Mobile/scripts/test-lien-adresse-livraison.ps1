param(
    [string]$ApiBaseUrl = 'http://srvapi1.sli.local:5000',
    [Parameter(Mandatory = $true)]
    [string]$CodeLivreur,
    [Parameter(Mandatory = $true)]
    [string]$CodeTournee,
    [string]$ExpectedSchemaVersion = '1.2',
    [switch]$ExpectLienAdresseLivraison
)

$ErrorActionPreference = 'Stop'

function Join-Url([string]$BaseUrl, [string]$RelativeUrl) {
    return $BaseUrl.TrimEnd('/') + '/' + $RelativeUrl.TrimStart('/')
}

function Fail([string]$Message) {
    Write-Host ('[KO] ' + $Message) -ForegroundColor Red
    exit 1
}

$url = Join-Url $ApiBaseUrl ('/api/tournees/jour?codeLivreur=' + [uri]::EscapeDataString($CodeLivreur) + '&codeTournee=' + [uri]::EscapeDataString($CodeTournee))

Write-Host '=== Test lienAdresseLivraison final ==='
Write-Host ('URL : ' + $url)

try {
    $response = Invoke-WebRequest -Uri $url -Method Get -UseBasicParsing
}
catch {
    Fail ('GET /api/tournees/jour impossible : ' + $_.Exception.Message)
}

if ([int]$response.StatusCode -ne 200) {
    Fail ('HTTP attendu 200, obtenu ' + [int]$response.StatusCode)
}

try {
    $json = $response.Content | ConvertFrom-Json
}
catch {
    Fail 'Réponse JSON non parseable.'
}

if ([string]$json.schemaVersion -ne $ExpectedSchemaVersion) {
    Fail ('schemaVersion attendu ' + $ExpectedSchemaVersion + ', obtenu ' + [string]$json.schemaVersion)
}

if ($null -eq $json.lignes -or @($json.lignes).Count -eq 0) {
    Fail 'Aucune ligne retournée dans la tournée.'
}

$withLien = @($json.lignes | Where-Object {
    $null -ne $_.pointLivraison -and
    ($_.pointLivraison.PSObject.Properties.Name -contains 'lienAdresseLivraison') -and
    -not [string]::IsNullOrWhiteSpace([string]$_.pointLivraison.lienAdresseLivraison)
})

if ($ExpectLienAdresseLivraison) {
    if ($withLien.Count -eq 0) {
        Fail 'Aucun lienAdresseLivraison non vide trouvé. Vérifier que la vue contient au moins un CodePDL de cette tournée.'
    }

    foreach ($ligne in $withLien) {
        $lien = [string]$ligne.pointLivraison.lienAdresseLivraison
        if (-not [uri]::IsWellFormedUriString($lien, [System.UriKind]::Absolute)) {
            Fail ('lienAdresseLivraison invalide : ' + $lien)
        }
    }
}

Write-Host '[OK] JSON parseable'
Write-Host ('[OK] schemaVersion = ' + [string]$json.schemaVersion)
Write-Host ('[OK] lignes = ' + @($json.lignes).Count)
Write-Host ('[OK] lignes avec lienAdresseLivraison non vide = ' + $withLien.Count)

$withLien |
    Select-Object `
        ordreArret,
        @{Name='codePDL';Expression={$_.pointLivraison.codePDL}},
        @{Name='lienAdresseLivraison';Expression={$_.pointLivraison.lienAdresseLivraison}} |
    Format-Table -AutoSize

exit 0
