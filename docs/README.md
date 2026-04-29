@'
# Documentation projet mobile tournée

Ce dossier contient les documents de référence du projet.

## 00-cadrage

Cahier des charges général du projet :
- besoin métier
- architecture globale
- fonctionnement mobile
- règles métier
- hébergement prévu
- exigences de documentation et de tests

## 01-api

Documents liés à l’API ASP.NET Core :
- contrats JSON
- routes principales
- règles de validation
- synchronisation mobile vers SQL Server

## 02-base-donnees

Documents liés à SQL Server :
- tables Mobile_*
- contraintes SQL
- index
- logs
- stockage des synchronisations

## 03-deploiement

Documents liés à l’installation :
- VM Windows
- IIS
- .NET Hosting Bundle
- configuration réseau
- déploiement de l’API

## 04-tests

Notes et scénarios de tests à compléter.

## 05-codex

Notes spécifiques pour guider Codex et éviter les modifications incohérentes.
'@ | Set-Content docs\README.md -Encoding UTF8