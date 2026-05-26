#requires -RunAsAdministrator
<#
MISE A JOUR API MOBILESLI IIS
Script prêt à exécuter après chaque modification Git / nouvelle version.

Contexte projet :
- Site IIS attendu        : MobileSLI.Api
- Dépôt Git API           : C:\Sources\API-ASP.NET-Core
- Projet ASP.NET Core     : API-ASP.NET-Core.csproj
- Dossier de publication  : C:\Publish\MobileSLI.Api
- Dossier de sauvegarde   : C:\Backups\MobileSLI.Api
- Port API local attendu  : 5000

Exécution conseillée :
Set-ExecutionPolicy -Scope Process Bypass -Force
.\update-api-iis.ps1

Important :
- À exécuter sur le serveur qui héberge l'API IIS.
- PowerShell doit être lancé en administrateur.
- Le site IIS MobileSLI.Api doit déjà exister.
- Les secrets / chaînes de connexion ne sont pas écrits dans ce script.
  Ils doivent rester configurés côté serveur : variables d'environnement,
  web.config existant, coffre de secrets ou configuration serveur.
#>

$ErrorActionPreference = "Stop"

# =========================
# Paramètres projet
# =========================

$SiteName = "MobileSLI.Api"

$SourcePath = "C:\Sources\API-ASP.NET-Core"
$ProjectPath = "C:\Sources\API-ASP.NET-Core\API-ASP.NET-Core.csproj"

$PublishPath = "C:\Publish\MobileSLI.Api"
$BackupRoot = "C:\Backups\MobileSLI.Api"

$LocalPort = 5000
$HealthUrl = "http://localhost:$LocalPort/api/health"
$ExpeditionPreparationsUrl = "http://localhost:$LocalPort/api/expedition/preparations/a-preparer"

# Laisse vide pour ne pas modifier l'environnement IIS existant.
# Mets "Development" si tu veux conserver Swagger et le comportement déjà utilisé en test.
$AspNetCoreEnvironment = ""

# Dossiers et fichiers à ne pas écraser pendant le déploiement.
# logs : conserver les journaux locaux.
# appsettings.Production.json : utile si une configuration serveur spécifique existe.
$RobocopyExcludedDirectories = @("logs")
$RobocopyExcludedFiles = @("*.log", "appsettings.Production.json")

# =========================
# Fonctions utilitaires
# =========================

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "=== $Message ===" -ForegroundColor Cyan
}

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Message
    )

    if (-not (Test-Path $Path)) {
        throw "$Message : $Path"
    }
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command,

        [Parameter(Mandatory = $true)]
        [string]$ErrorMessage
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$ErrorMessage Code retour : $LASTEXITCODE"
    }
}

function Invoke-RobocopyChecked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Destination,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    Assert-PathExists $Source "Source robocopy introuvable"

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null

    $args = @(
        $Source,
        $Destination,
        "/MIR"
    )

    if ($RobocopyExcludedDirectories.Count -gt 0) {
        $args += "/XD"
        $args += $RobocopyExcludedDirectories
    }

    if ($RobocopyExcludedFiles.Count -gt 0) {
        $args += "/XF"
        $args += $RobocopyExcludedFiles
    }

    Write-Host $Description
    Write-Host "robocopy $($args -join ' ')" -ForegroundColor DarkGray

    & robocopy @args
    $code = $LASTEXITCODE

    # Robocopy : 0 à 7 = succès ou avertissement non bloquant.
    if ($code -gt 7) {
        throw "$Description échoué. Code robocopy : $code"
    }

    Write-Host "$Description terminé. Code robocopy : $code" -ForegroundColor Green
}

function Import-IisModuleOrStop {
    Write-Step "Chargement du module IIS WebAdministration"

    try {
        Import-Module WebAdministration -ErrorAction Stop
    }
    catch {
        throw @"
Le module WebAdministration est indisponible.
Installe les outils d'administration IIS ou exécute ce script sur le serveur IIS.

Erreur :
$($_.Exception.Message)
"@
    }

    if (-not (Get-Command Get-Website -ErrorAction SilentlyContinue)) {
        throw "Get-Website indisponible. Vérifier l'installation IIS / WebAdministration."
    }
}

function Get-IisDeploymentInfo {
    Write-Step "Lecture de la configuration IIS"

    $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
    if (-not $site) {
        throw "Site IIS introuvable : $SiteName"
    }

    if ([string]::IsNullOrWhiteSpace($site.ApplicationPool)) {
        throw "Le site IIS $SiteName n'a pas d'ApplicationPool associé."
    }

    $appPoolName = $site.ApplicationPool
    $deployPath = [Environment]::ExpandEnvironmentVariables($site.PhysicalPath)

    if ([string]::IsNullOrWhiteSpace($deployPath)) {
        throw "Le chemin physique IIS du site $SiteName est vide."
    }

    return [PSCustomObject]@{
        Site = $site
        AppPoolName = $appPoolName
        DeployPath = $deployPath
    }
}

function Test-DotNetEnvironment {
    Write-Step "Vérification .NET"

    Assert-PathExists $ProjectPath "Projet API introuvable"

    $projectContent = Get-Content $ProjectPath -Raw
    $targetFramework = "inconnu"

    if ($projectContent -match "<TargetFramework>(?<TargetFramework>[^<]+)</TargetFramework>") {
        $targetFramework = $Matches.TargetFramework
    }

    Write-Host "Projet           : $ProjectPath"
    Write-Host "TargetFramework  : $targetFramework"

    Write-Host ""
    Write-Host "SDK .NET installés :" -ForegroundColor Yellow
    dotnet --list-sdks

    if ($targetFramework -eq "net10.0") {
        Write-Host ""
        Write-Host "Attention : le projet cible net10.0. Le serveur doit avoir le SDK .NET 10 pour publier et le Hosting Bundle .NET 10 pour IIS." -ForegroundColor Yellow
    }
}

function Update-GitAndPublish {
    Write-Step "Mise à jour Git et publication Release"

    Assert-PathExists $SourcePath "Dossier source introuvable"
    Assert-PathExists $ProjectPath "Projet API introuvable"

    Set-Location $SourcePath

    Write-Host "Dépôt Git : $SourcePath"

    git status
    if ($LASTEXITCODE -ne 0) {
        throw "git status a échoué."
    }

    git pull
    if ($LASTEXITCODE -ne 0) {
        throw "git pull a échoué. Vérifier les modifications locales ou l'accès au dépôt."
    }

    dotnet restore $ProjectPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore a échoué."
    }

    dotnet build $ProjectPath -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build a échoué. Déploiement annulé."
    }

    Remove-Item $PublishPath -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $PublishPath -Force | Out-Null

    dotnet publish $ProjectPath -c Release -o $PublishPath --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish a échoué. Déploiement annulé."
    }

    Assert-PathExists (Join-Path $PublishPath "web.config") "web.config absent après publication"
}

function Stop-IisApplication {
    param(
        [string]$AppPoolName
    )

    Write-Step "Arrêt IIS"

    $site = Get-Website -Name $SiteName -ErrorAction Stop

    if ($site.State -eq "Started") {
        Write-Host "Arrêt du site IIS : $SiteName"
        Stop-Website -Name $SiteName
    }
    else {
        Write-Host "Site IIS déjà arrêté : $SiteName"
    }

    $appPoolState = (Get-WebAppPoolState -Name $AppPoolName -ErrorAction Stop).Value

    if ($appPoolState -eq "Started") {
        Write-Host "Arrêt de l'AppPool : $AppPoolName"
        Stop-WebAppPool -Name $AppPoolName
    }
    else {
        Write-Host "AppPool déjà arrêté : $AppPoolName"
    }

    Start-Sleep -Seconds 3
}

function Start-IisApplication {
    param(
        [string]$AppPoolName
    )

    Write-Step "Démarrage IIS"

    $appPoolState = (Get-WebAppPoolState -Name $AppPoolName -ErrorAction Stop).Value
    if ($appPoolState -ne "Started") {
        Write-Host "Démarrage de l'AppPool : $AppPoolName"
        Start-WebAppPool -Name $AppPoolName
    }
    else {
        Write-Host "AppPool déjà démarré : $AppPoolName"
    }

    $site = Get-Website -Name $SiteName -ErrorAction Stop
    if ($site.State -ne "Started") {
        Write-Host "Démarrage du site IIS : $SiteName"
        Start-Website -Name $SiteName
    }
    else {
        Write-Host "Site IIS déjà démarré : $SiteName"
    }

    Start-Sleep -Seconds 5
}

function Backup-CurrentDeployment {
    param(
        [string]$DeployPath
    )

    Write-Step "Sauvegarde de la version actuelle"

    if (-not (Test-Path $DeployPath)) {
        Write-Host "Dossier IIS absent, création : $DeployPath" -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $DeployPath -Force | Out-Null
    }

    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupPath = Join-Path $BackupRoot $timestamp

    Invoke-RobocopyChecked `
        -Source $DeployPath `
        -Destination $backupPath `
        -Description "Sauvegarde vers $backupPath"
}

function Deploy-PublishedFiles {
    param(
        [string]$DeployPath
    )

    Write-Step "Copie de la nouvelle version"

    Assert-PathExists $PublishPath "Dossier publish introuvable"

    Invoke-RobocopyChecked `
        -Source $PublishPath `
        -Destination $DeployPath `
        -Description "Déploiement vers $DeployPath"
}

function Ensure-WebConfigEnvironment {
    param(
        [string]$WebConfigPath
    )

    if ([string]::IsNullOrWhiteSpace($AspNetCoreEnvironment)) {
        Write-Host "ASPNETCORE_ENVIRONMENT non modifié : la configuration IIS existante est conservée." -ForegroundColor Yellow
        return
    }

    Write-Step "Configuration ASPNETCORE_ENVIRONMENT dans web.config"

    Assert-PathExists $WebConfigPath "web.config introuvable"

    [xml]$webConfig = Get-Content $WebConfigPath

    $systemWebServer = $webConfig.configuration.'system.webServer'
    if (-not $systemWebServer -and $webConfig.configuration.location) {
        $systemWebServer = $webConfig.configuration.location.'system.webServer'
    }

    if (-not $systemWebServer) {
        throw "web.config invalide : section system.webServer introuvable."
    }

    $aspNetCoreNode = $systemWebServer.aspNetCore
    if (-not $aspNetCoreNode) {
        throw "web.config invalide : noeud aspNetCore introuvable."
    }

    $environmentVariables = $aspNetCoreNode.environmentVariables
    if (-not $environmentVariables) {
        $environmentVariables = $webConfig.CreateElement("environmentVariables")
        $aspNetCoreNode.AppendChild($environmentVariables) | Out-Null
    }

    $existing = $environmentVariables.SelectSingleNode("environmentVariable[@name='ASPNETCORE_ENVIRONMENT']")
    if ($existing) {
        $existing.SetAttribute("value", $AspNetCoreEnvironment)
    }
    else {
        $newNode = $webConfig.CreateElement("environmentVariable")
        $newNode.SetAttribute("name", "ASPNETCORE_ENVIRONMENT")
        $newNode.SetAttribute("value", $AspNetCoreEnvironment)
        $environmentVariables.AppendChild($newNode) | Out-Null
    }

    $webConfig.Save($WebConfigPath)

    Write-Host "ASPNETCORE_ENVIRONMENT = $AspNetCoreEnvironment"
}

function Grant-AppPoolPermissions {
    param(
        [string]$DeployPath,
        [string]$AppPoolName
    )

    Write-Step "Droits AppPool sur dossiers locaux"

    $logsPath = Join-Path $DeployPath "logs"
    $dataPath = Join-Path $DeployPath "data"

    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
    New-Item -ItemType Directory -Path $dataPath -Force | Out-Null

    $appPoolIdentity = "IIS AppPool\${AppPoolName}"

    icacls $logsPath /grant "${appPoolIdentity}:(OI)(CI)M" /T | Out-Host
    icacls $dataPath /grant "${appPoolIdentity}:(OI)(CI)M" /T | Out-Host
}

function Test-HttpEndpoint {
    param(
        [string]$Url,
        [switch]$Required
    )

    Write-Host ""
    Write-Host "Test HTTP : $Url" -ForegroundColor Yellow

    curl.exe -i $Url
    $code = $LASTEXITCODE

    if ($Required -and $code -ne 0) {
        throw "Test HTTP obligatoire échoué : $Url"
    }

    if ($code -ne 0) {
        Write-Host "Test HTTP non bloquant échoué : $Url" -ForegroundColor Yellow
    }
}

function Test-Deployment {
    param(
        [string]$AppPoolName
    )

    Write-Step "Tests de vérification"

    Get-Website -Name $SiteName | Format-Table -AutoSize
    Get-WebAppPoolState -Name $AppPoolName | Format-Table -AutoSize

    Test-HttpEndpoint -Url $HealthUrl -Required
    Test-HttpEndpoint -Url $ExpeditionPreparationsUrl
}

# =========================
# Exécution principale
# =========================

Write-Step "Mise à jour API MobileSLI IIS"

Import-IisModuleOrStop

$deploymentInfo = Get-IisDeploymentInfo
$AppPoolName = $deploymentInfo.AppPoolName
$DeployPath = $deploymentInfo.DeployPath

Write-Host "Site IIS       : $SiteName"
Write-Host "AppPool        : $AppPoolName"
Write-Host "Chemin IIS     : $DeployPath"
Write-Host "Dépôt Git      : $SourcePath"
Write-Host "Projet         : $ProjectPath"
Write-Host "Publish        : $PublishPath"
Write-Host "Backup         : $BackupRoot"
Write-Host "Port local     : $LocalPort"

Test-DotNetEnvironment
Update-GitAndPublish

Stop-IisApplication -AppPoolName $AppPoolName
Backup-CurrentDeployment -DeployPath $DeployPath
Deploy-PublishedFiles -DeployPath $DeployPath
Ensure-WebConfigEnvironment -WebConfigPath (Join-Path $DeployPath "web.config")
Grant-AppPoolPermissions -DeployPath $DeployPath -AppPoolName $AppPoolName
Start-IisApplication -AppPoolName $AppPoolName
Test-Deployment -AppPoolName $AppPoolName

Write-Step "Mise à jour API terminée"
Write-Host "URL santé API     : $HealthUrl" -ForegroundColor Green
Write-Host "URL Expédition    : $ExpeditionPreparationsUrl" -ForegroundColor Green
Write-Host "Site IIS          : $SiteName" -ForegroundColor Green
Write-Host "Dossier déployé   : $DeployPath" -ForegroundColor Green
