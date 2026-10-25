# Matrice des dépendances

Source de vérité : `UnityProject/Packages/manifest.json` et `packages-lock.json`, `UnityProject/GemHunterUGSCloud/Project/GemHunterUGSCloud.csproj`. Ce tableau résume les composants qui comptent pour le socle.

| Composant | Version | Raison | Vérifié le | Source |
| --- | --- | --- | --- | --- |
| Unity Editor | 6000.6.7f1 | Choix du propriétaire (décision 0005) ; non LTS | 2026-10-24 | `ProjectVersion.txt` |
| Unity IAP (`com.unity.purchasing`) | 5.4.4 | API v5 ; l'ancienne API reste disponible mais obsolète. Fournit `SetObfuscatedAccountId` (Google), `SetAppAccountToken` (Apple) et `RemoteCatalogProvider` (Remote Catalog, non utilisé : arbitrage A3). Pas de validation distante intégrée pour les stores natifs | 2026-10-25 | `manifest.json`, code du package, docs IAP |
| LevelPlay (`com.unity.services.levelplay`) | 9.5.1 | Résolue par l'éditeur ; API `Init(appKey, userId)` | 2026-10-24 | `manifest.json`, code du package |
| Services Core | 1.18.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Authentication | 3.8.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Cloud Code (client) | 2.10.4 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Economy (client) | 3.5.4 | Résolue par l'éditeur. **Inutilisable sur le projet UGS du template** (Economy fermé aux nouveaux projets depuis le 2026-09-08, 403 au déploiement) ; retrait proposé (décision 0008, à valider) | 2026-10-25 | `manifest.json`, docs.unity.com/en-us/economy |
| Analytics | 6.3.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Cloud Save | 3.4.0 | Résolue par l'éditeur (hors chemin obligatoire du template) | 2026-10-24 | `manifest.json` |
| Remote Config | 4.2.5 | Résolue par l'éditeur (optionnel) | 2026-10-24 | `manifest.json` |
| Cloud Diagnostics | 1.0.10 | Inchangée ; inutilisée par les scripts, statut à vérifier | 2026-10-24 | `manifest.json` |
| Leaderboards | 2.3.4 | 2.3.3 ne compile pas en 6000.4+ ; hors périmètre du template | 2026-10-24 | changelog du package |
| Friends | 1.3.0 | Résolue par l'éditeur ; hors périmètre du template | 2026-10-24 | `manifest.json` |
| URP | 17.6.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Backend Cloud Code et bibliothèque `DevilCorp.MobileFoundation.Server` | net8.0, `Com.Unity.Services.CloudCode.Apis` 0.0.16 (`Core` 0.0.1) | Passé de net6.0 (hors support Microsoft depuis 2024-11) le 2026-10-25. **Apis reste en 0.0.16** : la 0.0.27 impose `Core` 0.0.7, que le générateur de bindings du package Cloud Code 2.10.4 refuse (il embarque sa propre `Core`, échec `FileLoadException` le 2026-10-25). Tout ce que le portefeuille utilise existe en 0.0.16 (`SetProtectedItemBatchAsync`, writeLock). Les appels Cloud Save protected exigent `context.ServiceToken` (jeton joueur → 401, spike du 2026-10-25). Plafond .NET 9 sur ce chemin ; net8 et net9 hors support le 2026-11-10. Cloud Code 3.0 (2026-10-23) cible .NET 10 mais exige Unity 6000.7, en bêta ; la page des modules de la doc 3.0.0 dit encore « up to version 9 ». Montée d’Apis à refaire avec la migration Unity 6.7 / Cloud Code 3.0 | 2026-10-25 | `.csproj`, changelog Cloud Code 3.0, réflexion sur les DLL, génération de bindings |
| Tests backend | net10.0, xUnit 2.9.2, NSubstitute 5.1.0 | Seul runtime .NET installé localement | 2026-10-24 | `Tests/GemHunterUGSCloud.Tests.csproj` |
| Package commun `com.devilcorp.mobilefoundation` | 0.2.0 | Modules Achats (décision 0006) et Pubs (décision 0007) ; dépend de Unity IAP 5.4.4 ; LevelPlay ≥ 9.0.0 optionnel (adaptateur compilé seulement s'il est installé) | 2026-10-25 | `SharedPackages/com.devilcorp.mobilefoundation/package.json`, `README.md` |
| Tests du cœur Achats | net10.0, C# 9, xUnit 2.9.2 | Même code source que le package, limité au C# supporté par Unity | 2026-10-24 | `SharedPackages/Tests/MobileFoundation.Purchases.Tests` |

## Outillage de déploiement et de pilotage

| Outil | Version | Rôle | Vérifié le |
| --- | --- | --- | --- |
| Unity CLI | 1.0.0-beta.14 | Éditeurs, projets, builds ; pilotage de l'éditeur ouvert via le package Pipeline (`unity command …`). Ne déploie pas de ressources UGS et ne gère pas les environnements | 2026-10-25 |
| Fenêtre Deployment (`com.unity.services.deployment` 1.7.2, API 1.1.3) | — | Déploie depuis l'éditeur le module Cloud Code (`.ccmr`) et les fichiers de config (`.rc`, `.ec*`…) vers l'environnement actif | 2026-10-25 |
| UGS CLI | non installée | Déploiement et gestion des environnements hors éditeur ; exige un compte de service | 2026-10-25 |
| Serveur Cloud Code local | non utilisé | Exécution locale des modules ; ne prouve pas les politiques du service distant | 2026-10-25 |

À vérifier par un build : adaptateurs réseaux LevelPlay, Android minSdk/targetSdk exigés par Google Play, iOS minimum et version de Xcode.
