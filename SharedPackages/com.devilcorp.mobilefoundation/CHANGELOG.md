# Changelog

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/), versionnement [SemVer](https://semver.org/lang/fr/).

## [Non publié]

### Sécurité
- Les identifiants de transaction ne sont plus journalisés : avec IAP 5, sur Google Play, c'est le purchase token. `StorePurchase.ToString()` et `UnityIapStoreGateway` affichent `TransactionLogId` (12 premiers caractères hexadécimaux du SHA-256), exposé aussi par `StorePurchase.LogId`.

## [0.2.0] — 2026-10-25

### Ajouté
- Module **Ads** : `IRewardedAdsService`, `RewardedAdCoordinator` (récompense uniquement sur l'événement de récompense, au plus une fois par pub, délai de grâce pour une récompense reçue après la fermeture, relances de chargement bornées), `IRewardedAdGateway`.
- Adaptateur optionnel **Ads.LevelPlay** (`LevelPlayRewardedAdGateway`, `LevelPlayAdsSettings`), compilé seulement si `com.unity.services.levelplay` ≥ 9.0.0 est installé. Suite de test LevelPlay limitée aux builds de développement.
- `README.md` : intégration du package dans un autre projet.

## [0.1.0] — 2026-10-24

### Ajouté
- Module **Purchases** : `IPurchaseService`, `PurchaseCoordinator` (validation par `IPurchaseFulfillment` avant confirmation, reprise des achats payés non livrés, achat actif lié à sa transaction, reconnexion du store à la demande), `IStoreGateway`.
- Adaptateur **Purchases.UnityIap** pour Unity IAP 5.4.4 (`UnityIapStoreGateway`, `StoreProductDefinition`).
