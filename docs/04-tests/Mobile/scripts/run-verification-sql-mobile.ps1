param(
    [Parameter(Mandatory = $true)]
    [string]$ServerInstance,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [Parameter(Mandatory = $false)]
    [string]$SqlFile = '',

    [Parameter(Mandatory = $false)]
    [string]$DateTournee = '',

    [Parameter(Mandatory = $false)]
    [switch]$UseSqlAuthentication,

    [Parameter(Mandatory = $false)]
    [string]$Username = '',

    [Parameter(Mandatory = $false)]
    [string]$Password = ''
)

$ErrorActionPreference = 'Stop'

function Get-ScriptDirectory {
    if ($PSScriptRoot -and $PSScriptRoot.Trim().Length -gt 0) {
        return $PSScriptRoot
    }

    return Split-Path -Parent $MyInvocation.MyCommand.Path
}

$scriptDirectory = Get-ScriptDirectory
$mobileTestsDirectory = Split-Path -Parent $scriptDirectory

if ([string]::IsNullOrWhiteSpace($SqlFile)) {
    $SqlFile = Join-Path $mobileTestsDirectory 'sql\verification_synchronisation_mobile_v12.sql'
}

if ([string]::IsNullOrWhiteSpace($DateTournee)) {
    $DateTournee = Get-Date -Format 'yyyy-MM-dd'
}

if (-not (Test-Path $SqlFile)) {
    throw ('Fichier SQL introuvable : ' + $SqlFile)
}

$sqlcmdCommand = Get-Command sqlcmd -ErrorAction SilentlyContinue

if ($null -eq $sqlcmdCommand) {
    throw 'sqlcmd est introuvable. Installe SQL Server Command Line Utilities ou lance le script depuis une machine qui possede sqlcmd.'
}

Write-Host ''
Write-Host '=== Verification SQL Mobile SLI ==='
Write-Host ('Serveur SQL : ' + $ServerInstance)
Write-Host ('Base        : ' + $Database)
Write-Host ('Fichier SQL : ' + $SqlFile)
Write-Host ('Date        : ' + $DateTournee)
Write-Host ''

$arguments = @()
$arguments += '-S'
$arguments += $ServerInstance
$arguments += '-d'
$arguments += $Database
$arguments += '-i'
$arguments += $SqlFile
$arguments += '-v'
$arguments += ('DateTournee=' + $DateTournee)
$arguments += '-W'
$arguments += '-s'
$arguments += ';'

if ($UseSqlAuthentication) {
    if ([string]::IsNullOrWhiteSpace($Username)) {
        throw 'Username est obligatoire avec -UseSqlAuthentication.'
    }

    if ([string]::IsNullOrWhiteSpace($Password)) {
        throw 'Password est obligatoire avec -UseSqlAuthentication.'
    }

    $arguments += '-U'
    $arguments += $Username
    $arguments += '-P'
    $arguments += $Password
}
else {
    $arguments += '-E'
}

& $sqlcmdCommand.Source @arguments

if ($LASTEXITCODE -ne 0) {
    throw ('La verification SQL a echoue avec le code ' + $LASTEXITCODE + '.')
}

Write-Host ''
Write-Host 'Verification SQL terminee.' -ForegroundColor Green
Write-Host ''
