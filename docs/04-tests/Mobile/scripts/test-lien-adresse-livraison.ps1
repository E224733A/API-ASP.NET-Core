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

Write-Host '=== Test lienAdresseLivraison ==='
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

$firstLine = @($json.lignes)[0]
if ($null -eq $firstLine.pointLivraison) {
    Fail 'pointLivraison absent sur la première ligne.'
}

$hasProperty = $firstLine.pointLivraison.PSObject.Properties.Name -contains 'lienAdresseLivraison'
if (-not $hasProperty) {
    Fail 'pointLivraison.lienAdresseLivraison absent du JSON.'
}

$lien = $firstLine.pointLivraison.lienAdresseLivraison

if ($ExpectLienAdresseLivraison) {
    if ([string]::IsNullOrWhiteSpace([string]$lien)) {
        Fail 'lienAdresseLivraison attendu non vide en mode Hardcoded, mais il est vide ou null.'
    }

    if (-not [uri]::IsWellFormedUriString([string]$lien, [System.UriKind]::Absolute)) {
        Fail ('lienAdresseLivraison invalide : ' + [string]$lien)
    }
}
else {
    if (-not [string]::IsNullOrWhiteSpace([string]$lien)) {
        Fail ('lienAdresseLivraison attendu null/vide en mode Disabled, obtenu : ' + [string]$lien)
    }
}

Write-Host '[OK] JSON parseable'
Write-Host ('[OK] schemaVersion = ' + [string]$json.schemaVersion)
Write-Host '[OK] pointLivraison présent'
Write-Host ('[OK] lienAdresseLivraison = ' + ($(if ($null -eq $lien) { 'null' } else { [string]$lien })))
exit 0
