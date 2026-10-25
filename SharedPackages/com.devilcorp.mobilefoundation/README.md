# DevilCorp Mobile Foundation

Package UPM `com.devilcorp.mobilefoundation` : le socle non-gameplay commun aux jeux mobiles DevilCorp. Il fournit des **contrats** que le jeu appelle et des **adaptateurs** vers les SDK (Unity IAP, LevelPlay), sans aucune référence à un jeu précis.

| Module | État | Rôle |
| --- | --- | --- |
| Purchases | 0.1.0 | Achats intégrés validés par un backend, reprise des achats payés non livrés |
| Ads | 0.2.0 | Pubs récompensées, récompense locale accordée une seule fois |
| Analytics, consentement, sauvegarde | à venir | — |

Gem Hunter (`UnityProject/`) n'est qu'un exemple d'intégration. Ce document suffit pour intégrer le package dans un autre projet.

## Ce que le package fait et ne fait pas

- Il ne crédite **jamais** rien côté client. Pour les achats, l'attribution appartient au backend du jeu. Pour les pubs, le jeu applique lui-même sa récompense locale.
- Il ne fournit pas de backend : chaque jeu implémente `IPurchaseFulfillment`, qui appelle son propre serveur (Cloud Code dans Gem Hunter).
- Il ne journalise jamais de reçu, de token ni de signature. `StorePurchase.ToString()` n'affiche que le produit et l'identifiant de transaction.
- Pas d'abonnements, pas de restauration iOS (`RestoreTransactions`) et pas de portefeuille protégé pour l'instant (voir [Limites connues](#limites-connues)).

## Installation

Prérequis : Unity **6000.6** ou plus récent.

Dans le `Packages/manifest.json` du projet, ajouter **une** des deux formes :

```json
// Copie locale du dépôt (chemin relatif au dossier Packages/)
"com.devilcorp.mobilefoundation": "file:../../SharedPackages/com.devilcorp.mobilefoundation"

// Depuis git (épingler un tag ou un commit, jamais une branche mouvante en production)
"com.devilcorp.mobilefoundation": "https://github.com/DevilssK/UnityMobileGameSample.git?path=/SharedPackages/com.devilcorp.mobilefoundation#<tag-ou-commit>"
```

Dépendances :

| Dépendance | Version | Obligatoire |
| --- | --- | --- |
| `com.unity.purchasing` (Unity IAP) | 5.4.4 | oui, installée automatiquement par le package |
| `com.unity.services.levelplay` | ≥ 9.0.0 (testé 9.5.1) | non : à ajouter soi-même pour utiliser l'adaptateur pub |

**LevelPlay**, c'est quoi ? C'est le SDK de **médiation publicitaire** de Unity (anciennement ironSource), distribué dans le package Unity « Ads Mediation » (`com.unity.services.levelplay`). Une médiation interroge plusieurs régies (Unity Ads, AdMob, Meta…) et affiche la pub la mieux payée. Le jeu n'intègre donc qu'un SDK au lieu d'un par régie. Les régies elles-mêmes s'ajoutent par le LevelPlay Network Manager (menu Ads Mediation de l'éditeur). L'adaptateur garde le nom LevelPlay parce que c'est le nom du SDK qu'il faudra mettre à jour.

Sans LevelPlay, l'assembly `DevilCorp.MobileFoundation.Ads.LevelPlay` n'est simplement pas compilée (`versionDefines` → `DEVILCORP_LEVELPLAY`). Le cœur `Ads` reste utilisable avec un autre fournisseur.

## Assemblies

| Assembly | Dépend de | Moteur Unity | Contenu |
| --- | --- | --- | --- |
| `DevilCorp.MobileFoundation.Purchases` | — | non (`noEngineReferences`) | `IPurchaseService`, `PurchaseCoordinator`, `IStoreGateway`, `IPurchaseFulfillment`, types de résultat |
| `DevilCorp.MobileFoundation.Purchases.UnityIap` | Purchases, `Unity.Purchasing` | oui | `UnityIapStoreGateway`, `StoreProductDefinition` |
| `DevilCorp.MobileFoundation.Ads` | — | non | `IRewardedAdsService`, `RewardedAdCoordinator`, `IRewardedAdGateway`, types de résultat |
| `DevilCorp.MobileFoundation.Ads.LevelPlay` | Ads, `Unity.LevelPlay` | oui | `LevelPlayRewardedAdGateway`, `LevelPlayAdsSettings` |

Les cœurs ne référencent aucun SDK : les règles (une transaction = une attribution, une pub = au plus une récompense) y sont testées hors Unity. Les adaptateurs ne font que traduire le SDK.

Règle d'architecture : le gameplay ne voit que `IPurchaseService` et `IRewardedAdsService`. Les types `UnityIap*` et `LevelPlay*` n'apparaissent que dans le code de composition (bootstrap).

## Achats

### Flux

```
Joueur ─► IPurchaseService.PurchaseAsync(productId)
            │
PurchaseCoordinator ─► IStoreGateway.Purchase ─► store (Google Play / App Store)
            │                                          │ paiement
            │◄──────────── PurchasePending(StorePurchase) ◄┘
            ▼
IPurchaseFulfillment.FulfillAsync ─► backend du jeu (validation du reçu + attribution unique)
            │
   Delivered ─► IStoreGateway.Confirm(transactionId) ─► transaction finalisée
   Rejected  ─► jamais confirmée, jamais relancée automatiquement
   RetryLater ─► gardée en attente, relancée par RetryPendingFulfillmentsAsync
```

Un achat payé mais non confirmé (application tuée, réseau coupé) est redonné par le store au démarrage suivant. Le coordinateur le revalide sans action du joueur et publie le résultat par `PurchaseCompleted`.

### Composition (bootstrap)

À faire **au démarrage de l'application**, pas dans une scène de boutique : sinon les achats d'une session précédente ne sont récupérés qu'à l'ouverture de la boutique.

```csharp
using DevilCorp.MobileFoundation.Purchases;
using DevilCorp.MobileFoundation.Purchases.UnityIap;
using UnityEngine.Purchasing;

var catalog = new[]
{
    // productId = identifiant connu du jeu et du backend ; puis ids Google Play et App Store
    new StoreProductDefinition("REMOVE_ADS", ProductType.NonConsumable, "remove_ads_play", "remove_ads_apple"),
};

var storeGateway = new UnityIapStoreGateway(catalog, Debug.Log);
IPurchaseFulfillment fulfillment = new MyBackendFulfillment(/* client du backend du jeu */);
IPurchaseService purchases = new PurchaseCoordinator(storeGateway, fulfillment, Debug.Log);

// Le coordinateur doit exister AVANT la connexion : un achat en attente peut arriver dès Connect.
await storeGateway.ConnectAsync();

// Occasions explicites de relance (il n'y a pas de boucle automatique) :
// après la connexion du joueur, à l'ouverture de la boutique, au retour au premier plan.
await purchases.RetryPendingFulfillmentsAsync(CancellationToken.None);
```

Désactiver l'initialisation automatique du catalogue codeless d'Unity IAP (`enableCodelessAutoInitialization: false` dans `Assets/Resources/IAPProductCatalog.json`) : c'est `UnityIapStoreGateway` qui connecte le store.

### Acheter depuis le jeu

```csharp
var result = await purchases.PurchaseAsync("REMOVE_ADS", cancellationToken);
switch (result.Outcome)
{
    case PurchaseOutcome.Delivered:          /* afficher le succès : le backend a déjà attribué */ break;
    case PurchaseOutcome.RetryLater:         /* « Paiement reçu, livraison automatique bientôt » */ break;
    case PurchaseOutcome.Deferred:           /* « En attente d'approbation » : aucun droit */ break;
    case PurchaseOutcome.Rejected:           /* « Achat non validé » */ break;
    case PurchaseOutcome.Unavailable:        /* store non prêt : une reconnexion est relancée */ break;
    case PurchaseOutcome.Cancelled:
    case PurchaseOutcome.Failed:
    case PurchaseOutcome.AlreadyInProgress:  /* débloquer l'UI */ break;
}
```

- Toujours débloquer l'UI quel que soit le résultat : chaque chemin est terminal.
- Annuler le `CancellationToken` (fermeture de l'écran) arrête seulement l'attente. Un achat payé continue d'être validé, et son résultat arrive par `PurchaseCompleted`.
- S'abonner à `PurchaseCompleted` pour signaler au joueur une livraison tardive (achat récupéré, relance réussie).

### Implémenter `IPurchaseFulfillment`

```csharp
public sealed class MyBackendFulfillment : IPurchaseFulfillment
{
    public async Task<FulfillmentStatus> FulfillAsync(StorePurchase purchase, CancellationToken ct)
    {
        if (!IsSignedIn) return FulfillmentStatus.RetryLater;          // personne à créditer pour l'instant
        try
        {
            await _backend.RedeemAsync(purchase.ProductId, purchase.Receipt, purchase.TransactionId, ct);
            return FulfillmentStatus.Delivered;                       // livré maintenant ou déjà livré à ce joueur
        }
        catch (DefinitiveRefusal) { return FulfillmentStatus.Rejected; }  // reçu invalide, autre joueur, mauvais produit
        catch (Exception)        { return FulfillmentStatus.RetryLater; } // réseau, service, erreur inconnue
    }
}
```

- Ne jamais logger `purchase.Receipt`. Logger `purchase` (ToString sûr) ou `purchase.TransactionId`.
- En cas de doute, répondre `RetryLater` : un `Rejected` à tort laisse le joueur payer sans être livré.
- Exemple réel : `UnityProject/Assets/GemHunterUGS/Scripts/Store/CloudCodePurchaseFulfillment.cs`.

### Ce que le backend doit garantir

Le package suppose un backend qui :

1. Valide le reçu auprès du store et vérifie produit, application et environnement.
2. Est le **seul** à attribuer (Gem Hunter : Economy `RedeemGooglePlayPurchase` / `RedeemAppleAppStorePurchase`, sans second crédit).
3. Déduplique sur l'identifiant de transaction du store. Pour le même joueur, il renvoie le résultat déjà livré (le client confirmera alors). Pour un autre joueur, il refuse.
4. Refuse tout store simulé (`fake`) hors environnement de développement.
5. Distingue refus définitif (→ `Rejected`) et erreur transitoire (→ `RetryLater`).

Implémentation de référence et statuts : `Doc/backend-achats.md`, code dans `UnityProject/GemHunterUGSCloud/Project/Purchases/`.

## Pubs récompensées

### Politique

- La récompense est accordée **uniquement** sur l'événement de récompense du SDK, jamais à la simple fermeture, et **au plus une fois** par pub.
- Certains réseaux envoient la récompense *après* la fermeture. À la fermeture sans récompense, le coordinateur attend donc un délai de grâce (`DefaultRewardGracePeriod`, 1 s) avant de conclure `NotRewarded`.
- Rechargement automatique après chaque pub. Un échec de chargement est relancé jusqu'à `MaxLoadRetries` (3) fois, espacées de 5 s, puis le coordinateur attend la prochaine demande.
- V1 : la récompense est un **effet local de faible valeur** (coups bonus, booster…), décision `Doc/decisions/0001-modele-economique-v1.md`. Une récompense de monnaie protégée exigerait une confirmation serveur (S2S), non fournie.
- Une pub indisponible ne doit jamais bloquer la partie.

### Composition (bootstrap)

```csharp
using DevilCorp.MobileFoundation.Ads;
using DevilCorp.MobileFoundation.Ads.LevelPlay;

var settings = new LevelPlayAdsSettings(
    androidAppKey: "<app key Android>",
    iosAppKey: "<app key iOS>",
    androidRewardedAdUnitId: "<ad unit rewarded Android>",
    iosRewardedAdUnitId: "<ad unit rewarded iOS>",
    enableTestSuite: true);            // n'agit que dans les builds de développement (Debug.isDebugBuild)

var adGateway = new LevelPlayRewardedAdGateway(settings, Debug.Log);
IRewardedAdsService rewardedAds = new RewardedAdCoordinator(adGateway, log: Debug.Log);

// Une fois l'identifiant joueur connu (après connexion) :
adGateway.Initialize(playerId);        // LevelPlay.Init ; l'id n'est jamais loggé
rewardedAds.Start();                   // premier chargement ; un Load demandé avant l'init attend l'init
```

Si l'init LevelPlay échoue (réseau…), l'échec est remonté comme un échec de chargement. Le prochain chargement relance l'init, que ce soit une relance automatique (3 au plus) ou une demande du joueur.

Les app keys et ad units sont publics, mais propres à chaque jeu et plateforme : les garder dans la configuration du jeu. La suite de test LevelPlay ne s'ouvre jamais dans un build de production, même si `enableTestSuite` vaut `true`.

### Afficher une pub depuis le jeu

**Accorder la récompense à un seul endroit.** `AdCompleted` est levé pour **chaque** pub terminée, y compris celles dont l'appelant a déjà reçu `Rewarded` par `ShowAsync`. Accorder dans les deux revient à accorder deux fois. Choisir l'un des deux modèles :

**Modèle A — recommandé si la récompense doit survivre à la fermeture de l'écran :** accorder dans `AdCompleted`, et n'utiliser `ShowAsync` que pour l'UI.

```csharp
// Abonnement unique, par exemple au bootstrap ou dans un service du jeu qui vit toute la session
rewardedAds.AdCompleted += result =>
{
    if (result.Outcome == RewardedAdOutcome.Rewarded && result.Placement == "Level_Failed")
    {
        GrantLocalSecondChance();      // seul endroit qui accorde
    }
};

// Dans l'écran : uniquement l'UI
button.SetEnabled(rewardedAds.IsAdReady);
rewardedAds.AvailabilityChanged += ready => button.SetEnabled(ready);   // se désabonner à la fermeture
var result = await rewardedAds.ShowAsync("Level_Failed", cancellationToken);
ShowFeedback(result.Outcome);          // message, déblocage du bouton ; n'accorde rien
```

**Modèle B — plus simple, si l'écran reste ouvert pendant la pub :** accorder sur le résultat de `ShowAsync` et ne pas utiliser `AdCompleted`. Si l'attente est annulée (fermeture de l'écran), la récompense de cette pub est perdue, ce qui est acceptable pour un effet local de faible valeur.

```csharp
var result = await rewardedAds.ShowAsync("Level_Failed", cancellationToken);
if (result.Outcome == RewardedAdOutcome.Rewarded)
{
    GrantLocalSecondChance();          // le jeu décide de la récompense ; result.Reward n'est qu'informatif
}
```

| `RewardedAdOutcome` | Sens |
| --- | --- |
| `Rewarded` | Événement de récompense reçu : accorder l'effet une fois |
| `NotRewarded` | Pub fermée avant la fin, ou attente annulée par l'appelant |
| `Unavailable` | Aucune pub chargée ; un chargement est relancé |
| `Failed` | Affichage impossible |
| `AlreadyShowing` | Une pub est déjà à l'écran |

Si l'attente est annulée (`NotRewarded`, message `wait cancelled`) alors que la pub est encore à l'écran, le vrai résultat arrive quand même par `AdCompleted` : c'est ce qui rend le modèle A robuste.

Exemple réel (modèle B) : `UnityProject/Assets/GemHunterUGS/Scripts/Ads/AdRewardManagerClient.cs`, composé dans `Core/GameInitializer.cs`.

### Autre fournisseur de pubs

Implémenter `IRewardedAdGateway` (`IsReady`, `Load`, `Show` et les événements `Loaded`, `LoadFailed`, `Rewarded`, `Closed`, `DisplayFailed`) et le passer à `RewardedAdCoordinator`. Les règles de récompense restent celles du coordinateur. Ne pas ajouter de fournisseur sans besoin démontré.

## Threading et logs

- Les événements des SDK arrivent sur le thread principal Unity. Les cœurs n'utilisent pas `ConfigureAwait(false)` : les continuations `await` y restent, et le jeu peut toucher l'UI directement.
- Chaque constructeur accepte un `Action<string>? log` facultatif. Le brancher sur le logger du jeu. Les messages ne contiennent ni reçu, ni token, ni identifiant joueur.

## Tests

Les cœurs sont testés hors Unity (xUnit, .NET SDK 10, C# 9 comme Unity) :

```bash
dotnet test SharedPackages/Tests/MobileFoundation.Purchases.Tests
dotnet test SharedPackages/Tests/MobileFoundation.Ads.Tests
```

Ces tests couvrent les invariants : achat livré confirmé une fois, échec puis nouvel achat, reprise au démarrage, doublons, annulation de l'attente, récompense en double ou après fermeture, relances de chargement bornées. Ils ne remplacent ni les sandbox Google Play / App Store, ni un appareil réel avec de vraies pubs.

## Limites connues

- Achats : un achat refusé par le serveur est redonné par le store à chaque lancement (jamais confirmé, donc redélivré).
- Achats : un `CancellationToken` déjà annulé ouvre quand même la fenêtre du store.
- Achats : pas de délai maximal sur `FulfillAsync` ; il revient à l'implémentation du jeu.
- Achats : pas d'abonnements, pas de `RestoreTransactions` (obligatoire sur iOS pour les non-consommables : à ajouter avant une publication iOS).
- Pubs : la récompense n'est pas confirmée côté serveur (pas de S2S LevelPlay) ; réservée à des effets locaux.
- Tests des cœurs exécutés sur net10.0 : une API .NET absente du profil Unity ne serait détectée qu'à la compilation Unity.

Historique des versions : [CHANGELOG.md](CHANGELOG.md).
