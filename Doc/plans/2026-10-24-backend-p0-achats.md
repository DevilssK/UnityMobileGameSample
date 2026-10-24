# Backend P0 — achats et récompenses : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal :** supprimer les failles serveur P0 du flux d'achat sans changer le contrat réseau consommé par le client actuel.

**Architecture :** Economy redeem devient le **seul** propriétaire de l'attribution des achats réels (décision 0001) : le serveur ne crédite plus rien lui-même. Un petit module `Purchases/` (parsing du reçu, politique d'environnement, correspondance des statuts, orchestrateur) est testable sans UGS ; `StoreService` n'est plus qu'une façade Cloud Code. Le service de récompense publicitaire par jeton client est retiré.

**Tech Stack :** C# Cloud Code module `net6.0` (inchangé ici, montée à part — décision 0003), `Com.Unity.Services.CloudCode.Apis 0.0.16`, tests xUnit + NSubstitute en `net10.0` (seul runtime installé).

**Spec :** `Doc/CLAUDE_UNITY_MOBILE_FOUNDATION.md` §5, §8, §12 ; décisions `Doc/decisions/0001` à `0004` ; skill `monetization-change`.

## Global Constraints

- Contrat réseau inchangé : `HandlePurchase(IExecutionContext, string productId, string receipt, string transactionId) -> PlayerEconomyData` et `HandleFreeCoinPackPurchase(IExecutionContext) -> PlayerEconomyData` gardent nom et signature (les bindings client générés ne peuvent pas être régénérés sans Unity).
- Une transaction valide = une seule attribution logique. Aucun appel `EconomyCurrencies`/`EconomyInventory` en écriture dans le chemin d'achat réel.
- Aucun chemin `fake` n'attribue quoi que ce soit, dans aucun environnement. Il n'est accepté (sans attribution) que si `context.EnvironmentName` vaut exactement `development` (comparaison ordinale, insensible à la casse).
- Pas de reçu, payload, signature ni jeton dans les logs. Logger `context.CorrelationId`, `productId`, store, statut.
- Nouveaux fichiers : conventions du brief §7 (`_camelCase`, constantes PascalCase, Allman, 4 espaces). Fichiers existants modifiés : garder leurs conventions (`m_`, `k_`).
- Tests : `Méthode_Situation_Résultat`. Pas de dépendance UGS réelle ; les doublures doivent savoir échouer et dupliquer.
- Fichiers hors `Assets/` : pas de `.meta`. `UnityProject/.gitignore` ignore `*.csproj`/`*.sln` : ajouter une exception pour le projet de tests.
- Ne pas ajouter le projet de tests à `GemHunterUGSCloud.sln` (la solution est référencée par `Assets/GemHunterUGS/Scripts/GemHunterUGSCloud.ccmr` pour le déploiement).

## Faits vérifiés (API installée, réflexion sur `Unity.Services.CloudCode.Apis.dll` 0.0.16)

- `PlayerPurchaseGoogleplaystoreRequest { Id, PurchaseData, PurchaseDataSignature, LocalCost, LocalCurrency }` ; `PlayerPurchaseAppleappstoreRequest { Id, Receipt, LocalCost, LocalCurrency }`.
- Réponses : `Verification.Status` ∈ `VALID, VALIDNOTREDEEMED, INVALIDALREADYREDEEMED, INVALIDVERIFICATIONFAILED, INVALIDANOTHERPLAYER, INVALIDCONFIGURATION, INVALIDPRODUCTIDMISMATCH` (un enum distinct par store, mêmes noms).
- `ApiException(ApiExceptionType type, string message, IApiResponse response)`, `ApiExceptionType ∈ { InvalidParameters, Network, Http, Deserialization }` ; `ApiResponse<T>` a un constructeur vide et `StatusCode`, `Data`, `RawContent` settables.
- `InventoryResponse.Created.Date : DateTime?` ; `AddInventoryRequest(inventoryItemId, playersInventoryItemId, instanceData)`.
- `IExecutionContext` expose `EnvironmentName`, `CorrelationId`.
- Doc Economy : les statuts `INVALID_*` peuvent arriver **en réponse ou en exception** — gérer les deux.

## Hypothèses à confirmer en staging/sandbox (non vérifiables ici)

1. Format de reçu Unity IAP 4 : `{"Store","TransactionID","Payload"}` ; Google : `Payload` = JSON `{"json","signature",...}` ; Apple : `Payload` = reçu base64 ; éditeur : `Store == "fake"`.
2. `VALIDNOTREDEEMED` : traité comme `RetryLater` (on ne confirme pas au store ; prudent pour le joueur).
3. Deux redeems simultanés du même reçu : Economy n'en valide qu'un.
4. Economy renvoie `409 Conflict` sur un `playersInventoryItemId` déjà existant (verrou du pack gratuit).
5. Redeem d'un `.ecr` contenant `INFINITE_HEART` crée l'item sans `instanceData`.

## Changements de comportement visibles (à annoncer)

- Les récompenses viennent désormais **uniquement** des `.ecr` : le Mega Pack donne aussi 5 `VERTICAL_ROCKET` (absents de l'ancien `s_Rewards`), et les doubles crédits disparaissent.
- Achat en éditeur (fake store) : accepté en `development` mais **ne crédite rien** ; refusé ailleurs.
- Pubs récompensées : la fonction `HandleGrantVideoAdReward` disparaît ; le client actuel affichera un échec de récompense jusqu'à la réécriture client (récompense locale, décision 0001).

## Review Focus

1. Reçu `GooglePlay` dont le `Payload` n'est pas du JSON ou n'a pas de `signature` → `Rejected`, aucun appel Economy (test tâche 1 + 3).
2. `EnvironmentName` null ou vide (exécution locale) avec store `fake` → `Rejected` (test tâche 2).
3. Panne réseau / 5xx Economy → `RetryLater`, pas `Rejected`, et aucune attribution (test tâche 3).
4. Réponse perdue puis nouvel essai du même joueur, « déjà redeem » reçu **par exception** → `AlreadyDelivered` (test tâche 3).
5. Produit inconnu ou ne correspondant pas au reçu → `Rejected` (pas de boucle de reprise infinie côté client) (test tâche 2 + 3).

## Structure des fichiers

Racine : `UnityProject/GemHunterUGSCloud/`.

| Fichier | Rôle |
| --- | --- |
| `Project/Purchases/StoreKind.cs` | enum `GooglePlay, AppleAppStore, Fake, Unknown` |
| `Project/Purchases/StoreReceipt.cs` | Parsing de l'enveloppe Unity IAP → données attendues par Economy |
| `Project/Purchases/PurchaseRedemptionStatus.cs` | enum `Delivered, AlreadyDelivered, Simulated, Rejected, RetryLater` |
| `Project/Purchases/PurchaseRedemptionResult.cs` | record résultat + raison (sans donnée sensible) |
| `Project/Purchases/RedemptionStatusMapper.cs` | statut Economy / exception → `PurchaseRedemptionStatus` |
| `Project/Purchases/FakeStorePolicy.cs` | environnements où `fake` est accepté |
| `Project/Purchases/PurchaseRedeemer.cs` | orchestration : parse → politique → redeem → résultat |
| `Project/Services/StoreService.cs` | façade Cloud Code (modifiée) |
| `Project/Services/PlayerEconomyService.cs` | cœur infini sans `instanceData`, verrou du pack gratuit (modifié) |
| `Project/Services/AdRewardsService.cs` | supprimé |
| `Tests/GemHunterUGSCloud.Tests.csproj` + `Tests/**` | tests |

---

### Task 1 : Projet de tests et parsing du reçu

**Files :**
- Create : `Tests/GemHunterUGSCloud.Tests.csproj`, `Tests/Purchases/StoreReceiptTests.cs`
- Create : `Project/Purchases/StoreKind.cs`, `Project/Purchases/StoreReceipt.cs`
- Modify : `UnityProject/.gitignore` (ajouter `!GemHunterUGSCloud/Tests/GemHunterUGSCloud.Tests.csproj`)

**Interfaces :**
- Produces : `public enum StoreKind { Unknown, GooglePlay, AppleAppStore, Fake }` ;
  `public sealed class StoreReceipt { StoreKind Store; string TransactionId; string? GooglePurchaseData; string? GoogleSignature; string? AppleReceipt; static bool TryParse(string? receiptJson, out StoreReceipt? receipt) }`.
  `TryParse` renvoie `false` si le JSON est invalide, si `Store` est inconnu, ou si les champs requis par le store manquent (Google : `json` et `signature` ; Apple : `Payload` non vide). `Fake` n'exige rien d'autre que `Store`.

- [ ] **Step 1 : créer le projet de tests** — `net10.0`, xUnit, NSubstitute, `Microsoft.NET.Test.Sdk`, `ProjectReference` vers `../Project/GemHunterUGSCloud.csproj`. Ajouter l'exception `.gitignore`.
- [ ] **Step 2 : écrire les tests qui échouent**

```csharp
// Google : payload = JSON contenant json + signature
TryParse_WhenGooglePlayReceipt_ExtractsPurchaseDataAndSignature
    // receipt = {"Store":"GooglePlay","TransactionID":"GPA.1","Payload":"{\"json\":\"{\\\"orderId\\\":\\\"GPA.1\\\"}\",\"signature\":\"sig==\"}"}
    // => true, Store == GooglePlay, GooglePurchaseData == "{\"orderId\":\"GPA.1\"}", GoogleSignature == "sig==", TransactionId == "GPA.1"
TryParse_WhenAppleAppStoreReceipt_UsesPayloadAsReceipt          // "AppleAppStore", Payload "MIIT..." => AppleReceipt == "MIIT..."
TryParse_WhenStoreNameHasDifferentCase_IsRecognised             // "googleplay" => GooglePlay
TryParse_WhenFakeStore_ReturnsFake                              // "fake" => Fake
TryParse_WhenStoreIsLegacyApple_IsRejected                      // "apple" => false (ancienne valeur du sample)
TryParse_WhenGooglePayloadIsNotJson_IsRejected                  // Review Focus 1
TryParse_WhenGoogleSignatureMissing_IsRejected                  // Review Focus 1
TryParse_WhenReceiptIsNullEmptyOrInvalidJson_IsRejected         // [Theory] null, "", "not json"
```

- [ ] **Step 3 : vérifier l'échec** — `dotnet test UnityProject/GemHunterUGSCloud/Tests` → erreurs de compilation (`StoreReceipt` absent).
- [ ] **Step 4 : implémenter** `StoreKind` et `StoreReceipt.TryParse` avec Newtonsoft (`JObject`), noms de store comparés en `OrdinalIgnoreCase` (`GooglePlay`, `AppleAppStore`, `fake`).
- [ ] **Step 5 : vérifier** — tous les tests `StoreReceiptTests` passent ; `dotnet build` du module : 0 erreur.
- [ ] **Step 6 : commit** — `Add backend test project and Unity IAP receipt parsing`.

### Task 2 : Correspondance des statuts et politique d'environnement

**Files :**
- Create : `Project/Purchases/PurchaseRedemptionStatus.cs`, `PurchaseRedemptionResult.cs`, `RedemptionStatusMapper.cs`, `FakeStorePolicy.cs`
- Test : `Tests/Purchases/RedemptionStatusMapperTests.cs`, `Tests/Purchases/FakeStorePolicyTests.cs`

**Interfaces :**
- Produces : `public enum PurchaseRedemptionStatus { Delivered, AlreadyDelivered, Simulated, Rejected, RetryLater }` ;
  `public sealed record PurchaseRedemptionResult(PurchaseRedemptionStatus Status, string Reason)` ;
  `internal static class RedemptionStatusMapper { PurchaseRedemptionStatus FromGoogle(PlayerPurchaseGoogleplaystoreResponseVerification.StatusEnum s); PurchaseRedemptionStatus FromApple(PlayerPurchaseAppleappstoreResponseVerification.StatusEnum s); PurchaseRedemptionStatus FromHttpStatus(HttpStatusCode? code, ApiExceptionType type) }` ;
  `internal static class FakeStorePolicy { bool IsAllowed(string? environmentName) }`.
- Ajouter `[assembly: InternalsVisibleTo("GemHunterUGSCloud.Tests")]` dans le module (et `DynamicProxyGenAssembly2` pour NSubstitute si nécessaire).

Table de correspondance (identique pour les deux stores) :

| Economy | Résultat |
| --- | --- |
| `VALID` | `Delivered` |
| `INVALIDALREADYREDEEMED` | `AlreadyDelivered` |
| `VALIDNOTREDEEMED` | `RetryLater` (hypothèse 2) |
| `INVALIDANOTHERPLAYER`, `INVALIDVERIFICATIONFAILED`, `INVALIDCONFIGURATION`, `INVALIDPRODUCTIDMISMATCH` | `Rejected` |

`FromHttpStatus` : `Network` ou `Deserialization` → `RetryLater` ; `Http` avec 5xx ou 429 → `RetryLater` ; autre 4xx → `Rejected` ; `InvalidParameters` → `Rejected`.

- [ ] **Step 1 : tests qui échouent**

```csharp
FromGoogle_MapsEveryStatus            // [Theory] couvrant les 7 valeurs de la table
FromApple_MapsEveryStatus             // idem
FromHttpStatus_WhenNetworkError_ReturnsRetryLater        // Review Focus 3
FromHttpStatus_WhenServerErrorOrTooManyRequests_ReturnsRetryLater  // 500, 503, 429
FromHttpStatus_WhenClientError_ReturnsRejected           // 400, 404, 422 — Review Focus 5
IsAllowed_WhenDevelopment_ReturnsTrue                    // "development", "Development"
IsAllowed_WhenProductionOrOther_ReturnsFalse             // "production", "staging", "dev", "development2"
IsAllowed_WhenNullOrEmpty_ReturnsFalse                   // Review Focus 2
```

- [ ] **Step 2 : vérifier l'échec**, **Step 3 : implémenter** (switch exhaustifs ; une valeur d'enum inconnue → `Rejected`), **Step 4 : vérifier le succès**.
- [ ] **Step 5 : commit** — `Map Economy verification statuses and restrict fake store to development`.

### Task 3 : `PurchaseRedeemer` — propriétaire unique de l'attribution

**Files :**
- Create : `Project/Purchases/PurchaseRedeemer.cs`
- Test : `Tests/Purchases/PurchaseRedeemerTests.cs`

**Interfaces :**
- Consumes : tâches 1 et 2.
- Produces : `public class PurchaseRedeemer { PurchaseRedeemer(ILogger<PurchaseRedeemer> logger, IGameApiClient gameApiClient); Task<PurchaseRedemptionResult> RedeemAsync(IExecutionContext context, string productId, string receiptJson) }`.

Comportement : `TryParse` échoue → `Rejected`. `Fake` → `Simulated` si `FakeStorePolicy.IsAllowed(context.EnvironmentName)`, sinon `Rejected` ; dans les deux cas, aucun appel Economy. Google/Apple → appel `RedeemGooglePlayPurchaseAsync`/`RedeemAppleAppStorePurchaseAsync` avec `Id = productId` et les champs de `StoreReceipt` ; statut lu dans `response.Data.Verification.Status`. Sur `ApiException` : tenter de désérialiser `Response.RawContent` vers le type de réponse du store (champ `verification.status`) ; si impossible → `FromHttpStatus`. Aucune autre exception n'est avalée. Le chemin réel ne touche jamais `EconomyCurrencies` ni `EconomyInventory`. Logs : `CorrelationId`, `productId`, store, statut — jamais le reçu.

- [ ] **Step 1 : tests qui échouent** (doublures NSubstitute de `IGameApiClient.EconomyPurchases` ; un `IExecutionContext` substitué avec `EnvironmentName`, `PlayerId`, `CorrelationId`)

```csharp
RedeemAsync_WhenGoogleReceiptValid_SendsExtractedPayloadAndReturnsDelivered
    // Received: RedeemGooglePlayPurchaseAsync(..., Arg.Is<Request>(r => r.Id=="COIN_PACK_100" && r.PurchaseData=="{\"orderId\":\"GPA.1\"}" && r.PurchaseDataSignature=="sig=="), ...)
RedeemAsync_WhenAppleReceiptValid_SendsPayloadAndReturnsDelivered
RedeemAsync_WhenValid_NeverWritesCurrenciesOrInventory
    // gameApiClient.EconomyCurrencies et EconomyInventory : DidNotReceiveWithAnyArgs() sur toutes les écritures
RedeemAsync_WhenCalledTwiceBySamePlayer_ReturnsAlreadyDeliveredWithoutSecondGrant
    // 1er appel VALID, 2e appel INVALIDALREADYREDEEMED en réponse
RedeemAsync_WhenAlreadyRedeemedArrivesAsException_ReturnsAlreadyDelivered      // Review Focus 4
    // ApiException(Http, ..., ApiResponse{StatusCode=422, RawContent="{\"verification\":{\"status\":\"INVALID_ALREADY_REDEEMED\"}}"})
RedeemAsync_WhenReceiptBelongsToOtherPlayer_IsRejected
RedeemAsync_WhenConcurrentIdenticalRequests_ProducesOneDeliveredAndOneAlreadyDelivered
    // doublure qui renvoie VALID au premier appel puis ALREADY ; Task.WhenAll de deux appels ; jamais d'écriture currencies/inventory
RedeemAsync_WhenStoreIsFakeInProduction_IsRejectedWithoutCallingEconomy
RedeemAsync_WhenStoreIsFakeInDevelopment_ReturnsSimulatedWithoutCallingEconomy
RedeemAsync_WhenGooglePayloadMalformed_IsRejectedWithoutCallingEconomy          // Review Focus 1
RedeemAsync_WhenEconomyNetworkFailure_ReturnsRetryLater                         // Review Focus 3
RedeemAsync_WhenProductIdMismatch_IsRejected                                    // Review Focus 5
```

- [ ] **Step 2 : vérifier l'échec**, **Step 3 : implémenter**, **Step 4 : vérifier le succès**.
- [ ] **Step 5 : commit** — `Add PurchaseRedeemer with Economy redeem as single grant owner`.

### Task 4 : `StoreService` devient une façade

**Files :**
- Modify : `Project/Services/StoreService.cs` (supprimer `s_Rewards`, `BundleReward`, `CoinReward`, `ProductType`, `GrantRewards`, le `switch` des stores et le `catch` sur le message « purchase already redeemed »)
- Modify : `Project/ModuleConfig.cs` (enregistrer `PurchaseRedeemer` avant `StoreService`)
- Test : `Tests/Services/StoreServiceTests.cs`

**Interfaces :**
- Consumes : `PurchaseRedeemer.RedeemAsync` (tâche 3) — rendre la méthode `virtual` pour la substitution.
- Produces : `HandlePurchase` (signature inchangée). `Delivered`, `AlreadyDelivered`, `Simulated` → renvoie `m_PlayerEconomyService.GetPlayerEconomyData(context)` (le client confirme alors l'achat, ce qui est correct aussi pour `AlreadyDelivered`). `Rejected` → `throw new InvalidOperationException("PURCHASE_REJECTED: <Reason>")` ; `RetryLater` → `throw new InvalidOperationException("PURCHASE_RETRY_LATER: <Reason>")`. Le paramètre `transactionId` n'est plus utilisé pour la validation : seulement journalisé (identifiant de support, non sensible).

- [ ] **Step 1 : tests qui échouent**

```csharp
HandlePurchase_WhenRejected_ThrowsWithRejectedCodeAndDoesNotReadEconomy
HandlePurchase_WhenRetryLater_ThrowsWithRetryCode
HandlePurchase_WhenAlreadyDelivered_ReturnsEconomyData
```

- [ ] **Step 2 : vérifier l'échec**, **Step 3 : implémenter**, **Step 4 : vérifier le succès**.
- [ ] **Step 5 : commit** — `Make StoreService delegate purchase attribution to Economy redeem`.

### Task 5 : Cœur infini accordé par Economy redeem

L'item `INFINITE_HEART` créé par redeem n'a pas d'`instanceData` (hypothèse 5). Plutôt que d'ajouter une écriture après redeem (second chemin), le lire comme activé à sa date de création.

**Files :**
- Modify : `Project/Services/PlayerEconomyService.cs:588-669` (`CheckInfiniteHeartExpiration`, `ParseInfiniteHeartInstanceData`)
- Test : `Tests/Services/InfiniteHeartStateTests.cs`

**Interfaces :**
- Produces : `internal static (bool Active, long ActivatedAt)? ResolveInfiniteHeartState(object? instanceData, DateTime? createdAt)` dans `PlayerEconomyService`. Données valides → elles ; données absentes/illisibles et `createdAt` connu → `(true, createdAt en secondes Unix)` ; sinon `null`. `CheckInfiniteHeartExpiration` l'utilise avec `infiniteHeartItem.Created?.Date` et garde sa logique d'expiration.

- [ ] **Step 1 : tests qui échouent**

```csharp
ResolveInfiniteHeartState_WhenInstanceDataPresent_UsesIt          // {"active":false,"activatedAt":100} => (false, 100)
ResolveInfiniteHeartState_WhenInstanceDataMissing_UsesCreatedDate  // null, 2026-10-24T00:00:00Z => (true, 1792800000)
ResolveInfiniteHeartState_WhenNothingKnown_ReturnsNull
```

- [ ] **Step 2-4 :** échec, implémentation, succès.
- [ ] **Step 5 : commit** — `Treat redeemed infinite hearts without instance data as active from creation`.

### Task 6 : Pack gratuit sans double crédit

Actuellement : lecture → test → crédit → marquage, non atomique. Nouveau : **verrou d'abord** (création de l'item `COIN_PACK_FREE` avec l'identifiant fixe `COIN_PACK_FREE_CLAIM`), crédit ensuite. Un conflit = déjà réclamé. Une panne entre les deux fait perdre 10 pièces gratuites au joueur plutôt que de les doubler : risque accepté, documenté dans le code.

**Files :**
- Modify : `Project/Services/PlayerEconomyService.cs:706-736` (`MarkFreePackAsClaimed` → `TryClaimFreePackAsync`)
- Modify : `Project/Services/StoreService.cs` (`HandleFreeCoinPackPurchase`)
- Test : `Tests/Services/FreeCoinPackTests.cs`

**Interfaces :**
- Produces : `public async Task<bool> TryClaimFreePackAsync(IExecutionContext context)` — `AddInventoryItemAsync` avec `new AddInventoryRequest("COIN_PACK_FREE", "COIN_PACK_FREE_CLAIM", { claimedAt })` ; `ApiException` de statut 409 → `false` ; autre exception → relancée. Constante `FreePackClaimItemId = "COIN_PACK_FREE_CLAIM"`.
- `HandleFreeCoinPackPurchase` : si `HasPlayerClaimedFreePack` (couvre les anciens joueurs marqués avec un identifiant aléatoire) → renvoie l'état ; sinon `TryClaimFreePackAsync` → `false` : renvoie l'état sans crédit ; `true` : `UpdatePlayerCurrency(COIN, 10)` puis renvoie l'état.

- [ ] **Step 1 : tests qui échouent** (doublure `EconomyInventory` qui accepte le premier `AddInventoryItemAsync` avec `COIN_PACK_FREE_CLAIM` puis lève `ApiException` 409)

```csharp
HandleFreeCoinPackPurchase_WhenConcurrentRequests_CreditsOnce
    // deux appels en Task.WhenAll => IncrementPlayerCurrencyBalanceAsync reçu exactement 1 fois
HandleFreeCoinPackPurchase_WhenAlreadyClaimed_DoesNotCredit
TryClaimFreePackAsync_WhenUnexpectedError_Rethrows                  // 500 => exception, pas false
```

- [ ] **Step 2-4 :** échec, implémentation, succès.
- [ ] **Step 5 : commit** — `Claim free coin pack with an atomic inventory lock before crediting`.

### Task 7 : Retrait de la récompense publicitaire par jeton client

**Files :**
- Delete : `Project/Services/AdRewardsService.cs`
- Modify : `Project/ModuleConfig.cs` (retirer l'enregistrement)

- [ ] **Step 1 :** vérifier qu'aucun autre service n'utilise `AdRewardsService`, `ParsedTokenData` ou `LAST_AD_TOKEN` (`grep`).
- [ ] **Step 2 :** supprimer, puis `dotnet build` du module (0 erreur) et `dotnet test` (tout passe).
- [ ] **Step 3 : commit** — `Remove client-token ad reward function (decision 0001)` ; message rappelant que le client actuel verra un échec de récompense jusqu'à sa réécriture.

### Task 8 : Vérification finale et documentation

- [ ] **Step 1 :** skill `run-checks` côté backend : `dotnet build` du module en Release (0 erreur, avertissements ≤ ligne de base de 39) et `dotnet test` (tout vert).
- [ ] **Step 2 :** `grep` dans `Project/` : aucun log de `receipt`, `Payload`, `signature`, `adToken`.
- [ ] **Step 3 :** créer `Doc/backend-achats.md` (≤ 1 page) : flux, table des statuts, codes d'erreur `PURCHASE_REJECTED` / `PURCHASE_RETRY_LATER`, changements de comportement, liste des hypothèses à vérifier en staging/sandbox avec leur procédure.
- [ ] **Step 4 : commit** — `Document backend purchase flow and pending sandbox checks`.

## Hors de ce plan (prochaines étapes)

- Montée `net6.0` → `net8.0` du module et version Unity/SDK (décision 0003, skill `sdk-upgrade`).
- Client d'achat sur l'API IAP retenue : remise à zéro de l'état sur tous les chemins, reprise, traitement de `PURCHASE_REJECTED` (ne pas reboucler) et `PURCHASE_RETRY_LATER`, suppression du log de reçu.
- Adaptateur Apple pour StoreKit 2 si IAP 5 est retenu.
- Règles d'accès UGS (écriture client directe sur l'économie refusée), notifications de remboursement.
