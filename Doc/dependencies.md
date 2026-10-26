# Matrice des dépendances

Source de vérité : `UnityProject/Packages/manifest.json` et `packages-lock.json`, `UnityProject/GemHunterUGSCloud/Project/GemHunterUGSCloud.csproj`. Ce tableau résume les composants qui comptent pour le socle.

| Composant | Version | Raison | Vérifié le | Source |
| --- | --- | --- | --- | --- |
| Unity Editor | 6000.6.7f1 | Choix du propriétaire (décision 0005) ; non LTS | 2026-10-24 | `ProjectVersion.txt` |
| Unity IAP (`com.unity.purchasing`) | 5.4.4 | API v5 ; l'ancienne API reste disponible mais obsolète. Fournit `SetObfuscatedAccountId` (Google), `SetAppAccountToken` (Apple) et `RemoteCatalogProvider` (Remote Catalog, non utilisé : arbitrage A3). Pas de validation distante intégrée pour les stores natifs | 2026-10-25 | `manifest.json`, code du package, docs IAP |
| LevelPlay (`com.unity.services.levelplay`) | 9.5.1 | Résolue par l'éditeur ; API `Init(appKey, userId)` | 2026-10-24 | `manifest.json`, code du package |
| SDK natif LevelPlay et adaptateur Unity Ads | SDK 9.7.0 (Android et iOS) ; adaptateur 5.15.0 (Android), 5.13.0 (iOS) | Le package 9.5.1 exige un SDK natif dans `[9.5.0, 10.0[` (`LevelPlayVersions.json` du package) ; le sample figeait 8.9.1, et le build Android échouait sur des API absentes (`LevelPlayPrivacySettings`). Versions choisies par le LevelPlay Network Manager le 2026-10-26 dans `Assets/LevelPlay/Editor/*Dependencies.xml`. Le même passage retire de `mainTemplate.gradle` les dépendances Facebook et Google Play Games, dont les SDK sont absents du projet | 2026-10-26 | Network Manager, changelog officiel LevelPlay Unity |
| Services Core | 1.18.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Authentication | 3.8.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Cloud Code (client) | 2.10.4 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Analytics | 6.3.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Cloud Save | 3.4.0 | Résolue par l'éditeur (hors chemin obligatoire du template) | 2026-10-24 | `manifest.json` |
| Remote Config | 4.2.5 | Résolue par l'éditeur (optionnel) | 2026-10-24 | `manifest.json` |
| Cloud Diagnostics | 1.0.10 | Inchangée ; inutilisée par les scripts, statut à vérifier | 2026-10-24 | `manifest.json` |
| Android : API cible / minimale | 36 (Android 16) / 26 (Android 8.0) | Google Play exige l’API 36 pour toute nouvelle app et mise à jour depuis le 2026-08-31 (prolongation possible jusqu’au 2026-11-01). Unity 6000.6 permet de cibler 35 et 36 et supporte Android 8.0 et plus. SDK Android 36 et build-tools 36.0.0 fournis avec l’éditeur. À l’API 36, l’affichage bord à bord est imposé sans exception et le retour prédictif est actif par défaut | 2026-10-25 | developer.android.com/google/play/requirements/target-sdk, manuel Unity 6000.6 « Android requirements and compatibility », developer.android.com/about/versions/16/behavior-changes-16 |
| Google Play Billing Library | 9.0.0 | Embarquée par Unity IAP 5.4.4 ; pour publier une nouvelle app ou une mise à jour, Google exige au moins la 8 depuis le 2026-08-31 et la 9 à partir du 2027-08-31 ; la 9.0.0 reste acceptée jusqu’au 2028-08-31 | 2026-10-25 | cache du package `com.unity.purchasing`, developer.android.com/google/play/billing/deprecation-faq |
| URP | 17.6.0 | Résolue par l'éditeur | 2026-10-24 | `manifest.json` |
| Backend Cloud Code et bibliothèque `DevilCorp.MobileFoundation.Server` | net8.0, `Com.Unity.Services.CloudCode.Apis` 0.0.16 (`Core` 0.0.1) | Passé de net6.0 (hors support Microsoft depuis 2024-11) le 2026-10-25. **Apis reste en 0.0.16** : la 0.0.27 impose `Core` 0.0.7, que le générateur de bindings du package Cloud Code 2.10.4 refuse (il embarque sa propre `Core`, échec `FileLoadException` le 2026-10-25). Tout ce que le portefeuille utilise existe en 0.0.16 (`SetProtectedItemBatchAsync`, writeLock). Les appels Cloud Save protected exigent `context.ServiceToken` (jeton joueur → 401, spike du 2026-10-25). Plafond .NET 9 sur ce chemin ; net8 et net9 hors support le 2026-11-10. Cloud Code 3.0 (2026-10-23) cible .NET 10 mais exige Unity 6000.7, en bêta ; la page des modules de la doc 3.0.0 dit encore « up to version 9 ». Montée d’Apis à refaire avec la migration Unity 6.7 / Cloud Code 3.0 | 2026-10-25 | `.csproj`, changelog Cloud Code 3.0, réflexion sur les DLL, génération de bindings |
| Tests backend | net10.0, xUnit 2.9.2, NSubstitute 5.1.0 | Seul runtime .NET installé localement | 2026-10-24 | `Tests/GemHunterUGSCloud.Tests.csproj` |
| Package commun `com.devilcorp.mobilefoundation` | 0.2.0 | Modules Achats (décision 0006) et Pubs (décision 0007) ; dépend de Unity IAP 5.4.4 ; LevelPlay ≥ 9.0.0 optionnel (adaptateur compilé seulement s'il est installé) | 2026-10-25 | `SharedPackages/com.devilcorp.mobilefoundation/package.json`, `README.md` |
| Tests du cœur Achats | net10.0, C# 9, xUnit 2.9.2 | Même code source que le package, limité au C# supporté par Unity | 2026-10-24 | `SharedPackages/Tests/MobileFoundation.Purchases.Tests` |
| Tests serveur | net10.0, xUnit 2.9.2, NSubstitute 5.1.0 | Bibliothèque serveur et module Gem Hunter ; Cloud Save simulé avec la sémantique observée au spike | 2026-10-25 | `SharedPackages/Server/Tests/`, `UnityProject/GemHunterUGSCloud/Tests/` |
| Retirés le 2026-10-25 | — | Economy 3.5.4 (fermé aux nouveaux projets, remplacé par le portefeuille protégé, décision 0008), Friends 1.3.0 et Leaderboards 2.3.4 (fonctions retirées du template) | 2026-10-25 | `manifest.json` |

## Outillage de déploiement et de pilotage

| Outil | Version | Rôle | Vérifié le |
| --- | --- | --- | --- |
| Unity CLI | 1.0.0-beta.14 | Éditeurs, projets, builds ; pilotage de l'éditeur ouvert via le package Pipeline (`unity command …`). Ne déploie pas de ressources UGS et ne gère pas les environnements | 2026-10-25 |
| Fenêtre Deployment (`com.unity.services.deployment` 1.7.2, API 1.1.3) | — | Déploie depuis l'éditeur le module Cloud Code (`.ccmr`) et les fichiers de config (`.rc`, `.ec*`…) vers l'environnement actif | 2026-10-25 |
| UGS CLI | non installée | Déploiement et gestion des environnements hors éditeur ; exige un compte de service | 2026-10-25 |
| Serveur Cloud Code local | non utilisé | Exécution locale des modules ; ne prouve pas les politiques du service distant | 2026-10-25 |

À vérifier par un build : adaptateurs réseaux LevelPlay, build Android avec l’API cible 36 (bord à bord, retour prédictif sur appareil), iOS minimum et version de Xcode.

## Problèmes connus

### Bruit graphique en Vulkan sur Mali-G610 (2026-10-26)

- **Symptôme :** dans le hub et les niveaux, la scène 3D est couverte d’un bruit par blocs qui change à chaque image ; les zones redessinées par une action de jeu redeviennent nettes un moment. L’interface UI Toolkit n’est pas touchée.
- **Appareil :** Nothing Phone A142 (MT6886, Mali-G610 MC4, pilote r38p1), Android 15, build de développement, Unity 6000.6.7f1, URP avec le Renderer 2D.
- **Preuve :** le même build en OpenGL ES 3 seul (API graphiques automatiques désactivées) s’affiche sans défaut, dans le hub comme en niveau. Le défaut est donc propre au chemin Vulkan.
- **Contournement disponible, non appliqué :** garder Vulkan par défaut et forcer OpenGL ES sur les appareils concernés avec un *Vulkan hardware profile* (méthode recommandée par la doc Unity 6000.6 ; les *Vulkan Device Filtering Assets* et les listes Allow/Deny des Player settings sont plus anciens). Solution de repli plus large : OpenGL ES 3 seul sur Android, au prix de Vulkan sur tous les appareils.
- **Statut :** recherche de la cause dans les réglages Vulkan et URP en cours (option C). Revenir à OpenGL ES si elle n’aboutit pas.
- **Sources :** [Graphics API hardware profile settings (6000.6)](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/developing/graphics/vulkan-hardware-profiles/graphics-api), [Vulkan Device Filtering Asset reference (6000.6)](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/developing/graphics/vulkanapi-graphics-jobs-configuration/vulkan-device-filter-list-asset-reference).
