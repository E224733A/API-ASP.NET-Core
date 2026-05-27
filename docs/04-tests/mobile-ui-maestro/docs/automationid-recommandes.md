# AutomationId recommandés pour fiabiliser les tests Maestro

Les flows `flows-texte` fonctionnent avec les textes visibles, mais ils sont fragiles.

Pour des tests UI fiables, il est recommandé d'ajouter des `AutomationId` dans les pages MAUI. Ces identifiants ne changent pas l'affichage, mais ils donnent aux tests une cible stable.

## Pages

| Page | Élément | AutomationId recommandé |
|---|---|---|
| Confirmation tournée | page racine | `page_confirmation_tournee` |
| Liste des points | page racine | `page_points_livraison` |
| Détail point | page racine | `page_detail_point` |

## Confirmation tournée

| Élément | AutomationId recommandé | Objectif de test |
|---|---|---|
| Bouton Charger la tournée | `btn_charger_tournee` | Déclencher le contrôle de tournée locale existante. |
| Panneau tournée non synchronisée | `panel_tournee_non_synchronisee` | Vérifier que l'avertissement est visible. |
| Bouton Reprendre | `btn_reprendre_tournee` | Reprendre la tournée locale. |
| Bouton Abandonner et charger | `btn_abandonner_tournee` | Vérifier le scénario de remplacement. |
| Bouton Retour liste | `btn_retour_liste_tournees` | Revenir sans action. |

## Liste des points

| Élément | AutomationId recommandé | Objectif de test |
|---|---|---|
| Bouton ouvrir premier point | `btn_ouvrir_point_0` | Ouvrir un point stable pour les tests. |
| Filtre Tous | `btn_filtre_tous` | Réinitialiser l'affichage. |
| Filtre À faire | `btn_filtre_a_faire` | Cibler les points non validés. |
| Bouton Récapitulatif | `btn_recapitulatif` | Vérifier le parcours de fin. |

## Détail point

| Élément | AutomationId recommandé | Objectif de test |
|---|---|---|
| Ligne article ROLLS_VIDES | `row_article_rolls_vides` | Vérifier que l'article est affiché. |
| Champ livré ROLLS_VIDES | `input_rolls_vides_livre` | Saisir une quantité livrée. |
| Champ récupéré ROLLS_VIDES | `input_rolls_vides_recupere` | Saisir une quantité récupérée. |
| Bouton Fait | `btn_statut_fait` | Passer le point en statut fait. |
| Bouton Non fait | `btn_statut_non_fait` | Tester le commentaire obligatoire. |
| Bouton Anomalie | `btn_statut_anomalie` | Tester le commentaire obligatoire. |
| Statut sélectionné | `label_statut_selectionne` | Vérifier le statut actif. |
| Commentaire livreur | `input_commentaire_livreur` | Saisir un commentaire. |
| Bouton Valider passage | `btn_valider_passage` | Valider le point. |
| Bouton Retour détail | `btn_retour_detail_point` | Revenir à la liste. |

## Exemple XAML

```xml
<Button
    AutomationId="btn_reprendre_tournee"
    Text="Reprendre la tournée"
    Command="{Binding ReprendreTourneeCommand}" />
```

## Règle de nommage

```text
page_nom_ecran    pour les pages ou conteneurs principaux
btn_action      pour les boutons
input_champ    pour les champs de saisie
label_info     pour les textes de contrôle
row_element    pour les lignes ou cartes de liste
panel_info     pour les panneaux d'information ou d'alerte
```

## Attention avec CollectionView

Dans une `CollectionView`, les éléments sont répétés. Il faut éviter de donner le même `AutomationId` à plusieurs boutons visibles en même temps.

Pour le test simple, il suffit d'ajouter un identifiant au premier point de test, ou de garder une variante texte avec `tapOn: "Ouvrir"`.
