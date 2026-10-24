---
name: analytics-event
description: À utiliser pour ajouter, modifier ou supprimer un événement analytics (Unity Analytics / IAnalyticsService), ou pour mettre à jour le dictionnaire d'événements.
---

# Événement analytics

Référence : section 10 du brief. Dictionnaire versionné : `Doc/analytics-events.md` (le créer au premier événement).

## Avant d'ajouter

- Vérifier qu'un événement automatique du SDK ou un événement existant ne couvre pas déjà le besoin.
- Événement commun (achat, pub, run) → package socle. Événement de gameplay → code du jeu, sans toucher au cœur du package.

## Règles

- Nom et propriétés en `snake_case` (`purchase_completed`, `product_id`).
- Aucune donnée personnelle : pas d'email, nom, reçu, token, identifiant publicitaire.
- Émis seulement si le consentement analytics le permet ; l'absence de consentement ou une panne SDK ne bloque rien.
- L'analytics n'est jamais une preuve d'achat ni une source d'attribution. `purchase_completed` = transaction validée **et** livrée par le serveur, définition unique.
- Éviter les doublons : un événement par fait métier (pas à chaque retry ni à chaque affichage).
- Les builds de test envoient vers l'environnement de test, pas la production.
- Un événement custom doit aussi être déclaré côté tableau de bord Unity Analytics (schéma) : le signaler au propriétaire.

## Entrée du dictionnaire

```markdown
### purchase_completed (v1)
- Signification : achat validé et livré par le backend
- Source : client, après réponse succès du backend
- Moment : une fois par transaction livrée (pas sur restauration)
- Propriétés : product_id (string), store (string), is_restore (bool)
- Consentement : analytics
```
