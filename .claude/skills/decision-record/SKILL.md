---
name: decision-record
description: À utiliser quand un choix structurant est fait ou proposé — architecture, propriétaire de l'attribution, politique de récompense pub, version Unity/SDK, suppression d'une fonction du sample, nom du package — pour le consigner dans Doc/decisions/.
---

# Note de décision

Fichier : `Doc/decisions/NNNN-titre-court.md` (numéro suivant le plus grand existant). Court : une page maximum.

```markdown
# NNNN — Titre

Date : AAAA-MM-JJ · Statut : proposée | acceptée | remplacée par NNNN

## Problème
Ce qui oblige à choisir, en 2-4 phrases.

## Décision
Le choix retenu.

## Alternatives écartées
- Option — pourquoi non.

## Conséquences
Ce que ça implique (code, tests, exploitation, limites connues).
```

Une décision non validée par le propriétaire reste au statut `proposée`. Ne pas réécrire une décision acceptée : en créer une nouvelle qui la remplace.
