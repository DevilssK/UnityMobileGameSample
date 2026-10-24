# 0004 — Le backend reste dans le projet Unity

Date : 2026-10-24 · Statut : acceptée

## Problème
Le `CLAUDE.md` prévoyait `Backend/` à la racine ; le brief (§6.1) conserve `GemHunterUGSCloud/` dans le projet au début. Le fichier `Assets/GemHunterUGS/Scripts/GemHunterUGSCloud.ccmr` référence la solution par chemin relatif et sert à générer les bindings.

## Décision
Le backend reste dans `UnityProject/GemHunterUGSCloud/` pour l'instant. Un déplacement éventuel fera l'objet d'une décision et d'un commit dédiés (mise à jour du `.ccmr` et des bindings).

## Alternatives écartées
- `Backend/` à la racine dès maintenant — déplacement sans bénéfice immédiat, risque sur la génération des bindings.

## Conséquences
- `CLAUDE.md` mis à jour en conséquence.
