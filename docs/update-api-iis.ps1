#requires -RunAsAdministrator
<#
MISE A JOUR API MOBILESLI IIS
Version finale DNS : les tests post-deploiement utilisent SRVAPI1.SLI.local.
#>
$ErrorActionPreference = "Stop"
$SiteName = "MobileSLI.Api"
$SourcePath = "C:\Sources\API-ASP.NET-Core"
$ProjectPath = "C:\Sources\API-ASP.NET-Core\API-ASP.NET-Core.csproj"
$PublishPath = "C:\Publish\MobileSLI.Api"
$BackupRoot = "C:\Backups\MobileSLI.Api"
$ApiDns = "SRVAPI1.SLI.local"
$ApiPort = 5000
$HealthUrl = "http://${ApiDns}:$ApiPort/api/health"
$ExpeditionPreparationsUrl = "http://${ApiDns}:$ApiPort/api/expedition/preparations/a-preparer"
$AspNetCoreEnvironment = ""
$RobocopyExcludedDirectories = @("logs")
$RobocopyExcludedFiles = @("*.log", "appsettings.Production.json")
function Write-Step { param([string]$Message) Write-Host ""; Write-Host "=== $Message ===" -ForegroundColor Cyan }
function Assert-PathExists { param([string]$Path,[string]$Message) if (-not (Test-Path $Path)) { throw "$Message : $Path" } }
function Invoke-RobocopyChecked { param([string]$Source,[string]$Destination,[string]$Description)
    Assert-PathExists $Source "Source robocopy introuvable"
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $args = @($Source, $Destination, "/MIR")
    if ($RobocopyExcludedDirectories.Count -gt 0) { $args += "/XD"; $args += $RobocopyExcludedDirectories }
    if ($RobocopyExcludedFiles.Count -gt 0) { $args += "/XF"; $args += $RobocopyExcludedFiles }
    & robocopy @args
    if ($LASTEXITCODE -gt 7) { throw "$Description échoué. Code robocopy : $LASTEXITCODE" }
}
function Import-IisModuleOrStop { Write-Step "Chargement du module IIS WebAdministration"; try { Import-Module WebAdministration -ErrorAction Stop } catch { throw "WebAdministration indisponible : $($_.Exception.Message)" } }
function Get-IisDeploymentInfo {
    Write-Step "Lecture de la configuration IIS"
    $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
    if (-not $site) { throw "Site IIS introuvable : $SiteName" }
    $appPoolName = $site.ApplicationPool
    $deployPath = [Environment]::ExpandEnvironmentVariables($site.PhysicalPath)
    return [PSCustomObject]@{ Site = $site; AppPoolName = $appPoolName; DeployPath = $deployPath }
}
function Test-DotNetEnvironment { Write-Step "Vérification .NET"; Assert-PathExists $ProjectPath "Projet API introuvable"; dotnet --list-sdks }
function Update-GitAndPublish {
    Write-Step "Mise à jour Git et publication Release"
    Assert-PathExists $SourcePath "Dossier source introuvable"
    Set-Location $SourcePath
    git status; if ($LASTEXITCODE -ne 0) { throw "git status a échoué." }
    git pull; if ($LASTEXITCODE -ne 0) { throw "git pull a échoué." }
    dotnet restore $ProjectPath; if ($LASTEXITCODE -ne 0) { throw "dotnet restore a échoué." }
    dotnet build $ProjectPath -c Release --no-restore; if ($LASTEXITCODE -ne 0) { throw "dotnet build a échoué." }
    Remove-Item $PublishPath -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $PublishPath -Force | Out-Null
    dotnet publish $ProjectPath -c Release -o $PublishPath --no-build; if ($LASTEXITCODE -ne 0) { throw "dotnet publish a échoué." }
}
function Stop-IisApplication { param([string]$AppPoolName)
    Write-Step "Arrêt IIS"
    $site = Get-Website -Name $SiteName -ErrorAction Stop
    if ($site.State -eq "Started") { Stop-Website -Name $SiteName }
    if ((Get-WebAppPoolState -Name $AppPoolName -ErrorAction Stop).Value -eq "Started") { Stop-WebAppPool -Name $AppPoolName }
    Start-Sleep -Seconds 3
}
function Start-IisApplication { param([string]$AppPoolName)
    Write-Step "Démarrage IIS"
    if ((Get-WebAppPoolState -Name $AppPoolName -ErrorAction Stop).Value -ne "Started") { Start-WebAppPool -Name $AppPoolName }
    if ((Get-Website -Name $SiteName -ErrorAction Stop).State -ne "Started") { Start-Website -Name $SiteName }
    Start-Sleep -Seconds 5
}
function Backup-CurrentDeployment { param([string]$DeployPath)
    Write-Step "Sauvegarde de la version actuelle"
    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
    $backupPath = Join-Path $BackupRoot (Get-Date -Format "yyyyMMdd-HHmmss")
    Invoke-RobocopyChecked -Source $DeployPath -Destination $backupPath -Description "Sauvegarde"
}
function Deploy-PublishedFiles { param([string]$DeployPath) Write-Step "Copie de la nouvelle version"; Invoke-RobocopyChecked -Source $PublishPath -Destination $DeployPath -Description "Déploiement" }
function Ensure-WebConfigEnvironment { param([string]$WebConfigPath)
    if ([string]::IsNullOrWhiteSpace($AspNetCoreEnvironment)) { Write-Host "ASPNETCORE_ENVIRONMENT non modifié." -ForegroundColor Yellow; return }
    [xml]$webConfig = Get-Content $WebConfigPath
    $systemWebServer = $webConfig.configuration.'system.webServer'
    if (-not $systemWebServer -and $webConfig.configuration.location) { $systemWebServer = $webConfig.configuration.location.'system.webServer' }
    $aspNetCoreNode = $systemWebServer.aspNetCore
    $environmentVariables = $aspNetCoreNode.environmentVariables
    if (-not $environmentVariables) { $environmentVariables = $webConfig.CreateElement("environmentVariables"); $aspNetCoreNode.AppendChild($environmentVariables) | Out-Null }
    $existing = $environmentVariables.SelectSingleNode("environmentVariable[@name='ASPNETCORE_ENVIRONMENT']")
    if ($existing) { $existing.SetAttribute("value", $AspNetCoreEnvironment) } else { $newNode = $webConfig.CreateElement("environmentVariable"); $newNode.SetAttribute("name", "ASPNETCORE_ENVIRONMENT"); $newNode.SetAttribute("value", $AspNetCoreEnvironment); $environmentVariables.AppendChild($newNode) | Out-Null }
    $webConfig.Save($WebConfigPath)
}
function Grant-AppPoolPermissions { param([string]$DeployPath,[string]$AppPoolName)
    $logsPath = Join-Path $DeployPath "logs"; $dataPath = Join-Path $DeployPath "data"
    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null; New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
    $appPoolIdentity = "IIS AppPool\${AppPoolName}"
    icacls $logsPath /grant "${appPoolIdentity}:(OI)(CI)M" /T | Out-Host
    icacls $dataPath /grant "${appPoolIdentity}:(OI)(CI)M" /T | Out-Host
}
function Test-HttpEndpoint { param([string]$Url,[switch]$Required)
    Write-Host ""; Write-Host "Test HTTP : $Url" -ForegroundColor Yellow; curl.exe -i $Url; if ($Required -and $LASTEXITCODE -ne 0) { throw "Test HTTP obligatoire échoué : $Url" }
}
function Test-Deployment { param([string]$AppPoolName)
    Write-Step "Tests de vérification"; Get-Website -Name $SiteName | Format-Table -AutoSize; Get-WebAppPoolState -Name $AppPoolName | Format-Table -AutoSize; Test-HttpEndpoint -Url $HealthUrl -Required; Test-HttpEndpoint -Url $ExpeditionPreparationsUrl
}
Write-Step "Mise à jour API MobileSLI IIS"
Import-IisModuleOrStop
$deploymentInfo = Get-IisDeploymentInfo
$AppPoolName = $deploymentInfo.AppPoolName
$DeployPath = $deploymentInfo.DeployPath
Write-Host "Site IIS       : $SiteName"
Write-Host "AppPool        : $AppPoolName"
Write-Host "Chemin IIS     : $DeployPath"
Write-Host "DNS API        : $ApiDns"
Write-Host "Port API       : $ApiPort"
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
