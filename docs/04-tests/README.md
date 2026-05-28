# 04 - Tests API

Ce dossier contient les tests manuels et semi-automatiques de l'API.

## Dossiers

| Dossier | Rôle |
|---|---|
| `Expedition` | Tests des routes Expédition |
| `Mobile` | Tests des routes mobile |

## Règle

Les tests GET ne modifient pas la base.

Les tests POST peuvent écrire en base de développement.

## Lancement recommandé

1. Lancer l'API.
2. Exécuter les tests GET.
3. Exécuter les tests POST uniquement sur une base de développement.
4. Vérifier les données SQL après les POST.


Test de base : 
Les tests API Mobile v1.2 ont été exécutés le 27/05/2026 contre l’API centrale http://192.168.1.233:5000.
Les 18 scénarios automatisés sont passés avec succès : 18 OK, 0 KO, 0 ignoré.
Les tests couvrent les synchronisations valides, les doublons techniques, les doublons métier, les validations de quantités, les statuts invalides, les lignes non validées, les articles dupliqués, la version de schéma et le cas métier ROLLS_VIDES.

Test de masse : 
Le test de masse de 20 synchronisations mobiles est validé.
L’API a accepté 20/20 synchronisations, sans erreur 400, 409 ou 500.
La vérification SQL confirme l’enregistrement des volumes attendus en base.
Le seuil de performance k6 a signalé un dépassement sur le p95, mais cela ne remet pas en cause la réussite fonctionnelle du test.

Le test de masse de 50 synchronisations a été exécuté avec 10 utilisateurs virtuels k6. Les 50 synchronisations valides ont été acceptées par l’API avec un code HTTP 200. Aucune erreur serveur, aucun conflit et aucune réponse inattendue n’ont été observés. Le test est donc validé fonctionnellement. Le p95 HTTP mesuré est de 2810 ms, supérieur au seuil indicatif de 2000 ms, ce qui constitue un point de performance à surveiller mais ne remet pas en cause la validité fonctionnelle du traitement.
