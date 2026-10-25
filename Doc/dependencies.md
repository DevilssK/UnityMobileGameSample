# Matrice des dépendances

Source de vérité : `UnityProject/Packages/manifest.json` et `packages-lock.json`, `UnityProject/GemHunterUGSCloud/Project/GemHunterUGSCloud.csproj`. Ce tableau résume les composants qui comptent pour le socle.

| Composant | Version | Raison | Vérifié le | Source |
| --- | --- | --- | --- | --- |
| Unity Editor | 6000.6.7f1 | Choix du propriétaire (décision 0005) ; non LTS | 2026-10-24 | `ProjectVersion.txt` |
| Unity IAP (`com.unity.purchasing`) | 5.4.4 | API v5 ; l'ancienne API reste disponible mais obsolète | 2026-10-24 | `manifest.json` |
| LevelPlay (`com.unity.services.levelplay`) | 9.5.1 | Résolue par l'éditeur ; API `Init(appKey, userId)` | 2026-10-24 | `manifest.json`, code du package |
| Services Core | 1.18.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Authentication | 3.8.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Cloud Code (client) | 2.10.4 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Economy (client) | 3.5.4 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Analytics | 6.3.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Cloud Save | 3.4.0 | Résolue par l'éditeur (hors chemin obligatoire du template) | 2026-10-24 | `manifest.json` |
| Remote Config | 4.2.5 | Résolue par l'éditeur (optionnel) | 2026-10-24 | `manifest.json` |
| Cloud Diagnostics | 1.0.10 | Inchangée ; inutilisée par les scripts, statut à vérifier | 2026-10-24 | `manifest.json` |
| Leaderboards | 2.3.4 | 2.3.3 ne compile pas en 6000.4+ ; hors périmètre du template | 2026-10-24 | changelog du package |
| Friends | 1.3.0 | Résolue par l'éditeur ; hors périmètre du template | 2026-10-24 | `manifest.json` |
| URP | 17.6.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Backend Cloud Code | net6.0, `Com.Unity.Services.CloudCode.Apis` 0.0.16 | Inchangé ; montée vers net8.0 à faire (runtime Cloud Code ≤ .NET 9) | 2026-10-24 | `.csproj`, doc Cloud Code |
| Tests backend | net10.0, xUnit 2.9.2, NSubstitute 5.1.0 | Seul runtime .NET installé localement | 2026-10-24 | `Tests/GemHunterUGSCloud.Tests.csproj` |
| Package commun `com.devilcorp.mobilefoundation` | 0.1.0 | Module Achats (décision 0006) ; dépend de Unity IAP 5.4.4 | 2026-10-24 | `SharedPackages/com.devilcorp.mobilefoundation/package.json` |
| Tests du cœur Achats | net10.0, C# 9, xUnit 2.9.2 | Même code source que le package, limité au C# supporté par Unity | 2026-10-24 | `SharedPackages/Tests/MobileFoundation.Purchases.Tests` |

À vérifier par un build : adaptateurs réseaux LevelPlay, Android minSdk/targetSdk exigés par Google Play, iOS minimum et version de Xcode.
