# JSON de test Expédition

Ce dossier contient des exemples JSON pour tester les routes Expédition.

## Fichiers

| Fichier | Rôle |
|---|---|
| `get-a-preparer-exemple-reponse.json` | Exemple de réponse GET |
| `get-a-preparer-filtres-interdits.json` | Documentation des filtres interdits |
| `post-verrouiller-valide.json` | Exemple de POST valide |
| `post-verrouiller-article-interdit-rolls-vides.json` | Exemple invalide avec `ROLLS_VIDES` |
| `post-verrouiller-quantite-negative.json` | Exemple invalide avec quantité négative |
| `post-verrouiller-sans-id-lot.json` | Exemple invalide sans `idLotVerrouillage` |

## Attention

Les fichiers JSON sont des exemples lisibles.

Le script PowerShell `Run-ExpeditionTests.ps1` construit aussi un payload dynamique à partir du GET réel pour les tests POST, afin de mieux correspondre aux lignes actuellement préparables dans la base.
