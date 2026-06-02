[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl,

    [int]$Vus = 10,

    [string]$DateTournee = ""
)

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "run-k6-masse.ps1") `
    -ApiBaseUrl $ApiBaseUrl `
    -Count 50 `
    -Vus $Vus `
    -DateTournee $DateTournee
