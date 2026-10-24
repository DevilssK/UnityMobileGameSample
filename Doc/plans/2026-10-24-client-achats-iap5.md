# Client d'achat Unity IAP 5 — plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal :** remplacer le client d'achat Unity IAP 4 du sample par un service d'achat IAP 5 du socle, sûr sur tous les chemins (échec, annulation, attente, reprise), consommé par Gem Hunter via un contrat.

**Architecture :** trois couches. (1) **Cœur** C# pur dans le package commun (`DevilCorp.MobileFoundation.Purchases`, sans `UnityEngine`) : contrats et `PurchaseCoordinator`, la machine à états testée par `dotnet test`. (2) **Adaptateur** `UnityIapStoreGateway` (asmdef séparée, dépend d'Unity IAP 5) qui traduit `StoreController` vers le contrat `IStoreGateway`. (3) **Hôte** Gem Hunter : `CloudCodePurchaseFulfillment` (appel `HandlePurchase`, propre au jeu) et `StoreClient` réduit à un pont UI ↔ `IPurchaseService`. Le service est créé au démarrage du jeu, pas dans la scène du hub, pour reprendre les achats payés en attente.

**Tech Stack :** Unity `6000.6.7f1`, Unity IAP `5.4.4`, package UPM local (`file:`), xUnit en `net10.0` avec `LangVersion 9` pour rester dans le C# supporté par Unity.

**Spec :** `Doc/CLAUDE_UNITY_MOBILE_FOUNDATION.md` §6.2-6.3, §7 (asynchronisme), §8.2-8.4, §12 ; décisions 0001, 0003, 0005 ; skills `monetization-change`, `unity:implement-in-app-purchases` (références `migration-v4-to-v5.md`, `codeless-catalog.md`).

## Global Constraints

- Le cœur (`Runtime/Purchases/`) n'a aucune référence à `UnityEngine`, à Unity IAP, à UGS ni à un type Gem Hunter (asmdef `noEngineReferences: true`).
- C# 9 maximum dans le package (pas de `required`, pas de file-scoped namespace, pas de raw strings).
- Code du package : conventions du brief §7 (`_camelCase`, Allman, 4 espaces, suffixe `Async`). Fichiers hôte modifiés : conventions existantes (`m_`, `k_`).
- Ne jamais journaliser `Receipt` : `StorePurchase.ToString()` l'exclut ; logs = `ProductId`, `TransactionId`, résultat.
- Flux v5 obligatoire : événements abonnés **avant** `Connect()`, `ConfirmPurchase(PendingOrder)` seulement après `Delivered`, `OnPurchaseDeferred` sans attribution.
- Aucune attribution côté client : le client crédite uniquement l'affichage à partir de la réponse serveur (`PlayerEconomyData`).
- `StoreClient` garde son nom de classe, son fichier et ses champs sérialisés existants (référencé dans `PlayerHub.unity`).
- `manifest.json` est commité sans les ajouts locaux AI Assistant (`com.unity.ai.*`) ni `com.unity.pipeline`.

## Faits vérifiés (package installé `com.unity.purchasing` 5.4.4)

- `UnityIAPServices.StoreController()` ; événements `OnStoreConnected`, `OnStoreDisconnected(StoreConnectionFailureDescription)`, `OnProductsFetched(List<Product>)`, `OnProductsFetchFailed(ProductFetchFailed)`, `OnPurchasePending(PendingOrder)`, `OnPurchaseConfirmed(Order)`, `OnPurchaseFailed(FailedOrder)`, `OnPurchaseDeferred(DeferredOrder)`, `OnPurchasesFetched(Orders)`, `OnPurchasesFetchFailed(PurchasesFetchFailureDescription)` ; méthodes `Task Connect()`, `FetchProducts(List<ProductDefinition>, IRetryPolicy? = null)`, `FetchPurchases()`, `PurchaseProduct(Product)`, `ConfirmPurchase(PendingOrder)`, `GetProductById(string)`.
- `order.Info.Receipt` reste l'enveloppe unifiée `{"Store","TransactionID","Payload"}` (`UnifiedReceiptFormatter`) ; Google : `Payload` = `{json, signature, skuDetails}` ; Apple : `Payload` = reçu d'application ; noms de store `GooglePlay`, `AppleAppStore`, `fake`. **Le parsing du backend reste valide** (hypothèse 1 de `Doc/backend-achats.md` vérifiée dans le code source).
- `Assets/Resources/IAPProductCatalog.json` a `enableCodelessAutoInitialization: true` avec des IDs (`bundle_pack`) différents de ceux du code (`BUNDLE_PACK`) : course entre deux initialisations (vue dans le log : `CodelessIAPStoreListener`).
- Erreur d'une fonction Cloud Code → HTTP 422 → `CloudCodeException` (`Reason = ScriptError`).
- Défauts actuels confirmés : `m_IsPurchaseInProgress` non remis à zéro sur échec ; boutons désactivés au clic et jamais réactivés ; reçu complet journalisé ; `StoreClient` n'existe que dans `PlayerHub` ; le pack gratuit côté client ne fait rien.

## Hypothèses à confirmer (éditeur / sandbox)

1. Le message de `CloudCodeException` contient le texte de l'exception serveur (`PURCHASE_REJECTED: …`). Sinon, tout refus serveur est traité en `RetryLater` (prudent : rien n'est confirmé).
2. Les noms des valeurs de `PurchaseFailureReason` utilisées par l'adaptateur (à relire dans le package avant la tâche 3).

## Review Focus

1. Achat lancé alors que le joueur n'est pas connecté (session expirée) → `RetryLater`, achat gardé en attente, pas de confirmation (test tâche 2 via `FulfillAsync` qui renvoie `RetryLater`, et tâche 4 pour l'hôte).
2. Même `PendingOrder` livré deux fois par le store pendant que la première validation est en cours → une seule requête serveur (test tâche 2).
3. Fermeture de la boutique (annulation de l'attente UI) pendant la validation → la livraison et la confirmation continuent (test tâche 2).
4. `FulfillAsync` qui lève une exception → `RetryLater`, UI débloquée (test tâche 2).
5. Achat différé (Ask-to-Buy) puis approuvé plus tard → aucune attribution avant l'arrivée du `PendingOrder` (test tâche 2).

## Structure des fichiers

| Fichier | Rôle |
| --- | --- |
| `SharedPackages/com.devilcorp.mobilefoundation/package.json` | Package UPM `0.1.0`, dépend de `com.unity.purchasing` `5.4.4` |
| `…/Runtime/Purchases/DevilCorp.MobileFoundation.Purchases.asmdef` | Cœur, `noEngineReferences: true` |
| `…/Runtime/Purchases/PurchaseOutcome.cs`, `PurchaseResult.cs`, `StorePurchase.cs`, `StoreFailure.cs`, `FulfillmentStatus.cs` | Types du contrat |
| `…/Runtime/Purchases/IPurchaseService.cs`, `IStoreGateway.cs`, `IPurchaseFulfillment.cs` | Contrats |
| `…/Runtime/Purchases/PurchaseCoordinator.cs` | Machine à états |
| `…/Runtime/Purchases.UnityIap/DevilCorp.MobileFoundation.Purchases.UnityIap.asmdef` | Adaptateur (réf. `UnityEngine.Purchasing`) |
| `…/Runtime/Purchases.UnityIap/StoreProductDefinition.cs`, `UnityIapStoreGateway.cs` | Catalogue et adaptateur IAP 5 |
| `SharedPackages/Tests/MobileFoundation.Purchases.Tests/` | Tests `dotnet` du cœur (hors du package, Unity ne l'importe pas) |
| `UnityProject/Packages/manifest.json` | `"com.devilcorp.mobilefoundation": "file:../../SharedPackages/com.devilcorp.mobilefoundation"` |
| `UnityProject/Assets/GemHunterUGS/Scripts/Store/CloudCodePurchaseFulfillment.cs` | Validation serveur propre à Gem Hunter |
| `UnityProject/Assets/GemHunterUGS/Scripts/Store/GemHunterProductCatalog.cs` | Les 7 produits et leurs IDs Google/Apple |
| `UnityProject/Assets/GemHunterUGS/Scripts/Store/StoreClient.cs` | Pont UI ↔ `IPurchaseService` (réécrit) |
| `UnityProject/Assets/GemHunterUGS/Scripts/Store/StoreView.cs` | `+ ActivatePurchaseButton(Button)` |
| `UnityProject/Assets/GemHunterUGS/Scripts/Core/GameInitializer.cs` | Création, enregistrement et démarrage du service |
| `UnityProject/Assets/Resources/IAPProductCatalog.json` | `enableCodelessAutoInitialization: false` |

---

### Task 1 : Squelette du package et banc de test du cœur

**Files :** Create `package.json`, les deux `.asmdef`, `SharedPackages/Tests/MobileFoundation.Purchases.Tests/MobileFoundation.Purchases.Tests.csproj` (`net10.0`, `LangVersion 9`, `Nullable enable`, xUnit, `<Compile Include="../../com.devilcorp.mobilefoundation/Runtime/Purchases/**/*.cs" />`), `Tests/PackageSmokeTests.cs` ; Modify `manifest.json` (dépendance `file:`), `.gitignore` racine (bin/obj déjà couverts).

- [ ] **Step 1 :** écrire `PackageSmokeTests.PurchaseOutcome_HasDeliveredValue` (référence `DevilCorp.MobileFoundation.Purchases.PurchaseOutcome.Delivered`) ; `dotnet test` → échec de compilation attendu.
- [ ] **Step 2 :** créer le package, l'enum `PurchaseOutcome { Delivered, Cancelled, Failed, Deferred, Rejected, RetryLater, Unavailable, AlreadyInProgress }` ; `dotnet test` → vert.
- [ ] **Step 3 :** ajouter la dépendance `file:` au manifest ; via MCP, `recompile` puis lecture du log : le package est importé, 0 erreur ; Unity génère les `.meta` du package (à commiter).
- [ ] **Step 4 : commit** — `Add mobile foundation package skeleton with purchase outcome contract` (manifest commité sans les lignes locales AI/Pipeline).

### Task 2 : `PurchaseCoordinator` (TDD)

**Interfaces (namespace `DevilCorp.MobileFoundation.Purchases`) :**

```csharp
public sealed class StorePurchase { string ProductId; string TransactionId; string Receipt; /* ToString sans Receipt */ }
public enum StoreFailure { Cancelled, Failed, Unavailable }
public enum FulfillmentStatus { Delivered, Rejected, RetryLater }
public sealed class PurchaseResult { string ProductId; PurchaseOutcome Outcome; string Message; }

public interface IStoreGateway
{
    bool IsReady { get; }
    event Action<StorePurchase> PurchasePending;          // nouvel achat ou reprise au démarrage
    event Action<string, StoreFailure> PurchaseFailed;    // productId, cause
    event Action<string> PurchaseDeferred;                // productId
    void Purchase(string productId);
    void Confirm(string transactionId);
}
public interface IPurchaseFulfillment
{
    Task<FulfillmentStatus> FulfillAsync(StorePurchase purchase, CancellationToken cancellationToken);
}
public interface IPurchaseService
{
    bool IsReady { get; }
    bool IsPurchaseInProgress { get; }
    event Action<PurchaseResult> PurchaseCompleted;       // aussi pour les reprises sans action du joueur
    Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken);
    Task RetryPendingFulfillmentsAsync(CancellationToken cancellationToken);
}
public sealed class PurchaseCoordinator : IPurchaseService, IDisposable
{
    public PurchaseCoordinator(IStoreGateway store, IPurchaseFulfillment fulfillment, Action<string> log = null);
}
```

Règles : un seul achat initié à la fois ; un `PurchasePending` déclenche `FulfillAsync` une seule fois par `TransactionId` tant qu'il est en cours ; `Delivered` → `Confirm` puis `PurchaseCompleted(Delivered)` ; `Rejected` / `RetryLater` → pas de `Confirm` ; `RetryLater` garde l'achat dans une liste d'attente rejouée uniquement par `RetryPendingFulfillmentsAsync` (déclenchée par l'hôte au démarrage, à la connexion et à l'ouverture de la boutique : pas de boucle) ; une exception de `FulfillAsync` = `RetryLater` ; l'annulation du `CancellationToken` de `PurchaseAsync` termine l'attente de l'appelant (`Cancelled`) sans interrompre la validation en cours ; l'état « achat en cours » est relâché sur toute issue terminale.

- [ ] **Step 1 : tests qui échouent** (`PurchaseCoordinatorTests`, doublures manuelles `FakeStoreGateway` / `FakeFulfillment` qui savent échouer, différer, dupliquer et attendre via `TaskCompletionSource`) :

```
PurchaseAsync_WhenStoreNotReady_ReturnsUnavailable
PurchaseAsync_WhenAnotherPurchaseInProgress_ReturnsAlreadyInProgress
PurchaseAsync_WhenDelivered_ConfirmsOnceAndReturnsDelivered
PurchaseAsync_WhenStoreCancels_ReturnsCancelledAndAllowsNewPurchase
Purchase_AfterFailure_AllowsNewPurchase
PurchaseAsync_WhenDeferred_ReturnsDeferredWithoutFulfillment          // Review Focus 5
PurchaseAsync_WhenRejected_DoesNotConfirm
PurchaseAsync_WhenRetryLater_DoesNotConfirmAndKeepsPurchasePending   // Review Focus 1
RetryPendingFulfillmentsAsync_WhenNowDelivered_ConfirmsOnce
PendingPurchaseAtStartup_IsFulfilledAndConfirmedWithoutPlayerAction
PendingPurchase_WhenRedeliveredWhileInFlight_FulfillsOnce             // Review Focus 2
PurchaseAsync_WhenCallerCancelsDuringFulfillment_StillConfirms        // Review Focus 3
PurchaseAsync_WhenFulfillmentThrows_ReturnsRetryLaterAndReleases      // Review Focus 4
PurchaseCompleted_IsRaisedForEveryTerminalOutcome
StorePurchase_ToString_DoesNotContainReceipt
```

- [ ] **Step 2 :** `dotnet test SharedPackages/Tests/MobileFoundation.Purchases.Tests` → échec (types absents).
- [ ] **Step 3 :** implémenter types, contrats et `PurchaseCoordinator`.
- [ ] **Step 4 :** `dotnet test` → vert ; MCP `recompile` → 0 erreur Unity (le cœur compile aussi en C# Unity).
- [ ] **Step 5 : commit** — `Add purchase coordinator with recovery, dedup and terminal-state release`.

### Task 3 : Adaptateur `UnityIapStoreGateway` (IAP 5)

**Interfaces :** `public sealed class StoreProductDefinition { string ProductId; ProductType Type; string GooglePlayId; string AppleAppStoreId; }` ; `public sealed class UnityIapStoreGateway : IStoreGateway, IDisposable { UnityIapStoreGateway(IReadOnlyList<StoreProductDefinition> catalog, Action<string> log = null); Task ConnectAsync(); }`.

Comportement : abonnement à tous les événements avant `Connect()` ; sur `OnStoreConnected` → `FetchProducts` (IDs via `StoreSpecificIds`) puis `FetchPurchases()` (reprise des commandes non confirmées) ; `IsReady` vrai après `OnProductsFetched` ; `OnPurchasePending` → mémorise `PendingOrder` par `TransactionID` puis lève `PurchasePending(new StorePurchase(productId, order.Info.TransactionID, order.Info.Receipt))` ; `Confirm(transactionId)` → `ConfirmPurchase(pendingOrder)` ; `OnPurchaseFailed` → `StoreFailure` (annulation utilisateur → `Cancelled`, indisponibilité → `Unavailable`, sinon `Failed`, d'après les valeurs réelles de `PurchaseFailureReason`) ; `OnPurchaseDeferred` → `PurchaseDeferred` ; `OnPurchaseConfirmed` `FailedOrder` → log d'avertissement ; produit inconnu → `PurchaseFailed(Unavailable)`.

- [ ] **Step 1 :** relire dans le package `PurchaseFailureReason`, `PendingOrder.CartOrdered`, `Order.Info` ; noter les noms exacts.
- [ ] **Step 2 :** implémenter ; MCP `recompile` → 0 erreur, 0 avertissement `CS0618` dans les fichiers du package.
- [ ] **Step 3 : commit** — `Add Unity IAP 5 store gateway`.

(Pas de test automatisé : l'adaptateur ne fait que traduire l'API du SDK ; vérification dans l'éditeur à la tâche 4.)

### Task 4 : Intégration Gem Hunter

**Files :** Create `CloudCodePurchaseFulfillment.cs`, `GemHunterProductCatalog.cs` ; Modify `StoreClient.cs`, `StoreView.cs`, `GameInitializer.cs`, `IAPProductCatalog.json`.

- `CloudCodePurchaseFulfillment : IPurchaseFulfillment` (ctor `CloudBindingsProvider`, `PlayerEconomyManagerClient`, `PlayerAuthenticationManager`) : pas connecté → `RetryLater` ; appel `GemHunterBindings.HandlePurchase(ProductId, Receipt, TransactionId)` ; succès → `HandleEconomyUpdate(data)` puis `Delivered` ; `CloudCodeException` dont le message contient `PURCHASE_REJECTED` → `Rejected` ; toute autre exception → `RetryLater`.
- `GemHunterProductCatalog.All` : `BUNDLE_PACK`/`bundle_pack_play`/`bundle_pack_apple`, `MEGA_PACK`, `COIN_PACK_100…10000` (mêmes IDs que l'actuel `StoreClient`, tous `Consumable`).
- `GameInitializer` : crée gateway + coordinator, enregistre `IPurchaseService` dans `GameSystemLocator`, lance `ConnectAsync()` après l'initialisation des services, appelle `RetryPendingFulfillmentsAsync` sur `PlayerAuthenticationManager.SignedIn`.
- `StoreClient` : plus aucune référence à `UnityEngine.Purchasing` ; clic → `PurchaseAsync` ; `Delivered` → popup de succès ; autre issue → `ActivatePurchaseButton` + log court ; `OnDisable` annule l'attente (sans annuler la livraison) ; pack gratuit → `HandleFreeCoinPackPurchase()` + `HandleEconomyUpdate` ; ouverture de la boutique → `RetryPendingFulfillmentsAsync`.
- `IAPProductCatalog.json` : `"enableCodelessAutoInitialization": false`.

- [ ] **Step 1 :** implémenter ; MCP `recompile` → 0 erreur ; `grep` : plus de `UnityEngine.Purchasing` hors du package, plus de log de reçu.
- [ ] **Step 2 : vérification éditeur (MCP)** : Play depuis `InitGemHunterUGS` → hub → boutique → achat `COIN_PACK_100` avec le faux store. Attendu en environnement `production` : refus serveur (`fake` refusé) ou `RetryLater` si le module déployé n'est pas à jour ; dans les deux cas bouton réactivé, aucune exception, aucun `CodelessIAPStoreListener` dans le log, achat non confirmé. Captures à l'appui.
- [ ] **Step 3 : commit** — `Route Gem Hunter store through the foundation purchase service`.

### Task 5 : Vérification finale et documentation

- [ ] `dotnet test` du cœur et du backend (verts), recompilation Unity (0 erreur).
- [ ] Note de décision `0006` (création du package commun dès maintenant, nom provisoire, découpe cœur/adaptateur/hôte) ; `Doc/dependencies.md` (package `0.1.0`) ; `Doc/backend-achats.md` (hypothèse 1 vérifiée dans le code d'IAP 5.4.4).
- [ ] **commit** — `Document client purchase flow and foundation package`.

## Hors de ce plan

Restauration explicite iOS (`RestoreTransactions`, pas de produit permanent au catalogue actuel), `AppAccountToken`/`ObfuscatedAccountId`, environnement `production` en dur (P1), pubs LevelPlay, extraction des autres modules du socle.
