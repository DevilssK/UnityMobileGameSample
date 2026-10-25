# Portefeuille protégé sur Cloud Save — plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal :** remplacer Unity Economy par un portefeuille protégé géré par Cloud Code sur Cloud Save (classe protected), réutilisable par d'autres jeux, et brancher dessus la démo Gem Hunter retenue (boutique, zones, niveaux et cœurs, quotidien).

**Architecture :**
- Une bibliothèque serveur `DevilCorp.MobileFoundation.Server` (sans type Gem Hunter) fournit le stockage protégé, la transaction « lire → règle pure → batch conditionnel → relances », l'initialisation sous délai de grâce, les règles du portefeuille, le registre des achats et les catalogues.
- Le module Gem Hunter ne garde que ses règles de jeu, sous forme de fonctions pures, et des fonctions Cloud Code qui orchestrent.
- Côté client, on régénère les bindings, on ajoute les `operationId`, on choisit l'environnement, on masque les achats réels hors développement et on retire les fonctions hors périmètre.

**Tech Stack :**
- module et bibliothèque : C# net8.0 (après la tâche 3), `Com.Unity.Services.CloudCode.Apis` 0.0.27, Newtonsoft.Json ;
- tests : xUnit sur net10.0 ;
- Unity 6000.6.7f1, pilotée par la Unity CLI (`unity command …`) ;
- déploiement par la fenêtre Deployment vers l'environnement `development`.

**Spec :** `Doc/specs/2026-10-25-portefeuille-protege-design.md` (validée le 2026-10-25). Contexte : `Doc/revue/2026-10-25-realignement-diagnostic.md`, décision `Doc/decisions/0008-remplacement-economy.md`, skill `monetization-change`.

## Global Constraints

- « Une transaction valide → une seule attribution logique, même après retries, panne ou appels concurrents » (skill `monetization-change`).
- Toutes les données de jeu protégées sont en classe **protected**. Chaque opération métier écrit **un seul batch protected** (≤ 20 clés) avec le writeLock de chaque clé existante. Seule une clé nouvelle et déterministe (`PURCHASE_*`) s'écrit sans writeLock.
- Écriture inconditionnelle réservée à l'initialisation, et seulement si la lecture de la même exécution a trouvé `WALLET` absent.
- Toute mutation est refusée (`RETRY_LATER`) tant que `now < WALLET.createdAtUnixMs + 30 000 ms`. La constante `ProtectedStateOptions.GracePeriod` vaut 30 s.
- Relances sur conflit `409` : `MaxAttempts = 5`, délai aléatoire de 20 à 100 ms, puis `RETRY_LATER`.
- Codes d'erreur, en préfixe du message d'exception : `PURCHASE_REJECTED`, `PURCHASE_RETRY_LATER`, `INSUFFICIENT_FUNDS`, `RETRY_LATER`, `CONFIGURATION_ERROR`.
- Faux store accepté seulement si `context.EnvironmentName == "development"` (`FakeStorePolicy`, inchangé). Un vrai reçu Google ou Apple → `PURCHASE_RETRY_LATER` dans ce plan.
- Logs : `CorrelationId`, `TransactionLogId`, `operationId`, opération, résultat. **Jamais** de reçu, payload, signature, token ni id de transaction brut.
- Aucun type, nom de clé ou identifiant Gem Hunter dans `DevilCorp.MobileFoundation.Server`.
- `PlayerEconomyData` garde son format. `ItemInventory` contient **tous** les objets du catalogue, y compris à 0. `InfiniteHeartsExpiryTimestamp` est en **secondes** Unix.
- Nouveaux fichiers : conventions du brief §7 (`_camelCase`, constantes PascalCase, Allman, 4 espaces). Fichiers Gem Hunter modifiés : conventions existantes (`m_`, `k_`).
- Assets Unity : skill `unity-asset-safety`. Scènes et prefabs modifiés par l'éditeur via la Unity CLI, jamais en éditant le YAML à la main.
- Ne jamais commiter `Packages/manifest.json` / `packages-lock.json` avec les lignes locales `com.unity.ai.*` ou `com.unity.pipeline`, ni `ProjectSettings/EditorBuildSettings.asset`, `.agents/`, `.codex/`, `AGENTS.md`.
- Déploiement uniquement vers `development`. **Aucun déploiement en `production`** dans ce plan.
- Commits : correction, montée de version et refactoring séparés. Message terminé par `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Aller-retour des valeurs Cloud Save** (`Item.Value` reçu comme objet Newtonsoft, `SetItemBody.Value` envoyé comme `JToken`). Une valeur mal sérialisée corromprait silencieusement le portefeuille. Test d'adaptateur, tâche 4, et intégration, tâche 11.
2. **Cache Remote Config mélangeant deux environnements.** Test `GetAsync_TwoEnvironments_DoNotShareCache`, tâche 6.
3. **Batch de plus de 20 clés** (une opération qui grossit). Test `WriteBatchAsync_MoreThan20Writes_Throws`, tâche 4.
4. **Bord du délai de grâce** (`now == createdAt + 30 s`). Test `Run_AtExactEndOfGracePeriod_IsAccepted`, tâche 5.
5. **Achat rejoué par le même joueur après un premier succès, avec un catalogue modifié entre-temps.** Il doit renvoyer l'état actuel sans recalculer de gain. Test `ApplyPurchase_AlreadyRecorded_IgnoresCurrentCatalog`, tâche 6.

---

### Task 1 : Environnement côté client

**Files :**
- Create : `UnityProject/Assets/GemHunterUGS/Scripts/Core/UgsEnvironment.cs` (+ `.meta` généré par Unity)
- Modify : `UnityProject/Assets/GemHunterUGS/Scripts/Core/GameInitializer.cs` (`k_Environment`)

**Interfaces :**
- Produces : `static string UgsEnvironment.Resolve(string overrideName, bool isEditor, bool isDebugBuild)`.
  - `overrideName` non vide → cette valeur ;
  - sinon `isEditor || isDebugBuild` → `"development"` ;
  - sinon `"production"`.

- [ ] **Step 1 :** implémenter `UgsEnvironment.Resolve`. Dans `GameInitializer`, remplacer la constante par un champ `[SerializeField] string m_EnvironmentOverride = ""` passé à `Resolve(m_EnvironmentOverride, Application.isEditor, Debug.isDebugBuild)`. Journaliser le nom retenu (pas de donnée sensible).
- [ ] **Step 2 :** `unity command recompile` puis `console status`. Attendu : `compilationFailed:false`, 0 erreur.
- [ ] **Step 3 :** Play depuis la scène de démarrage (`unity command start`). Lire la console. Attendu : `UGS environment: development`, puis connexion anonyme réussie. Arrêter avec `unity command stop`.
- [ ] **Step 4 : commit** « Select UGS environment per build type instead of hard-coded production ».

### Task 2 : Spike Cloud Save en `development`

**Files :**
- Create (temporaire, supprimé à l'étape 6) : `UnityProject/GemHunterUGSCloud/Project/Spike/CloudSaveSpike.cs`, fonction `[CloudCodeFunction("SpikeCloudSave")]`
- Modify : `Doc/revue/2026-10-25-realignement-diagnostic.md` (résultats)

- [ ] **Step 1 :** écrire `SpikeCloudSave(IExecutionContext)`. Elle exécute sur des clés `SPIKE_*` du joueur et renvoie un rapport texte (codes HTTP et messages d'erreur, aucune donnée sensible) :
  - (a) `SetProtectedItemAsync` d'une clé absente avec `writeLock = ""`, puis avec `writeLock = "0"` ;
  - (b) écriture sans writeLock d'une clé existante ;
  - (c) `SetProtectedItemBatchAsync` de deux clés existantes, dont une avec un writeLock périmé ;
  - (d) `Item.Value` relu : son type .NET et sa forme `ToString()`.
- [ ] **Step 2 :** passer l'environnement actif de la fenêtre Deployment à `development` par eval (`EnvironmentsApi.Instance`). Ouvrir la fenêtre (`DeploymentWindow.OpenWindow()`), déployer le `.ccmr`. Attendu : « deployed … successfully » ; aucun déploiement en `production`.
- [ ] **Step 3 :** en Play (environnement `development`, Task 1), appeler `SpikeCloudSave` par eval (`CloudCodeService.Instance.CallModuleEndpointAsync`). Puis tenter une écriture protected avec le jeton du joueur : requête HTTP `POST https://cloud-save.services.api.unity.com/v1/data/projects/{projectId}/players/{playerId}/protected/items` (jeton lu en mémoire, jamais affiché). Attendu pour cette dernière : `403` ou `401`.
- [ ] **Step 4 :** consigner les résultats (a) à (d) et le refus client dans le diagnostic, section « A1-B — Persistance ». **Point de décision :**
  - si (a) montre qu'un writeLock réservé refuse une clé existante et crée une clé absente, la tâche 5 utilise cette création conditionnelle au lieu du délai de grâce ;
  - sinon, délai de grâce.
  - Si (c) ne rejette pas tout le batch : **arrêt et question au propriétaire**, l'atomicité étant la base de la spec.
- [ ] **Step 5 :** supprimer `Spike/`, redéployer en `development`.
- [ ] **Step 6 : commit** « Record Cloud Save spike results in development ». Le code du spike n'est pas commité.

### Task 3 : Montées de version du module

**Files :** `UnityProject/GemHunterUGSCloud/Project/GemHunterUGSCloud.csproj`

- [ ] **Step 1 :** `TargetFramework` net6.0 → net8.0. `dotnet build Project -c Release` → 0 erreur ; `dotnet test Tests` → 93/93.
- [ ] **Step 2 : commit** « Target .NET 8 for the Cloud Code module ».
- [ ] **Step 3 :** `Com.Unity.Services.CloudCode.Apis` 0.0.16 → 0.0.27. Corriger seulement les ruptures de signature, par exemple `DeleteProtectedItemAsync`, dont la clé change de position. Build et tests : mêmes attentes.
- [ ] **Step 4 :** mettre à jour la ligne « Backend Cloud Code » de `Doc/dependencies.md`. **commit** « Upgrade Cloud Code APIs SDK to 0.0.27 ».

### Task 4 : Bibliothèque serveur — stockage protégé et transaction

**Files :**
- Create : `SharedPackages/Server/DevilCorp.MobileFoundation.Server/DevilCorp.MobileFoundation.Server.csproj` (net8.0, `Nullable` enable, refs `Com.Unity.Services.CloudCode.Apis` 0.0.27, `Newtonsoft.Json`)
- Create, dans `State/` : `StoredItem.cs`, `ItemWrite.cs`, `IProtectedStateStore.cs`, `CloudSaveProtectedStateStore.cs`, `StateSnapshot.cs`, `StateTransaction.cs`, `ProtectedStateOptions.cs`, `GameOperationException.cs`, `ErrorCodes.cs`, `IClock.cs`
- Create : `SharedPackages/Server/Tests/DevilCorp.MobileFoundation.Server.Tests/` (net10.0, xUnit 2.9.2, NSubstitute 5.1.0), `FakeProtectedStateStore.cs`, `StateTransactionTests.cs`, `CloudSaveProtectedStateStoreTests.cs`
- Modify : `.gitignore` si un `*.csproj` y est ignoré pour ces chemins

**Interfaces (Produces) :**
- `record StoredItem(string Key, JToken Value, string WriteLock)`
- `record ItemWrite(string Key, JToken Value, string? WriteLock)` : `WriteLock == null` signifie création.
- `interface IProtectedStateStore` :
  - `Task<IReadOnlyDictionary<string, StoredItem>> ReadAsync(IExecutionContext context, IReadOnlyCollection<string> keys, CancellationToken ct)`
  - `Task<bool> TryWriteBatchAsync(IExecutionContext context, IReadOnlyList<ItemWrite> writes, CancellationToken ct)` : `false` uniquement sur conflit `409`.
- `CloudSaveProtectedStateStore(IGameApiClient api)` :
  - `GetProtectedItemsAsync(ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId, keys, null)` ;
  - `SetProtectedItemBatchAsync(ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId, new SetItemBatchBody(list of new SetItemBody(key, value, writeLock)))` ;
  - conflit = `ApiException` avec `Response.StatusCode == HttpStatusCode.Conflict` ;
  - plus de 20 écritures → `ArgumentException` avant tout appel.
- `class StateSnapshot` : `bool TryGet<T>(string key, out T value)`, `string? WriteLockOf(string key)`.
- `sealed class StateMutation<TResult>` : `Writes` (`IReadOnlyDictionary<string, object>`), `TResult Result`.
- `sealed class StateTransaction(IProtectedStateStore store, IClock clock, Func<TimeSpan, Task>? delay = null)` :
  - `const int MaxAttempts = 5` ;
  - `Task<TResult> RunAsync<TResult>(IExecutionContext ctx, IReadOnlyCollection<string> keys, Func<StateSnapshot, StateMutation<TResult>> decide, CancellationToken ct)`.
  - Chaque écriture reprend le writeLock lu, ou `null` si la clé était absente.
  - `decide` peut lever `GameOperationException`, qui est propagée sans écriture.
- `class GameOperationException(string code, string detail)` : `Message = $"{code}: {detail}"`. `ErrorCodes` regroupe les cinq codes des contraintes globales.

- [ ] **Step 1 : tests qui échouent** (`StateTransactionTests`, avec `FakeProtectedStateStore` qui sait renvoyer N conflits, lever une exception et enregistrer chaque batch) :
  - `Run_NoConflict_WritesOnceWithReadWriteLocks`
  - `Run_KeyAbsent_WritesWithNullWriteLock`
  - `Run_ConflictThenSuccess_RereadsAndRetries`
  - `Run_FiveConflicts_ThrowsRetryLaterWithoutPartialWrite`
  - `Run_DecideThrowsGameOperation_NoWrite`
  - `CloudSaveProtectedStateStoreTests.WriteBatchAsync_MoreThan20Writes_Throws` et `TryWriteBatchAsync_Conflict409_ReturnsFalse` (NSubstitute sur `ICloudSaveDataApi` ; `ApiResponse` renseignée par réflexion, comme dans `UnityProject/GemHunterUGSCloud/Tests/Support/PurchaseTestDoubles.cs`)
  - `ReadAsync_ValueObject_RoundTripsAsJToken` (forme de `Item.Value` relevée au spike (d))
- [ ] **Step 2 :** `dotnet test SharedPackages/Server/Tests/DevilCorp.MobileFoundation.Server.Tests` → échec de compilation.
- [ ] **Step 3 :** implémenter.
- [ ] **Step 4 :** même commande → tout passe.
- [ ] **Step 5 : commit** « Add server foundation library with protected state transaction ».

### Task 5 : Initialisation et délai de grâce

**Files :** `SharedPackages/Server/DevilCorp.MobileFoundation.Server/State/ProtectedStateInitializer.cs`, `Wallet/WalletState.cs`, tests `ProtectedStateInitializerTests.cs`

**Interfaces :**
- Consumes : tâche 4.
- Produces :
  - `WalletState` : `SchemaVersion` (const 1), `CreatedAtUnixMs`, `Currencies` (`Dictionary<string,long>`), `Items` (`Dictionary<string,int>`), `TimedEffects` (`Dictionary<string,long>` = expiration en secondes Unix), `Claims` (`HashSet<string>`), `RecentOperations` (`List<string>`, ≤ `MaxRecentOperations = 50`), `PurchaseCount` ;
  - `const string WalletState.Key = "WALLET"` ;
  - Écart assumé avec la spec : ses champs `infiniteHeartExpiresAtUnixSec` et `freeCoinPackClaimed` deviennent les génériques `TimedEffects["INFINITE_HEART"]` et `Claims` contenant `"FREE_COIN_PACK"`, parce que la bibliothèque ne doit contenir aucun nom Gem Hunter ;
  - `ProtectedStateInitializer(IProtectedStateStore, IClock)` :
    - `Task<bool> EnsureInitializedAsync(IExecutionContext ctx, Func<long, IReadOnlyDictionary<string, object>> initialState, CancellationToken ct)` : lit `WALLET` ; s'il est absent, écrit en un batch sans writeLock `initialState(nowMs)`, qui doit contenir `WALLET` avec `CreatedAtUnixMs = nowMs`. Renvoie `true` si cet appel a créé l'état ;
  - `StateTransaction.RunAsync` lit toujours `WALLET` en plus des clés demandées :
    - absent → `GameOperationException(RETRY_LATER, "state not initialized")` ;
    - `now < CreatedAtUnixMs + GracePeriod` → `RETRY_LATER` ;
    - `SchemaVersion` inconnue → `CONFIGURATION_ERROR`.
  - Si le spike a établi une création conditionnelle, `EnsureInitializedAsync` l'utilise et `GracePeriod` vaut `TimeSpan.Zero` (ruling à consigner).

- [ ] **Step 1 : tests qui échouent :**
  - `Ensure_WalletAbsent_CreatesAllKeysInOneBatch`
  - `Ensure_WalletPresent_DoesNotWrite`
  - `Run_WalletAbsent_ThrowsRetryLater`
  - `Run_DuringGracePeriod_ThrowsRetryLater`
  - `Run_AtExactEndOfGracePeriod_IsAccepted`
  - `Run_UnknownSchemaVersion_ThrowsConfigurationError`
  - `ConcurrentInitWithMutationDuringGrace_MutationRejectedAndNothingLost` : deux initialisations entrelacées sur le faux store, une mutation tentée entre les deux → refusée ; l'état final est un état initial valide.
- [ ] **Step 2 :** échec. **Step 3 :** implémenter. **Step 4 :** tout passe.
- [ ] **Step 5 : commit** « Initialize protected player state with a creation grace period ».

### Task 6 : Règles du portefeuille, registre des achats et catalogues

**Files :**
- dans la bibliothèque, `Wallet/` : `WalletCatalog.cs`, `WalletRules.cs`, `Grant.cs` ;
- `Purchases/` : `PurchaseCatalog.cs`, `PurchaseLedger.cs`, `PurchaseRecord.cs`, `TransactionLogId.cs` (déplacé depuis le module) ;
- `Config/` : `RemoteConfigReader.cs` ;
- et les tests de chacun.

**Interfaces (Produces) :**
- `record Grant(string Id, long Amount)`.
- `WalletCatalog` :
  - lu depuis le JSON `{currencies:{ID:{initial}}, items:{ID:{initial}}, timedEffects:{ID:{durationSeconds}}}` ;
  - `ResourceKind KindOf(string id)` (Currency, Item, TimedEffect, Unknown).
- `WalletRules` (statique, pur) :
  - `WalletState Initial(WalletCatalog c, long nowMs)` ;
  - `void Credit(WalletState s, IEnumerable<Grant> grants, WalletCatalog c, long nowSec)` :
    - un effet temporisé de N unités prolonge l'expiration de `N × durationSeconds` à partir de `max(now, expiration)` ;
    - un id inconnu → `CONFIGURATION_ERROR` ;
  - `void Spend(WalletState s, string currencyId, long amount)` : solde insuffisant → `INSUFFICIENT_FUNDS` ;
  - `bool TryRecordOperation(WalletState s, string operationId)` : `false` si l'id est déjà présent. Les 50 plus récents sont gardés (FIFO) ; un `operationId` vide → `CONFIGURATION_ERROR` ;
  - `bool TryClaim(WalletState s, string claimId)`.
- `PurchaseCatalog` :
  - lu depuis `{products:[{catalogId, type, storeIds:{google, apple}, grants:[{id, amount}]}]}` ;
  - `Product? FindByStoreProductId(string store, string storeProductId)` ;
  - `Product? FindByCatalogId(string catalogId)`.
- `PurchaseLedger` :
  - `string KeyFor(string transactionId)` = `"PURCHASE_" + sha256 hex minuscule` ;
  - `const int MaxRecords = 1900` ;
  - `PurchaseOutcome Apply(StateSnapshot snap, WalletState wallet, Product product, string store, string transactionId, WalletCatalog c, long nowMs, out IReadOnlyDictionary<string, object> writes)` :
    - clé déjà présente → `AlreadyDelivered` sans écriture ni recalcul ;
    - `wallet.PurchaseCount >= MaxRecords` → `CONFIGURATION_ERROR` ;
    - sinon crédit, `PurchaseCount + 1`, enregistrement `PurchaseRecord {SchemaVersion=1, Store, ProductId, Grants, DeliveredAtUnixMs}` → `Delivered`.
- `RemoteConfigReader(IGameApiClient api, IClock clock)` :
  - `Task<JToken> GetAsync(IExecutionContext ctx, string key, CancellationToken ct)` via `RemoteConfigSettings.AssignSettingsGetAsync(ctx, ctx.AccessToken, ctx.ProjectId, ctx.EnvironmentId, "settings", new List<string>{key}, null)` ;
  - cache de 60 s indexé par `(EnvironmentId, key)` ;
  - clé absente → `CONFIGURATION_ERROR`.

- [ ] **Step 1 : tests qui échouent :**
  - `Credit_CurrencyAndItem_AddsAmounts`
  - `Credit_TimedEffect_ExtendsFromLaterOfNowAndExpiry`
  - `Credit_UnknownId_ThrowsConfigurationError`
  - `Spend_Insufficient_ThrowsAndLeavesState`
  - `TryRecordOperation_Duplicate_ReturnsFalse`
  - `TryRecordOperation_Over50_EvictsOldest`
  - `Apply_NewTransaction_CreditsOnceAndWritesRecordAndWallet`
  - `Apply_SameTransactionTwice_SecondIsAlreadyDeliveredWithoutWrite`
  - `Apply_After250Transactions_ReplayOfFirstGivesNoCredit`
  - `ApplyPurchase_AlreadyRecorded_IgnoresCurrentCatalog`
  - `Apply_AtMaxRecords_ThrowsConfigurationError`
  - `FindByStoreProductId_GooglePlayAndApple_ResolveSameProduct`
  - `GetAsync_TwoEnvironments_DoNotShareCache`
  - `GetAsync_WithinSixtySeconds_UsesCache`
  - `GetAsync_MissingKey_ThrowsConfigurationError`
  - `TransactionLogId` : mêmes tests que ceux du module.
  - **Concurrence** (avec `StateTransaction` et le faux store) :
    - `TwoConcurrentSpendsExceedingBalance_OnlyOneSucceeds` ;
    - `LostResponseThenRetry_PurchaseCreditedOnce`.
- [ ] **Step 2 :** échec. **Step 3 :** implémenter. Le module Gem Hunter référence désormais `TransactionLogId` de la bibliothèque (supprimer sa copie et son test). **Step 4 :** tout passe, module compris (`dotnet test UnityProject/GemHunterUGSCloud/Tests`).
- [ ] **Step 5 : commit** « Add wallet rules, durable purchase ledger and catalogs to the server library ».

### Task 7 : Règles de jeu Gem Hunter (pures)

**Files :**
- dans `UnityProject/GemHunterUGSCloud/Project/Game/` : `GemHunterKeys.cs`, `GemHunterTuning.cs`, `LevelRules.cs`, `AreaRules.cs`, `DailyRewardRules.cs`, `FreeCoinPackRules.cs`, `EconomyView.cs` ;
- tests `UnityProject/GemHunterUGSCloud/Tests/Game/*Tests.cs`.

**Interfaces :**
- Consumes : `WalletState`, `WalletRules`, `WalletCatalog`, `GameOperationException` (tâches 5-6).
- Produces :
  - `GemHunterKeys` : `PlayerData = "PLAYER_DATA"`, `DailyRewardsStatus = "DAILY_REWARDS_STATUS"` ;
  - `GemHunterTuning` : désérialisé des clés Remote Config `WALLET_DEFAULTS` (catalogue du portefeuille, cœurs et étoiles initiaux, gains du pack gratuit), `AREA_ITEMS` (id → nom, coût par niveau, niveau max) et des clés existantes `AREA_COMPLETE_REWARDS`, `DAILY_REWARDS_CONFIG` ;
  - `LevelRules.Won(PlayerData p)` : étoiles + 1, plafond 25 ;
  - `LevelRules.Lost(PlayerData p, WalletState w, long nowSec)` : cœurs − 1, plancher 0, **sauf** si `w.TimedEffects["INFINITE_HEART"] > nowSec` ;
  - `AreaRules.Unlock(PlayerData p, int areaId, int itemId, GemHunterTuning t)` : coût en étoiles, comportement actuel de `UpgradableAreaItemsService` ;
  - `AreaRules.Upgrade(PlayerData p, WalletState w, int areaId, int itemId, GemHunterTuning t)` : coût en pièces via `WalletRules.Spend`, niveau max ;
  - `AreaRules.ClaimCompletion(PlayerData p, WalletState w, GemHunterTuning t)` : récompense seulement si tous les objets de la zone courante sont au niveau max et si `AreaData.CompletionRewardClaimed` est faux (nouveau champ) ; sinon `CONFIGURATION_ERROR` « area not complete ». Autorité dérivée de l'état serveur ; la liste de commandes envoyée par le client n'est plus qu'informative ;
  - `DailyRewardRules.Status(...)` / `Claim(...)` : portage pur de `DailyRewardsStatusService.CalculateRewardStatus` et de `DailyRewardsClaimService.ClaimRewards`, horloge injectée ;
  - `FreeCoinPackRules.Claim(WalletState w, GemHunterTuning t)` : `TryClaim("FREE_COIN_PACK")` puis crédit ; déjà réclamé → état inchangé, renvoyé comme un succès ;
  - `EconomyView.From(WalletState w, WalletCatalog c) → PlayerEconomyData` : tous les objets du catalogue y compris à 0 ; `InfiniteHeartsExpiryTimestamp` en secondes ; `HasPurchasedFreeCoinPack`.
- Valeurs reprises à l'identique des fichiers Economy et du code actuel :
  - `COIN` initial 200 (100 de `COIN.ecc` + bonus de 100) ;
  - objets initiaux : `LARGE_BOMB` 1, `SMALL_BOMB` 3, `HORIZONTAL_ROCKET` 1, `VERTICAL_ROCKET` 1, `COLOR_BONUS` 1 ;
  - `INFINITE_HEART` 360 s par unité ;
  - pack gratuit : `COIN` 10 ;
  - cœurs initiaux 8, étoiles 5 ;
  - zones : Shark 20, Seahorse 40, Submarine 60, Treasure 80, Turtle 100, niveau max 5.

- [ ] **Step 1 : tests qui échouent**, un par règle et par cas de la spec :
  - victoire, plafond ;
  - défaite, plancher, cœur infini actif et expiré ;
  - déblocage sans étoiles ;
  - amélioration sans pièces, au niveau max ;
  - complétion incomplète, déjà réclamée ;
  - quotidien : premier jour, déjà réclamé aujourd'hui, fin de campagne ;
  - pack gratuit deux fois ;
  - `EconomyView` avec un objet à 0.
- [ ] **Step 2 :** échec. **Step 3 :** implémenter. **Step 4 :** tout passe.
- [ ] **Step 5 : commit** « Port Gem Hunter game rules to pure functions over protected state ».

### Task 8 : Fonctions Cloud Code sur la nouvelle base

**Files :**
- Modify : `Services/PlayerDataService.cs`, `Services/StoreService.cs`, `Services/UpgradableAreaItemsService.cs`, `Services/CommandBatchingService_AreaUpgradables.cs`, `Services/DailyRewardsClaimService.cs`, `Services/DailyRewardsStatusService.cs`, `ModuleConfig.cs`, `GemHunterUGSCloud.csproj` (référence à la bibliothèque, `purchase_catalog.json` en `EmbeddedResource`)
- Create : `Project/purchase_catalog.json`, `UnityProject/Assets/UGSConfigs/game_tuning.rc` (clés `WALLET_DEFAULTS`, `AREA_ITEMS`)
- Delete : `Services/PlayerEconomyService.cs`, `Purchases/PurchaseRedeemer.cs`, `Purchases/RedemptionStatusMapper.cs`, `Purchases/PurchaseRedemption*.cs`, `Models/EconomyConstants.cs`, et leurs tests (`Tests/Services/Inventory*`, `InfiniteHeartStateTests`, `FreeCoinPackTests`, `Tests/Purchases/PurchaseRedeemerTests.cs`, `RedemptionStatusMapperTests.cs`)

**Interfaces :**
- Consumes : tâches 4 à 7.
- Signatures Cloud Code finales :
  - `OnSignInHandlePlayerInitialization(ctx) → PlayerInitializationResponse` : appelle `EnsureInitializedAsync`, puis renvoie l'état ;
  - `GetPlayerData(ctx) → PlayerData?` ;
  - `GetPlayerEconomyData(ctx) → PlayerEconomyData?` ;
  - `HandleLevelWon(ctx, string operationId) → PlayerData?` ;
  - `HandleLevelLost(ctx, string operationId) → PlayerData?` ;
  - `HandleUnlock(ctx, int areaId, int itemId, string operationId) → PlayerData` ;
  - `HandleUpgrade(ctx, int areaId, int itemId, string operationId) → PlayerData` ;
  - `ProcessAreaCompleteCommandBatch(ctx, List<string> timeSignedCommandKeys, string operationId) → CommandReward` ;
  - `GetDailyRewardsStatus(ctx)`, `ClaimDailyReward(ctx) → DailyRewardsResult` ;
  - `HandlePurchase(ctx, string productId, string receipt, string transactionId) → PlayerEconomyData` ;
  - `HandleFreeCoinPackPurchase(ctx) → PlayerEconomyData`.
- `HandlePurchase` :
  - `StoreReceipt.TryParse` ;
  - store `fake` → `FakeStorePolicy` (hors `development` → `PURCHASE_REJECTED`), produit trouvé par `FindByCatalogId(productId)` ;
  - Google / Apple → `PURCHASE_RETRY_LATER` (validation : spec suivante) ;
  - puis `StateTransaction` avec la clé `PurchaseLedger.KeyFor(receipt.TransactionId)` ;
  - `Delivered` / `AlreadyDelivered` → `EconomyView`.
- `purchase_catalog.json` : les 8 produits actuels (`COIN_PACK_100/500/1000/5000/10000`, `BUNDLE_PACK`, `MEGA_PACK`), avec ids store `<prefix>_play` / `<prefix>_apple` et gains repris des `.ecr`.

- [ ] **Step 1 :** adapter ou réécrire les tests de façade existants (`StoreServiceTests`) pour la nouvelle base : faux store en `development` livré une fois, faux store en `production` refusé, vrai reçu → `PURCHASE_RETRY_LATER`, reçu illisible → `PURCHASE_REJECTED`. Ajouter `PurchaseCatalogFileTests.AllClientProductIds_ExistInCatalog` : il lit `purchase_catalog.json` et les ids de `UnityProject/Assets/GemHunterUGS/Scripts/Store/GemHunterProductCatalog.cs` par expression régulière sur `"<prefix>"`.
- [ ] **Step 2 :** échec. **Step 3 :** implémenter et supprimer les fichiers listés. `grep -rn "Economy" Project --include=*.cs` → seulement des noms de type du jeu (`EconomyView`, `PlayerEconomyData`), aucun appel `EconomyCurrencies` / `EconomyInventory` / `EconomyPurchases`.
- [ ] **Step 4 :** `dotnet build Project -c Release` → 0 erreur ; `dotnet test Tests` et les tests de la bibliothèque → tout passe.
- [ ] **Step 5 : commit** « Rebuild kept Cloud Code functions on the protected wallet; remove Economy calls ».

### Task 9 : Retrait des fonctions serveur hors périmètre

**Files :**
- Delete : `Services/LootBoxService.cs`, `LootBoxCooldownService.cs`, `FriendsService.cs`, `PlayerGiftsService.cs`, `GiftHeartCooldownService.cs`, `AllPlayersService.cs`, `HandleProfileChangeService.cs`, `DailyRewardsMonthlyResetService.cs`, leurs modèles devenus orphelins (`Models/LootBoxResult.cs`, `Models/Friends.cs`, `Models/Rewards.cs` s'il n'est plus utilisé)
- Modify : `ModuleConfig.cs` ; `PlayerData` sans `GiftHearts` ni `DisplayName` ; `SyncAllPlayerData` supprimée (jamais appelée par le client)

- [ ] **Step 1 :** pour chaque type supprimé, `grep` dans `Project/` et `Tests/` : plus aucune référence.
- [ ] **Step 2 :** build Release (0 erreur), tests (tout passe).
- [ ] **Step 3 : commit** « Remove out-of-scope Cloud Code features (loot box, friends, gifts, leaderboard, profile) ».

### Task 10 : Client — bindings, `operationId`, achats réels, retraits

**Files :**
- Regenerate : `UnityProject/Assets/CloudCode/GeneratedModuleBindings/GemHunterUGSCloud/**` (jamais édité à la main)
- Modify : appelants de `HandleLevelWon`, `HandleLevelLost`, `HandleUnlock`, `HandleUpgrade`, `ProcessAreaCompleteCommandBatch` (`Guid.NewGuid().ToString("N")` par action du joueur, réutilisé si l'appel est relancé) ; `GameInitializer` (composition) ; `StoreView` / `StoreClient` (achats réels masqués si `!Debug.isDebugBuild && !Application.isEditor`, via un champ sérialisé `m_ShowRealPurchasesInRelease = false`)
- Delete (skill `unity-asset-safety`, recherche de références avant chaque suppression) : `Scripts/LootBoxWithCooldown/`, `Scripts/Friends/`, `Scripts/EditProfile/`, les parties correspondantes de `PlayerHub`, et les connexions Facebook et Game Center par défaut ; `Assets/UGSConfigs/*.ecr|*.ecc|*.eci|*.ecv` et leurs `.meta` ; package `com.unity.services.economy` (via `UnityEditor.PackageManager.Client.Remove` en eval)

- [ ] **Step 1 :** régénérer les bindings depuis le `.ccmr`. Méthode à vérifier sur la version installée du package Cloud Code (2.10.4) : bouton « Generate Code Bindings » de l'inspecteur, déclenché par eval. Si aucune API n'est accessible, demander au propriétaire de cliquer.
- [ ] **Step 2 :** adapter les appelants et ajouter le masquage des achats réels ; `recompile` → 0 erreur.
- [ ] **Step 3 :** retirer les dossiers et assets listés, un groupe à la fois. Pour chaque groupe :
  - `grep` des noms de types et des GUID des scripts dans `Assets/**/*.unity|*.prefab|*.asset` ;
  - références de scène retirées par l'éditeur (eval sur les objets concernés), puis sauvegarde de la scène ;
  - `recompile` → 0 erreur, 0 « Missing Script » dans la console à l'ouverture des scènes du hub.
- [ ] **Step 4 :** retirer le package Economy et les fichiers `.ec*` ; `recompile` → 0 erreur. Restaurer les fichiers re-sérialisés hors périmètre (`git checkout --`).
- [ ] **Step 5 : commits séparés** :
  - « Regenerate Cloud Code bindings and pass operation ids » ;
  - « Hide real-money purchases outside development builds » ;
  - un commit par groupe retiré ;
  - « Remove Unity Economy package and configuration files ».

### Task 11 : Déploiement en `development`, intégration, documentation

**Files :**
- Modify : `Doc/backend-achats.md` (réécrit), `Doc/dependencies.md` (Economy retiré, bibliothèque serveur), `CLAUDE.md` (ligne d'organisation `SharedPackages/Server/`), `SharedPackages/com.devilcorp.mobilefoundation/README.md` (garanties backend), `Doc/decisions/0008-remplacement-economy.md` (statut : acceptée, persistance retenue), diagnostic (§8 états mis à jour)

- [ ] **Step 1 :** environnement actif de la fenêtre Deployment = `development`. Déployer le `.ccmr` et `game_tuning.rc`. Attendu : succès. Vérifier que le module publié contient `DevilCorp.MobileFoundation.Server.dll` ; sinon, ajouter la bibliothèque à `GemHunterUGSCloud.sln` et redéployer (ruling à consigner).
- [ ] **Step 2 : parcours en Play** (Unity CLI, nouveau joueur : `AuthenticationService.Instance.ClearSessionToken()` avant la connexion). Lire la console à chaque étape :
  1. nouveau joueur → soldes initiaux (200 pièces, objets listés) ;
  2. mutation dans les 30 s → `RETRY_LATER` ;
  3. après 30 s, achat faux store `COIN_PACK_100` → +100, puis même transaction rejouée par eval → pas de second crédit ;
  4. pack gratuit deux fois → +10 une seule fois ;
  5. victoire → étoile + 1 ; défaite → cœur − 1 ;
  6. amélioration de zone → pièces débitées ;
  7. quotidien réclamé, puis réclamé à nouveau → refus.
- [ ] **Step 3 :** écriture protected avec un jeton client (comme au spike) → refus.
- [ ] **Step 4 :** documentation listée ci-dessus.
- [ ] **Step 5 : commit** « Document protected wallet backend and record development integration results ».

## Hors de ce plan

- Validation Apple / Google, liaison au joueur, achats permanents, restauration, remboursements (spec suivante).
- Déploiement en `production` : sur instruction explicite seulement.
- Migration Unity 6.7 LTS + Cloud Code 3.0 + .NET 10, puis adaptateur Stateful Cloud Code.
