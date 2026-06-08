#requires -RunAsAdministrator
<#
.SYNOPSIS
    Mise a jour applicative API MobileSLI sur SRVAPI1.

.DESCRIPTION
    Ce script sert a mettre a jour l'application API uniquement.

    Il ne reconfigure pas HTTPS.
    Il ne cree pas de certificat.
    Il ne modifie pas les bindings IIS.
    Il ne modifie pas HTTP.sys.
    Il ne configure pas le site CRL.
    Il ne remplace pas appsettings.Production.json.
    Il preserve web.config par defaut pour eviter d'ecraser une configuration IIS/ANCM locale.

    Configuration attendue deja validee par le script HTTPS :
    - Site IIS API : MobileSLI.Api
    - Binding fallback HTTP : http *:5000:
    - Binding HTTPS : https *:443:srvapi1.sli.local
    - Health HTTPS : https://srvapi1.sli.local/api/health
    - Health fallback : http://srvapi1.sli.local:5000/api/health
    - CRL : http://srvapi1.sli.local/crl/mobilesli-root-ca.crl

    Utilisation normale :
        Set-ExecutionPolicy -Scope Process Bypass -Force
        .\update-api-iis.ps1

    Options utiles :
        -SkipRuntimeTests
            Ignore les tests curl avant/apres deploiement.

        -SkipGitPull
            Ne fait pas git pull. Utile si le depot a deja ete mis a jour manuellement sur SRVAPI1.

        -ReplaceWebConfig
            Autorise le remplacement du web.config par celui du publish.
            A eviter sauf si le web.config publie doit vraiment etre redeploye.
#>

[CmdletBinding()]
param(
    [string]$SiteName = "MobileSLI.Api",
    [string]$SourcePath = "C:\Sources\API-ASP.NET-Core",
    [string]$ProjectPath = "C:\Sources\API-ASP.NET-Core\API-ASP.NET-Core.csproj",
    [string]$PublishPath = "C:\Publish\MobileSLI.Api",
    [string]$BackupRoot = "C:\Backups\MobileSLI.Api",
    [string]$ApiHostName = "srvapi1.sli.local",
    [int]$FallbackHttpPort = 5000,
    [switch]$SkipRuntimeTests,
    [switch]$SkipGitPull,
    [switch]$ReplaceWebConfig
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$HttpsHealthUrl = "https://$ApiHostName/api/health"
$HttpFallbackHealthUrl = "http://${ApiHostName}:$FallbackHttpPort/api/health"
$CrlUrl = "http://$ApiHostName/crl/mobilesli-root-ca.crl"
$ExpectedHttpBinding = "*:${FallbackHttpPort}:"
$ExpectedHttpsBinding = "*:443:$ApiHostName"

$RobocopyExcludedDirectories = @("logs", "data")
$RobocopyExcludedFiles = @("*.log", "appsettings.Production.json")

if (-not $ReplaceWebConfig) {
    $RobocopyExcludedFiles += "web.config"
}

function Write-Step {
    param([Parameter(Mandatory = $true)][string]$Message)

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
}

function Write-Ok {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Fail {
    param([Parameter(Mandatory = $true)][string]$Message)
    throw "[ERREUR] $Message"
}

function Assert-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)

    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Fail "Ce script doit etre lance dans PowerShell en administrateur."
    }

    Write-Ok "PowerShell administrateur verifie."
}

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if (-not (Test-Path $Path)) {
        Fail "$Label introuvable : $Path"
    }

    Write-Ok "$Label present : $Path"
}

function Invoke-ExternalChecked {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Command,
        [Parameter(Mandatory = $true)][string]$ErrorMessage
    )

    & $Command
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        Fail "$ErrorMessage Code=$exitCode"
    }
}

function Invoke-RobocopyChecked {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [Parameter(Mandatory = $true)][string]$Description
    )

    Assert-PathExists -Path $Source -Label "Source robocopy"

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null

    $args = @($Source, $Destination, "/MIR", "/R:3", "/W:5")

    if ($RobocopyExcludedDirectories.Count -gt 0) {
        $args += "/XD"
        $args += $RobocopyExcludedDirectories
    }

    if ($RobocopyExcludedFiles.Count -gt 0) {
        $args += "/XF"
        $args += $RobocopyExcludedFiles
    }

    Write-Host "robocopy $Source $Destination $($args[2..($args.Count - 1)] -join ' ')"

    & robocopy @args
    $exitCode = $LASTEXITCODE

    if ($exitCode -gt 7) {
        Fail "$Description echoue. Code Robocopy=$exitCode"
    }

    Write-Ok "$Description termine. Code Robocopy=$exitCode"
}

function Import-IisModuleOrStop {
    Write-Step "Chargement du module IIS WebAdministration"

    try {
        Import-Module WebAdministration -ErrorAction Stop
    }
    catch {
        Fail "Module WebAdministration indisponible : $($_.Exception.Message)"
    }

    Write-Ok "Module WebAdministration charge."
}

function Get-IisDeploymentInfo {
    Write-Step "Lecture configuration IIS existante"

    $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue

    if ($null -eq $site) {
        Fail "Site IIS introuvable : $SiteName"
    }

    $appPoolName = $site.ApplicationPool
    $deployPath = [Environment]::ExpandEnvironmentVariables($site.PhysicalPath)

    if ([string]::IsNullOrWhiteSpace($deployPath)) {
        Fail "Chemin physique IIS vide pour le site : $SiteName"
    }

    Assert-PathExists -Path $deployPath -Label "Dossier deploiement IIS"

    [PSCustomObject]@{
        Site = $site
        AppPoolName = $appPoolName
        DeployPath = $deployPath
    }
}

function Assert-ExpectedIisState {
    Write-Step "Verification configuration IIS existante"

    $bindings = Get-WebBinding -Name $SiteName -ErrorAction Stop

    Write-Host "Bindings IIS actuels :"
    $bindings | Select-Object protocol, bindingInformation | Format-Table -AutoSize

    $httpFallbackBinding = $bindings |
        Where-Object {
            $_.protocol -eq "http" -and $_.bindingInformation -eq $ExpectedHttpBinding
        } |
        Select-Object -First 1

    $httpsBinding = $bindings |
        Where-Object {
            $_.protocol -eq "https" -and $_.bindingInformation -eq $ExpectedHttpsBinding
        } |
        Select-Object -First 1

    if ($null -eq $httpFallbackBinding) {
        Fail "Binding fallback HTTP attendu absent : $ExpectedHttpBinding. Ce script ne reconfigure pas IIS."
    }

    if ($null -eq $httpsBinding) {
        Fail "Binding HTTPS attendu absent : $ExpectedHttpsBinding. Relancer d'abord Configure-MobileSLI-ApiHttpsAndCrl.ps1."
    }

    Write-Ok "Binding fallback HTTP conserve : $ExpectedHttpBinding"
    Write-Ok "Binding HTTPS conserve : $ExpectedHttpsBinding"
}

function Test-DotNetEnvironment {
    Write-Step "Verification .NET et projet"

    Assert-PathExists -Path $SourcePath -Label "Dossier source"
    Assert-PathExists -Path $ProjectPath -Label "Projet API"

    dotnet --list-sdks
    if ($LASTEXITCODE -ne 0) {
        Fail "dotnet --list-sdks a echoue."
    }

    Write-Ok ".NET disponible."
}

function Invoke-CurlStatus {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [int[]]$AllowedStatusCodes = @(200),
        [switch]$ShowBody
    )

    $tempBody = Join-Path $env:TEMP ("mobilesli-api-test-" + [guid]::NewGuid().ToString("N") + ".txt")

    try {
        $curlOutput = & curl.exe -sS -L -o $tempBody -w "%{http_code}|%{content_type}" $Url 2>&1
        $exitCode = $LASTEXITCODE

        Write-Host "$Url => $curlOutput"

        if ($exitCode -ne 0) {
            Fail "curl a echoue pour $Url. Code=$exitCode. Sortie=$curlOutput"
        }

        $parts = "$curlOutput".Split("|")
        $statusText = $parts[0].Trim()

        [int]$statusCode = 0
        if (-not [int]::TryParse($statusText, [ref]$statusCode)) {
            Fail "Code HTTP illisible pour $Url. Sortie=$curlOutput"
        }

        if ($AllowedStatusCodes -notcontains $statusCode) {
            Fail "Code HTTP inattendu pour $Url. Recu=$statusCode. Attendu=$($AllowedStatusCodes -join ',')"
        }

        if ($ShowBody -and (Test-Path $tempBody)) {
            Write-Host "Reponse :"
            Get-Content $tempBody -Raw | Write-Host
        }

        Write-Ok "Test HTTP valide pour $Url. Code=$statusCode"
    }
    finally {
        Remove-Item -Path $tempBody -Force -ErrorAction SilentlyContinue
    }
}

function Test-RuntimeState {
    param([string]$Phase)

    if ($SkipRuntimeTests) {
        Write-Warn "Tests runtime ignores pour la phase : $Phase"
        return
    }

    Write-Step "Tests runtime $Phase"

    Test-NetConnection $ApiHostName -Port 443 | Select-Object ComputerName, RemoteAddress, RemotePort, TcpTestSucceeded | Format-List
    Test-NetConnection $ApiHostName -Port 80 | Select-Object ComputerName, RemoteAddress, RemotePort, TcpTestSucceeded | Format-List
    Test-NetConnection $ApiHostName -Port $FallbackHttpPort | Select-Object ComputerName, RemoteAddress, RemotePort, TcpTestSucceeded | Format-List

    Invoke-CurlStatus -Url $CrlUrl -AllowedStatusCodes @(200)
    Invoke-CurlStatus -Url $HttpsHealthUrl -AllowedStatusCodes @(200) -ShowBody
    Invoke-CurlStatus -Url $HttpFallbackHealthUrl -AllowedStatusCodes @(200) -ShowBody
}

function Update-GitAndPublish {
    Write-Step "Mise a jour Git et publication Release"

    Set-Location $SourcePath

    Write-Host "Depot : $SourcePath"

    git status
    if ($LASTEXITCODE -ne 0) {
        Fail "git status a echoue."
    }

    if ($SkipGitPull) {
        Write-Warn "git pull ignore a la demande."
    }
    else {
        Invoke-ExternalChecked `
            -Command { git pull --ff-only } `
            -ErrorMessage "git pull --ff-only a echoue."
    }

    Invoke-ExternalChecked `
        -Command { dotnet restore $ProjectPath } `
        -ErrorMessage "dotnet restore a echoue."

    Invoke-ExternalChecked `
        -Command { dotnet build $ProjectPath -c Release --no-restore } `
        -ErrorMessage "dotnet build Release a echoue."

    Remove-Item -Path $PublishPath -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $PublishPath -Force | Out-Null

    Invoke-ExternalChecked `
        -Command { dotnet publish $ProjectPath -c Release -o $PublishPath --no-build } `
        -ErrorMessage "dotnet publish Release a echoue."

    Assert-PathExists -Path (Join-Path $PublishPath "API-ASP.NET-Core.dll") -Label "DLL publiee"
    Assert-PathExists -Path (Join-Path $PublishPath "API-ASP.NET-Core.exe") -Label "Executable publie"
    Assert-PathExists -Path (Join-Path $PublishPath "appsettings.json") -Label "appsettings.json publie"

    if ($ReplaceWebConfig) {
        Assert-PathExists -Path (Join-Path $PublishPath "web.config") -Label "web.config publie"
    }
    else {
        Write-Warn "web.config publie non deployee par defaut. Option -ReplaceWebConfig non active."
    }

    Write-Ok "Publication locale terminee : $PublishPath"
}

function Stop-IisApplication {
    param([Parameter(Mandatory = $true)][string]$AppPoolName)

    Write-Step "Arret application IIS"

    $site = Get-Website -Name $SiteName -ErrorAction Stop

    if ($site.State -eq "Started") {
        Stop-Website -Name $SiteName
        Write-Ok "Site IIS arrete : $SiteName"
    }
    else {
        Write-Warn "Site IIS deja arrete : $SiteName"
    }

    $appPoolState = (Get-WebAppPoolState -Name $AppPoolName -ErrorAction Stop).Value

    if ($appPoolState -eq "Started") {
        Stop-WebAppPool -Name $AppPoolName
        Write-Ok "AppPool arrete : $AppPoolName"
    }
    else {
        Write-Warn "AppPool deja arrete : $AppPoolName"
    }

    Start-Sleep -Seconds 3
}

function Start-IisApplication {
    param([Parameter(Mandatory = $true)][string]$AppPoolName)

    Write-Step "Demarrage application IIS"

    $appPoolState = (Get-WebAppPoolState -Name $AppPoolName -ErrorAction Stop).Value

    if ($appPoolState -ne "Started") {
        Start-WebAppPool -Name $AppPoolName
        Write-Ok "AppPool demarre : $AppPoolName"
    }
    else {
        Write-Warn "AppPool deja demarre : $AppPoolName"
    }

    $site = Get-Website -Name $SiteName -ErrorAction Stop

    if ($site.State -ne "Started") {
        Start-Website -Name $SiteName
        Write-Ok "Site IIS demarre : $SiteName"
    }
    else {
        Write-Warn "Site IIS deja demarre : $SiteName"
    }

    Start-Sleep -Seconds 5
}

function Backup-CurrentDeployment {
    param([Parameter(Mandatory = $true)][string]$DeployPath)

    Write-Step "Sauvegarde deploiement actuel"

    Assert-PathExists -Path $DeployPath -Label "Dossier deploiement a sauvegarder"

    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null

    $backupPath = Join-Path $BackupRoot (Get-Date -Format "yyyyMMdd-HHmmss")
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null

    Invoke-RobocopyChecked `
        -Source $DeployPath `
        -Destination $backupPath `
        -Description "Sauvegarde"

    Write-Ok "Backup cree : $backupPath"

    return $backupPath
}

function Deploy-PublishedFiles {
    param([Parameter(Mandatory = $true)][string]$DeployPath)

    Write-Step "Copie nouvelle version applicative"

    Write-Host "Elements exclus du deploiement :"
    Write-Host "Dossiers : $($RobocopyExcludedDirectories -join ', ')"
    Write-Host "Fichiers : $($RobocopyExcludedFiles -join ', ')"

    Invoke-RobocopyChecked `
        -Source $PublishPath `
        -Destination $DeployPath `
        -Description "Deploiement"
}

function Grant-AppPoolPermissions {
    param(
        [Parameter(Mandatory = $true)][string]$DeployPath,
        [Parameter(Mandatory = $true)][string]$AppPoolName
    )

    Write-Step "Droits AppPool sur logs et data"

    $appPoolIdentity = "IIS AppPool\${AppPoolName}"

    $paths = @(
        (Join-Path $DeployPath "logs"),
        (Join-Path $DeployPath "data")
    )

    foreach ($path in $paths) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
        icacls $path /grant "${appPoolIdentity}:(OI)(CI)M" /T | Out-Host

        if ($LASTEXITCODE -ne 0) {
            Fail "icacls a echoue pour : $path"
        }
    }

    Write-Ok "Droits AppPool verifies."
}

function Show-DeployedVersion {
    param([Parameter(Mandatory = $true)][string]$DeployPath)

    Write-Step "Informations version deployee"

    $dllPath = Join-Path $DeployPath "API-ASP.NET-Core.dll"
    $exePath = Join-Path $DeployPath "API-ASP.NET-Core.exe"
    $appsettingsPath = Join-Path $DeployPath "appsettings.json"
    $webConfigPath = Join-Path $DeployPath "web.config"

    Assert-PathExists -Path $dllPath -Label "DLL deployee"
    Assert-PathExists -Path $exePath -Label "Executable deploye"
    Assert-PathExists -Path $appsettingsPath -Label "appsettings.json deploye"

    Get-Item $dllPath, $exePath, $appsettingsPath |
        Select-Object FullName, LastWriteTime, Length |
        Format-Table -AutoSize

    if (Test-Path $webConfigPath) {
        Get-Item $webConfigPath |
            Select-Object FullName, LastWriteTime, Length |
            Format-Table -AutoSize
    }

    Set-Location $SourcePath
    Write-Host "Dernier commit source :"
    git log -1 --oneline
}

function Run-FinalChecks {
    param(
        [Parameter(Mandatory = $true)][string]$AppPoolName,
        [Parameter(Mandatory = $true)][string]$DeployPath
    )

    Write-Step "Verifications finales IIS"

    Get-Website -Name $SiteName | Format-Table -AutoSize
    Get-WebAppPoolState -Name $AppPoolName | Format-Table -AutoSize

    Assert-ExpectedIisState
    Show-DeployedVersion -DeployPath $DeployPath
    Test-RuntimeState -Phase "apres deploiement"
}

Write-Step "Mise a jour applicative API MobileSLI"

Write-Host "Site IIS                  : $SiteName"
Write-Host "Source                    : $SourcePath"
Write-Host "Projet                    : $ProjectPath"
Write-Host "Publish                   : $PublishPath"
Write-Host "Backups                   : $BackupRoot"
Write-Host "API HTTPS                 : $HttpsHealthUrl"
Write-Host "API fallback HTTP         : $HttpFallbackHealthUrl"
Write-Host "CRL                       : $CrlUrl"
Write-Host "Binding HTTPS attendu     : $ExpectedHttpsBinding"
Write-Host "Binding HTTP attendu      : $ExpectedHttpBinding"
Write-Host "ReplaceWebConfig          : $ReplaceWebConfig"
Write-Host "SkipRuntimeTests          : $SkipRuntimeTests"
Write-Host "SkipGitPull               : $SkipGitPull"

Assert-Admin
Import-IisModuleOrStop

$deploymentInfo = Get-IisDeploymentInfo
$AppPoolName = $deploymentInfo.AppPoolName
$DeployPath = $deploymentInfo.DeployPath

Write-Host ""
Write-Host "Configuration IIS detectee :"
Write-Host "Site IIS       : $SiteName"
Write-Host "AppPool        : $AppPoolName"
Write-Host "Chemin IIS     : $DeployPath"

Assert-ExpectedIisState
Test-RuntimeState -Phase "avant deploiement"
Test-DotNetEnvironment
Update-GitAndPublish

$backupPath = $null

try {
    Stop-IisApplication -AppPoolName $AppPoolName
    $backupPath = Backup-CurrentDeployment -DeployPath $DeployPath
    Deploy-PublishedFiles -DeployPath $DeployPath
    Grant-AppPoolPermissions -DeployPath $DeployPath -AppPoolName $AppPoolName
    Start-IisApplication -AppPoolName $AppPoolName
    Run-FinalChecks -AppPoolName $AppPoolName -DeployPath $DeployPath
}
catch {
    Write-Step "Erreur pendant la mise a jour API"
    Write-Host $_.Exception.Message -ForegroundColor Red

    if ($null -ne $backupPath) {
        Write-Host ""
        Write-Host "Backup disponible pour retour arriere : $backupPath" -ForegroundColor Yellow
        Write-Host "Le script ne restaure pas automatiquement pour eviter d'ecraser un etat intermediaire sans validation." -ForegroundColor Yellow
    }

    try {
        Start-IisApplication -AppPoolName $AppPoolName
    }
    catch {
        Write-Host "Redemarrage IIS apres erreur impossible : $($_.Exception.Message)" -ForegroundColor Red
    }

    throw
}

Write-Step "Mise a jour API terminee"

Write-Host "API HTTPS validee         : $HttpsHealthUrl" -ForegroundColor Green
Write-Host "Fallback HTTP valide      : $HttpFallbackHealthUrl" -ForegroundColor Green
Write-Host "CRL validee               : $CrlUrl" -ForegroundColor Green
Write-Host "Site IIS                  : $SiteName" -ForegroundColor Green
Write-Host "Dossier deploye           : $DeployPath" -ForegroundColor Green
Write-Host "Backup                    : $backupPath" -ForegroundColor Green
Write-Host ""
Write-Host "[OK] Mise a jour applicative terminee sans reconfiguration HTTPS/IIS." -ForegroundColor Green
