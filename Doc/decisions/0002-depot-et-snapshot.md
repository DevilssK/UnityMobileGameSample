# 0002 — Dépôt : nouveau repo git initialisé par un snapshot du sample

Date : 2026-10-24 · Statut : acceptée

## Problème
Le brief demande de préserver la traçabilité du sample et un point de retour Git. Le dépôt amont est un monorepo contenant de nombreux autres samples ; le dossier de travail n'était pas un dépôt git.

## Décision
Nouveau dépôt git dans `UnityMobileGameSample/`. Premier commit = import tel quel de `GemHunterMatch3/` au commit amont `575661c6f83e721e9f20d27f6c66e7249a929889` dans `UnityProject/`, avec `LICENSE.md` (Unity Companion License). Ce commit est tagué `upstream-575661c6` et sert de référence avant toute suppression.

## Alternatives écartées
- Fork du monorepo — historique complet mais embarque tous les autres samples.

## Conséquences
- L'historique amont n'est conservé que par référence (URL + commit) ; un diff avec une version amont future se fait par comparaison de snapshot.
- Le backend reste dans `UnityProject/GemHunterUGSCloud/` (voir 0004).
