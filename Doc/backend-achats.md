# Backend — flux d'achat

État au 2026-10-24, branche `feature/backend-p0-purchases`. Code : `UnityProject/GemHunterUGSCloud/Project/Purchases/` et `Services/StoreService.cs`.

> **Mise à jour du 2026-10-25.** Ce flux repose sur Unity Economy, fermé aux nouveaux projets depuis le 2026-09-08 ; le projet UGS du template répond 403 sur Economy. Il décrit le code actuel, mais **ne peut pas fonctionner sur ce projet UGS**. Le remplaçant (validation Apple/Google dans Cloud Code, registre Cloud Save) est proposé dans `Doc/decisions/0008-remplacement-economy.md`, à valider. Les hypothèses 2 à 9 ci-dessous, propres à Economy, deviendront sans objet avec ce remplacement : ne pas les tester d'ici là.

## Flux

1. Le client (Unity IAP) appelle `HandlePurchase(productId, receipt, transactionId)` avec l'enveloppe de reçu Unity IAP.
2. `PurchaseRedeemer` refuse sans appel Economy : joueur non authentifié, `productId` vide, reçu illisible, store inconnu, store `fake` hors de l'environnement `development`.
3. Google : `PurchaseData` = champ `json` du payload, `PurchaseDataSignature` = champ `signature`. Apple : `Receipt` = payload.
4. **Economy redeem est le seul propriétaire de l'attribution** : les récompenses sont celles des `.ecr`. Le serveur ne crédite rien d'autre.
5. Le statut Economy (en réponse ou dans le corps d'une erreur) est traduit en résultat.

| Statut Economy | Résultat | Réponse au client |
| --- | --- | --- |
| `VALID` | `Delivered` | `PlayerEconomyData` → le client confirme l'achat |
| `INVALID_ALREADY_REDEEMED` | `AlreadyDelivered` | `PlayerEconomyData` → le client confirme (reprise après réponse perdue) |
| `VALID_NOT_REDEEMED` | `RetryLater` | erreur `PURCHASE_RETRY_LATER` → achat laissé en attente |
| `INVALID_ANOTHER_PLAYER`, `INVALID_VERIFICATION_FAILED`, `INVALID_CONFIGURATION`, `INVALID_PRODUCT_ID_MISMATCH` | `Rejected` | erreur `PURCHASE_REJECTED` → ne pas réessayer |
| 400, 404, 422 sans statut lisible | `Rejected` | erreur `PURCHASE_REJECTED` |
| Autres codes (401, 403, 408, 409…) sans statut lisible | `RetryLater` | erreur `PURCHASE_RETRY_LATER` |
| Store `fake` en `development` | `Simulated` | `PlayerEconomyData` inchangé, **rien n'est crédité** |

Logs : statut, `productId`, store, `CorrelationId`, `TransactionLogId` (empreinte SHA-256 tronquée, même algorithme que le package client). Jamais le reçu, le payload, la signature ni l'identifiant de transaction brut, qui est le purchase token sur Google Play avec IAP 5.

## Inventaire

Le jeu stocke chaque objet empilable dans une instance « compteur » `{"amount": N}`. Un redeem ajoute des instances sans données d'instance : chacune compte pour une unité. Les lectures additionnent toutes les instances (lecture paginée) ; les écritures ciblent l'instance compteur. Pour le cœur infini, l'instance retenue est la plus récemment activée.

## Pack gratuit

Verrou d'abord (item `FREE_COIN_PACK_CLAIM`, défini dans `Assets/UGSConfigs/FREE_COIN_PACK_CLAIM.eci`, à déployer, créé avec l'identifiant fixe `FREE_COIN_PACK_CLAIM` et refusé par Economy s'il existe), crédit ensuite. Une panne entre les deux fait perdre le pack (10 pièces) plutôt que de le doubler.

## Changements de comportement

- Récompenses exclusivement issues des `.ecr` : le Mega Pack donne aussi 5 `VERTICAL_ROCKET` ; plus de double crédit.
- Cœur infini accordé par redeem (sans données d'instance) : considéré actif depuis la création de l'item.
- `HandleGrantVideoAdReward` supprimée (décision 0001) : le client Gem Hunter actuel affiche un échec de récompense pub.
- Le client actuel traite toute erreur de la même façon (pas de confirmation, pas de reprise) : les codes `PURCHASE_*` seront exploités lors de la réécriture du client.

## Hypothèses à vérifier en staging / sandbox

| # | Hypothèse | Procédure |
| --- | --- | --- |
| 1 | ~~Format du reçu~~ **Vérifié** dans le code d'Unity IAP 5.4.4 (`UnifiedReceiptFormatter`, `OrderInfo`) : `{Store, TransactionID, Payload}`, Google `json`/`signature`, Apple reçu d'application, éditeur `fake` | — |
| 2 | `VALID_NOT_REDEEMED` signifie « rien n'a été accordé » | Achat sandbox, rejouer le redeem, observer le statut et le portefeuille |
| 3 | Deux redeems simultanés du même reçu : un seul `VALID` | Deux appels parallèles en staging avec le même reçu sandbox |
| 4 | Economy renvoie `409` sur un `playersInventoryItemId` existant | Deux `HandleFreeCoinPackPurchase` parallèles en staging |
| 5 | L'item `INFINITE_HEART` créé par redeem n'a pas de données d'instance | Achat sandbox du Bundle Pack, lecture de l'inventaire |
| 6 | Economy accepte `localCost = 0` et `localCurrency = ""` | Premier achat sandbox Google et Apple |
| 7 | Forme des instances créées par un redeem d'objets (une instance par unité, sans données) | Achat sandbox du Bundle Pack, lecture de l'inventaire ; si une instance porte N unités, adapter `SumInventory` |
| 8 | iOS, reçu unifié IAP 4 : un second achat du même consommable est redeem sur la bonne transaction (pas `INVALID_ALREADY_REDEEMED`) | Deux achats sandbox successifs de `COIN_PACK_100` |
| 9 | Pagination de l'inventaire (`after` = dernier `playersInventoryItemId`, `limit` ≤ 100) | Joueur de staging avec plus de 100 instances |

## Non vérifié

Aucun achat sandbox Google Play ni App Store, aucun appareil réel, aucun déploiement Cloud Code. Les tests (`dotnet test UnityProject/GemHunterUGSCloud/Tests`) utilisent des doublures de l'API Economy : ils prouvent la logique du module, pas le comportement réel d'Economy ni des stores.

## État du déploiement (constat du 2026-10-24)

Le module Cloud Code déployé sur le projet UGS est encore celui du sample : un achat avec le faux store de l'éditeur, en environnement `production`, a été accepté et a crédité 100 pièces. Les corrections de ce document ne sont actives qu'après déploiement du module (non fait : aucun déploiement sans instruction explicite).

Mise à jour du 2026-10-25 : le module corrigé a été déployé en `production` sur instruction explicite (d'après le code, le faux store y est donc refusé ; non revérifié par un appel réel). Le déploiement des ressources Economy a échoué (403).
