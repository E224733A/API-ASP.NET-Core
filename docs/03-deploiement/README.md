# 03 - Déploiement et exécution

Ce dossier contient les procédures pour lancer, tester et déployer l'API.

## Documents

| Fichier | Rôle |
|---|---|
| `lancement-local.md` | Lancer l'API en développement |
| `test-mobile-ngrok.md` | Tester l'API depuis un téléphone via ngrok |
| `test-mobile-adb.md` | Tester l'API depuis un téléphone via ADB reverse |
| `deploiement-iis.md` | Publier l'API sur IIS |

## Ports de développement

| Service | URL |
|---|---|
| API locale | `http://127.0.0.1:5000` |
| Swagger | `http://127.0.0.1:5000/swagger` |
| Web Expédition local | `http://127.0.0.1:5100` |

## Règle de sécurité

Les secrets ne doivent jamais être stockés dans Git.

En développement, utiliser :

```text
dotnet user-secrets
variables d'environnement
fichiers locaux non versionnés
```

En production, utiliser une configuration serveur sécurisée.
