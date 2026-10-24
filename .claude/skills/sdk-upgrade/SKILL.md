---
name: sdk-upgrade
description: À utiliser pour changer la version de l'éditeur Unity, d'un package UPM (Unity IAP, LevelPlay, Analytics, Authentication, Cloud Code, Economy...), d'un SDK natif ou du framework du backend, ou pour figer la matrice de versions.
---

# Montée de version Unity / SDK

## Règles

- Une montée de version = un commit dédié, séparé de tout refactoring fonctionnel.
- Ne jamais traiter un numéro de version du brief comme une recommandation : vérifier la dernière version stable et ses notes de version (context7, doc Unity, changelog du package).
- Vérifier la compatibilité croisée : version Unity ↔ packages UGS ↔ LevelPlay ↔ adaptateurs réseaux ↔ Android minSdk/targetSdk exigé par le store ↔ Xcode/iOS minimum.
- Lire les guides de migration (breaking changes). Ex. Unity IAP 4 → 5 change l'API d'achat : adapter le code, ne pas supposer la parité.
- Conserver `Packages/packages-lock.json` dans le commit.

## Étapes

1. Relever l'état actuel : `UnityProject/ProjectSettings/ProjectVersion.txt`, `UnityProject/Packages/manifest.json`, versions des SDK natifs.
2. Lister cible et raisons dans une note `decision-record`.
3. Mettre à jour (manifest ou demander au propriétaire d'ouvrir le projet avec la nouvelle version via Unity Hub — l'upgrade d'éditeur se fait par l'éditeur, pas en éditant `ProjectVersion.txt`).
4. Compiler et tester avec `run-checks` ; corriger les API obsolètes.
5. Signaler ce qui reste à vérifier : build Android, build iOS, appareil réel, sandbox.

## Matrice

Tenir à jour `Doc/dependencies.md` : composant, version, raison, date de vérification, source.
