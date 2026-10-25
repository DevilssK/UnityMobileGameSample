# Pubs récompensées et documentation du package — plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal :** ajouter au package `com.devilcorp.mobilefoundation` un module de pubs récompensées (cœur testable + adaptateur LevelPlay optionnel), le brancher simplement dans Gem Hunter, et documenter le package pour qu'un autre projet puisse l'intégrer sans lire le code de Gem Hunter.

**Architecture :** même découpage que Purchases. Un cœur sans moteur (`DevilCorp.MobileFoundation.Ads`, `noEngineReferences`) contient le contrat `IRewardedAdsService` et un `RewardedAdCoordinator` qui garantit les invariants (récompense seulement sur l'événement de récompense, une seule fois par affichage, jamais à la fermeture). Un adaptateur `DevilCorp.MobileFoundation.Ads.LevelPlay` traduit le SDK et ne compile que si LevelPlay est installé (`versionDefines`). Gem Hunter n'est qu'un exemple d'intégration : on ne développe aucune logique métier de récompense (instruction utilisateur du 2026-10-25).

**Tech Stack :** Unity 6000.6.7f1, LevelPlay 9.5.1 (`com.unity.services.levelplay`, assembly `Unity.LevelPlay`, namespace `Unity.Services.LevelPlay`), C# 9, xUnit sur net10.0 (LangVersion 9), Unity CLI pour la compilation.

**Spec :** `Doc/CLAUDE_UNITY_MOBILE_FOUNDATION.md` §5 (P0 pubs, P1), §6.2, §9, §10.3, §14 ; décision `Doc/decisions/0001-modele-economique-v1.md`.

## Global Constraints

- Récompense pub = effet local de faible valeur, accordé une seule fois sur l'événement de récompense du SDK, jamais à la simple fermeture (décision 0001, brief §9).
- « Les builds de production ne doivent pas activer les suites de test du SDK » (brief §9).
- « Jamais de secrets, reçus complets, tokens ou signatures dans le code client ou les logs » (CLAUDE.md).
- Le package ne référence jamais un type ou une scène Gem Hunter ; le gameplay ne touche jamais LevelPlay directement.
- Cœur : `#nullable enable` par fichier, C# 9, pas de `ConfigureAwait(false)` (les continuations restent sur le thread principal Unity, cf. `MainThreadTests`).
- Champs privés `_camelCase` dans le package ; le code Gem Hunter garde ses conventions `m_`.
- Doc en français, code en anglais.
- Vérification Unity par la **Unity CLI** en priorité (`unity run` / `unity test`), MCP seulement si un éditeur interactif est nécessaire.

## Review Focus

1. Événement de récompense reçu **après** la fermeture (ordre fréquent sur Android) : la récompense doit être accordée si elle arrive dans le délai de grâce → test `Reward_ArrivingAfterClose_WithinGrace_IsGranted` (tâche 1).
2. Récompense signalée deux fois pour un même affichage → une seule → test `Reward_RaisedTwice_IsGrantedOnce` (tâche 1).
3. Pub demandée alors qu'aucune n'est chargée → `Unavailable` sans bloquer, et nouveau chargement demandé → test `ShowAsync_WhenNotReady_ReturnsUnavailableAndRequestsLoad` (tâche 1).
4. Échecs de chargement répétés → arrêt après `MaxLoadRetries`, pas de boucle infinie → test `LoadFailures_StopAfterMaxRetries` (tâche 1).
5. Attente UI annulée pendant que la pub s'affiche → le résultat final arrive quand même par `AdCompleted` → test `ShowAsync_WhenCallerCancels_ResultStillRaisedThroughAdCompleted` (tâche 1).

---

### Task 1 : cœur Ads du package (contrat + coordinateur), TDD

**Files :**
- Create : `SharedPackages/com.devilcorp.mobilefoundation/Runtime/Ads/DevilCorp.MobileFoundation.Ads.asmdef` (même forme que l'asmdef Purchases, `noEngineReferences: true`)
- Create, dans `Runtime/Ads/` : `RewardedAdOutcome.cs`, `AdReward.cs`, `RewardedAdResult.cs`, `IRewardedAdGateway.cs`, `IRewardedAdsService.cs`, `RewardedAdCoordinator.cs`
- Create : `SharedPackages/Tests/MobileFoundation.Ads.Tests/MobileFoundation.Ads.Tests.csproj` (copie du csproj Purchases, lien `Runtime/Ads/**/*.cs`), `RewardedAdCoordinatorTests.cs`, `AdsTestDoubles.cs`
- Les `.meta` des nouveaux fichiers sont générés par Unity à la tâche 3 (ne pas les écrire à la main).

**Interfaces — Produces :**
```csharp
namespace DevilCorp.MobileFoundation.Ads
public enum RewardedAdOutcome { Rewarded, NotRewarded, Unavailable, Failed, AlreadyShowing }
public sealed class AdReward { AdReward(string name, int amount); string Name; int Amount; }
public sealed class RewardedAdResult { RewardedAdResult(RewardedAdOutcome outcome, string placement, AdReward? reward = null, string? message = null); Outcome; Placement; Reward; Message; override ToString(); }
public interface IRewardedAdGateway {
    bool IsReady { get; }
    event Action? Loaded;
    event Action<string>? LoadFailed;        // raison lisible, sans donnée personnelle
    event Action<AdReward>? Rewarded;
    event Action? Closed;
    event Action<string>? DisplayFailed;
    void Load();
    void Show(string placement);
}
public interface IRewardedAdsService {
    bool IsAdReady { get; }
    bool IsShowing { get; }
    event Action<bool>? AvailabilityChanged;
    event Action<RewardedAdResult>? AdCompleted;   // levé pour chaque affichage terminé
    void Start();                                   // premier chargement ; idempotent
    Task<RewardedAdResult> ShowAsync(string placement, CancellationToken cancellationToken);
}
public sealed class RewardedAdCoordinator : IRewardedAdsService {
    public const int MaxLoadRetries = 3;
    public static readonly TimeSpan DefaultRewardGracePeriod = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DefaultLoadRetryDelay = TimeSpan.FromSeconds(5);
    RewardedAdCoordinator(IRewardedAdGateway gateway, Func<TimeSpan, Task>? delay = null,
                          TimeSpan? rewardGracePeriod = null, Action<string>? log = null);
}
```

Règles du coordinateur :
- `ShowAsync` : affichage déjà en cours → `AlreadyShowing` immédiat ; gateway pas prête → `Unavailable` + `gateway.Load()` ; sinon `gateway.Show(placement)` et attente.
- `Rewarded` pendant l'affichage → mémorise la récompense (la première seulement). `Closed` → si récompense mémorisée, termine `Rewarded` ; sinon attend `rewardGracePeriod` via `delay` puis termine `Rewarded` si elle est arrivée entre-temps, sinon `NotRewarded`. `DisplayFailed` → `Failed`.
- Une récompense reçue hors affichage ou après la fin est ignorée et journalisée.
- Après chaque fin d'affichage : `AdCompleted`, `AvailabilityChanged(false)`, puis `gateway.Load()`.
- `Loaded` → remise à zéro du compteur d'échecs, `AvailabilityChanged(true)`. `LoadFailed` → si compteur < `MaxLoadRetries`, attend `DefaultLoadRetryDelay` puis `Load()` ; sinon `AvailabilityChanged(false)` et arrêt (relancé par le prochain `ShowAsync`, qui remet le compteur à zéro).
- Annuler le jeton ne termine que l'attente (résultat `NotRewarded`, message « wait cancelled ») ; le vrai résultat est levé par `AdCompleted`.

- [ ] **Step 1 : écrire les tests** dans `RewardedAdCoordinatorTests.cs`, avec un `FakeAdGateway` (lève chaque événement à la demande, compte `LoadCalls`, enregistre `Shows`) et un `delay` contrôlé (`TaskCompletionSource` libéré par le test) :
  - `ShowAsync_WhenRewardedThenClosed_ReturnsRewardedOnce` — `Outcome == Rewarded`, `Reward.Name == "coins"`, `Reward.Amount == 10`, `AdCompleted` levé une fois.
  - `ShowAsync_WhenClosedWithoutReward_ReturnsNotRewardedAfterGrace` — pas de résultat avant libération du délai, `NotRewarded` après.
  - `Reward_ArrivingAfterClose_WithinGrace_IsGranted`
  - `Reward_RaisedTwice_IsGrantedOnce` — un seul `AdCompleted`, `Reward.Amount` de la première.
  - `Reward_OutsideShow_IsIgnored` — aucun `AdCompleted`.
  - `ShowAsync_WhenNotReady_ReturnsUnavailableAndRequestsLoad` — `Shows` vide, `LoadCalls == 1`.
  - `ShowAsync_WhileShowing_ReturnsAlreadyShowing`
  - `ShowAsync_WhenDisplayFails_ReturnsFailedAndReloads`
  - `LoadFailures_StopAfterMaxRetries` — après 4 `LoadFailed` (délais libérés), `LoadCalls == 1 + MaxLoadRetries` et dernier `AvailabilityChanged == false`.
  - `ShowAsync_WhenCallerCancels_ResultStillRaisedThroughAdCompleted`
  - `Start_IsIdempotent` — deux `Start()` → `LoadCalls == 1`.
  - `RewardedAdResult_ToString_ContainsOutcomeAndPlacement`
- [ ] **Step 2 : lancer** `dotnet test SharedPackages/Tests/MobileFoundation.Ads.Tests` → échec de compilation (types absents).
- [ ] **Step 3 : implémenter** les six fichiers du cœur selon les interfaces et règles ci-dessus.
- [ ] **Step 4 : lancer** la même commande → 12/12 ; relancer `dotnet test SharedPackages/Tests/MobileFoundation.Purchases.Tests` → 20/20 inchangé.
- [ ] **Step 5 : commit** « Add rewarded ads core to the foundation package ».

### Task 2 : adaptateur LevelPlay optionnel

**Files :**
- Create : `Runtime/Ads.LevelPlay/DevilCorp.MobileFoundation.Ads.LevelPlay.asmdef` — références `DevilCorp.MobileFoundation.Ads`, `Unity.LevelPlay` ; `versionDefines: [{ "name": "com.unity.services.levelplay", "expression": "9.0.0", "define": "DEVILCORP_LEVELPLAY" }]` ; `defineConstraints: ["DEVILCORP_LEVELPLAY"]`.
- Create : `Runtime/Ads.LevelPlay/LevelPlayAdsSettings.cs`, `Runtime/Ads.LevelPlay/LevelPlayRewardedAdGateway.cs`

**Interfaces :**
- Consumes : `IRewardedAdGateway`, `AdReward` (tâche 1).
- Produces :
```csharp
namespace DevilCorp.MobileFoundation.Ads.LevelPlay
public sealed class LevelPlayAdsSettings {
    LevelPlayAdsSettings(string androidAppKey, string iosAppKey, string androidRewardedAdUnitId,
                         string iosRewardedAdUnitId, bool enableTestSuite = false);
}
public sealed class LevelPlayRewardedAdGateway : IRewardedAdGateway, IDisposable {
    LevelPlayRewardedAdGateway(LevelPlayAdsSettings settings, Action<string>? log = null);
    void Initialize(string? userId);   // LevelPlay.Init(appKey, userId) ; idempotent
}
```

Règles :
- Clé et ad unit choisies par `#if UNITY_ANDROID` / `#elif UNITY_IOS` ; autre plateforme → `IsReady == false`, `Load()` lève `LoadFailed("unsupported platform")`. (Corrige le sample, qui utilisait l'ad unit Android sur iOS.)
- `LevelPlay.OnInitSuccess` → crée `LevelPlayRewardedAd(adUnitId)`, s'abonne à tous ses événements, puis exécute un `Load()` demandé avant l'init. `OnInitFailed` → `LoadFailed(error.ErrorMessage)`.
- Suite de test : `LevelPlay.SetMetaData("is_test_suite", "enable")` avant `Init` et `LevelPlay.LaunchTestSuite()` après l'init **uniquement si** `enableTestSuite && UnityEngine.Debug.isDebugBuild`.
- `OnAdRewarded(info, reward)` → `Rewarded(new AdReward(reward.Name, reward.Amount))`. Aucun log de `LevelPlayAdInfo` complet (ids d'instance seulement si nécessaire au diagnostic, jamais l'id utilisateur).
- `Dispose` : désabonnement de tous les événements et `DestroyAd()`.

- [ ] **Step 1 : implémenter** les trois fichiers.
- [ ] **Step 2 : commit** « Add optional LevelPlay rewarded ad gateway » (compilé à la tâche 3, après le branchement).

### Task 3 : branchement Gem Hunter, logs sans token, compilation

**Files :**
- Modify : `UnityProject/Assets/GemHunterUGS/Scripts/Core/GameInitializer.cs` — `CreateRewardedAdsService(authenticationManager)` : `LevelPlayAdsSettings` (clés placeholder du sample : `"enterappkey"`, `"enterappid"`, ad units `"xxxx"`, `"xxx"`, `enableTestSuite: true`), gateway + coordinateur, `GameSystemLocator.Register<IRewardedAdsService>`, sur `SignedIn` : `gateway.Initialize(AuthenticationService.Instance.PlayerId)` puis `service.Start()`.
- Modify : `UnityProject/Assets/GemHunterUGS/Scripts/Ads/AdRewardManagerClient.cs` — devient un mince adaptateur hôte : récupère `IRewardedAdsService`, garde son API publique actuelle (`AdSuccessfullyCompleted`, `AdAvailable`, `CanShowAd`, `ClickShowAdReward`, `GetRemainingCooldownSeconds`) pour ne pas toucher `AdRewardUIController`. Sur `Rewarded` : `Logger.LogDemo` du nom et du montant et `AdSuccessfullyCompleted(true)`, **aucun crédit**. Suppression de l'appel `HandleGrantVideoAdReward`, du jeton client, de toute référence LevelPlay. Nom de classe et fichier conservés (GUID et références de prefab intacts — skill `unity-asset-safety`).
- Modify : `Core/PlayerAuthenticationManager.cs` (`LogPlayerInfo` : retirer le token) et `Login and AccountManagement/UnityPlayerAccountSignIn.cs:264` (retirer la ligne `PlayerToken`).

- [ ] **Step 1 : appliquer** les modifications.
- [ ] **Step 2 : vérifier** qu'aucun token n'est plus journalisé : `grep -rn "AccessToken\|TokenString" UnityProject/Assets/GemHunterUGS/Scripts | grep -i log` → aucune ligne.
- [ ] **Step 3 : compiler par la Unity CLI** (éditeur fermé) : `unity run UnityProject --editor-version 6000.6.7f1 -- -logFile <scratchpad>/compile.log` puis rechercher `error CS` dans le log → 0 ; vérifier que les `.meta` des nouveaux fichiers du package ont été créés. Restaurer ensuite tout fichier re-sérialisé hors périmètre (`VersionControlSettings.asset`, etc.) avec `git checkout --`.
- [ ] **Step 4 : commit** (meta du package inclus) « Route Gem Hunter rewarded ads through the foundation service; stop logging tokens ».

### Task 4 : documentation du package

**Files :**
- Create : `SharedPackages/com.devilcorp.mobilefoundation/README.md` — en français, pour un développeur qui intègre le package dans un autre projet :
  1. Ce que fait le package / ce qu'il ne fait pas (aucune attribution côté client, pas de backend fourni).
  2. Installation : dépendance `file:` (chemin relatif) et URL git (`https://github.com/DevilssK/UnityMobileGameSample.git?path=/SharedPackages/com.devilcorp.mobilefoundation#<tag>`), version Unity minimale, dépendances (IAP 5.4.4 obligatoire, LevelPlay ≥ 9.0 optionnel).
  3. Modules et assemblies (tableau : cœur / adaptateur, dépendances, `noEngineReferences`).
  4. Achats : schéma du flux (store → `PurchaseCoordinator` → `IPurchaseFulfillment` → serveur → `Confirm`), exemple minimal de composition (catalogue `StoreProductDefinition`, gateway, fulfillment, coordinateur), contrat à implémenter côté jeu (`IPurchaseFulfillment` et le sens de `Delivered` / `Rejected` / `RetryLater`), exigences pour le backend (dédup sur l'id de transaction du store, refus du fake store hors dev, réponse idempotente), quand appeler `RetryPendingFulfillmentsAsync`.
  5. Pubs récompensées : exemple minimal de composition, politique (effet local, délai de grâce, pas de crédit à la fermeture), suite de test réservée aux builds de debug.
  6. Threading (continuations sur le thread principal) et logs (jamais de reçu ni token).
  7. Tests : commandes `dotnet test` des deux projets.
  8. Limites connues (liste des mineurs différés du rapport précédent + pas de restauration iOS ni d'abonnements).
- Create : `SharedPackages/com.devilcorp.mobilefoundation/CHANGELOG.md` (format Keep a Changelog : 0.1.0 Purchases, 0.2.0 Ads).
- Modify : `package.json` → `version` `0.2.0`, `description` à jour.
- Create : `Doc/decisions/0007-module-pubs-optionnel.md` (skill `decision-record`) : adaptateur LevelPlay optionnel par `versionDefines`, délai de grâce de récompense.
- Modify : `Doc/dependencies.md` (LevelPlay optionnel pour le package) et la section Organisation de `CLAUDE.md` si besoin.
- Les `.meta` de `README.md` / `CHANGELOG.md` : laisser Unity les générer (relancer la compilation CLI de la tâche 3).

- [ ] **Step 1 : rédiger** les fichiers ; chaque exemple de code doit compiler contre les signatures réelles (relire les fichiers sources en écrivant).
- [ ] **Step 2 : régénérer les meta** par la commande CLI de la tâche 3 ; 0 `error CS`.
- [ ] **Step 3 : commit** « Document the foundation package and its integration ».

### Fin

Relecture finale de la branche par un sous-agent (opus), corrections, puis rapport au format CLAUDE.md. Branche : `feature/rewarded-ads-and-docs`, créée depuis `feature/client-purchases-iap5`.

**Non vérifiable ici :** affichage réel d'une pub (clés LevelPlay placeholder, pas d'appareil), sandbox stores.
