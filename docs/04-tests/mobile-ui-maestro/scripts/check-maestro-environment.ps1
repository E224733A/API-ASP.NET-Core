param(
    [string]$AppId = "fr.sli.mobiletournee"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Vérification environnement Maestro / Android ===" -ForegroundColor Cyan
Write-Host "Application cible : $AppId"
Write-Host ""

function Test-CommandAvailable {
    param([string]$Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    return $null -ne $command
}

if (-not (Test-CommandAvailable -Name "adb")) {
    Write-Host "ERREUR : adb n'est pas disponible dans le PATH." -ForegroundColor Red
    Write-Host "Installez Android Platform Tools ou ouvrez un terminal où adb est disponible."
    exit 1
}

if (-not (Test-CommandAvailable -Name "maestro")) {
    Write-Host "ERREUR : maestro n'est pas disponible dans le PATH." -ForegroundColor Red
    Write-Host "Installez Maestro puis relancez ce script."
    exit 1
}

Write-Host "Version Maestro :" -ForegroundColor Yellow
maestro --version
Write-Host ""

Write-Host "Appareils Android détectés :" -ForegroundColor Yellow
$adbDevices = adb devices
$adbDevices | ForEach-Object { Write-Host $_ }
Write-Host ""

$deviceLines = $adbDevices | Where-Object { $_ -match "\tdevice$" }
if ($deviceLines.Count -eq 0) {
    Write-Host "ERREUR : aucun appareil Android prêt n'est détecté." -ForegroundColor Red
    Write-Host "Vérifiez le débogage USB, l'autorisation RSA ou l'émulateur Android."
    exit 1
}

Write-Host "Applications installées correspondant à $AppId :" -ForegroundColor Yellow
$packages = adb shell pm list packages $AppId
$packages | ForEach-Object { Write-Host $_ }

if (-not ($packages -match [regex]::Escape($AppId))) {
    Write-Host "ATTENTION : l'application $AppId ne semble pas installée sur l'appareil actif." -ForegroundColor Yellow
    Write-Host "Installez l'APK ou déployez depuis Visual Studio avant de lancer les flows."
    exit 2
}

Write-Host ""
Write-Host "Environnement prêt pour les tests Maestro." -ForegroundColor Green
