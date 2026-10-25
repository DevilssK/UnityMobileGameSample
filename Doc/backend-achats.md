# Backend — portefeuille protégé et achats

État au 2026-10-25, branche `feature/protected-wallet`. Spec : `Doc/specs/2026-10-25-portefeuille-protege-design.md` ; décision 0008.

## Où est le code

| Partie | Emplacement |
| --- | --- |
| Bibliothèque réutilisable (aucun type Gem Hunter) | `SharedPackages/Server/DevilCorp.MobileFoundation.Server/` : `State/` (stockage protected, transaction, initialisation sous délai de grâce), `Wallet/` (état et règles), `Purchases/` (registre, catalogue, `TransactionLogId`), `Config/` (Remote Config en cache) |
| Module Gem Hunter | `UnityProject/GemHunterUGSCloud/Project/` : `Game/` (règles pures, état du jeu), `Services/` (fonctions Cloud Code), `purchase_catalog.json` (ressource incorporée) |
| Réglages déployés | `Assets/UGSConfigs/game_tuning.rc` (`WALLET_DEFAULTS`, `AREA_ITEMS`) et `fetched_keys.rc` (`DAILY_REWARDS_CONFIG`, `AREA_COMPLETE_REWARDS`) |
| Tests | `dotnet test SharedPackages/Server/Tests/DevilCorp.MobileFoundation.Server.Tests` et `dotnet test UnityProject/GemHunterUGSCloud/Tests` |

## Garanties et comment elles sont obtenues

| Garantie | Mécanisme |
| --- | --- |
| Le client ne peut pas modifier son solde | Données en classe protected ; le jeton joueur reçoit 401 en écriture (vérifié en `development`). Le serveur écrit avec `context.ServiceToken` et ne lit jamais la classe default |
| Une opération = une écriture atomique | Batch protected (≤ 20 clés) avec le writeLock de chaque clé lue ; un conflit fait échouer tout le batch (vérifié) → relecture, 5 tentatives, puis `RETRY_LATER` |
| Une transaction d'achat livrée une seule fois, sans limite de durée | Clé `PURCHASE_<sha256 de l'id de transaction>` écrite dans le même batch que le crédit, jamais évincée (≤ 1900 par joueur) |
| Une action de jeu répétée ne s'applique qu'une fois | `operationId` du client gardé dans `WALLET.recentOperations` (50 derniers) |
| La création ne peut écraser aucun état acquis | Écriture inconditionnelle réservée à l'initialisation, puis aucune mutation pendant 30 s (une fonction est tuée après 15 s). Cloud Save n'a pas de création conditionnelle (spike du 2026-10-25) |
| Le gain d'un achat vient du serveur | `purchase_catalog.json` incorporé au module ; seul le faux store (développement) utilise l'id catalogue envoyé par le client |

## Flux d'achat

1. Le client (Unity IAP) appelle `HandlePurchase(productId, receipt, transactionId)`.
2. Reçu illisible → `PURCHASE_REJECTED`.
3. Google Play ou App Store → `PURCHASE_RETRY_LATER` : la vérification par le store n'existe pas encore (spec suivante). Les builds hors développement masquent donc les achats réels.
4. Faux store hors `development` → `PURCHASE_REJECTED` (décidé côté serveur, d'après `context.EnvironmentName`).
5. Faux store en `development` : livraison par `PurchaseLedger.DeliverAsync`. Une transaction déjà enregistrée renvoie le portefeuille actuel (le client confirme).
6. Toute erreur métier interne (délai de grâce, conflits, configuration) → `PURCHASE_RETRY_LATER` : un achat payé n'est jamais refusé pour une raison passagère.

Logs : `CorrelationId`, `TransactionLogId`, produit, store, résultat. Jamais le reçu, la signature ni l'id de transaction brut.

## Codes d'erreur

`PURCHASE_REJECTED`, `PURCHASE_RETRY_LATER`, `INSUFFICIENT_FUNDS` (pièces ou étoiles), `RETRY_LATER` (conflits épuisés, délai de grâce, quotidien pas encore réclamable), `CONFIGURATION_ERROR` (clé Remote Config absente, schéma inconnu, registre plein, zone non terminée).

## Vérifié en `development` (2026-10-25)

Module et réglages déployés par la fenêtre Deployment. Parcours joué depuis l'éditeur avec un nouveau joueur anonyme :

| Étape | Résultat |
| --- | --- |
| Initialisation | 200 pièces, objets initiaux (tous listés), 8 cœurs, 5 étoiles ; second appel `new=False` |
| Mutation dans les 30 s | `RETRY_LATER: state was just created` |
| Achat faux store `COIN_PACK_100`, puis même transaction | 300 puis 300 (pas de second crédit) |
| Pack gratuit ×2 | 310 une seule fois |
| Victoire ×2 avec le même `operationId` | 6 étoiles (une seule application) |
| Défaite | 7 cœurs |
| Amélioration de zone | niveau 1, 290 pièces |
| Quotidien ×2 | +10 pièces, puis `RETRY_LATER` (jour de 30 s dans la config de démo) |
| Écriture protected avec le jeton client | HTTP 401 ; le portefeuille reste à 300 |
| Configuration Remote Config absente (avant déploiement de `fetched_keys.rc`) | `CONFIGURATION_ERROR`, aucune écriture |

## Non vérifié

Aucun achat sandbox Google Play ni App Store, aucun appareil réel. La validation des reçus réels n'est pas implémentée. Le comportement des données Cloud Save à la suppression d'un compte UGS n'est pas établi. Rien n'est déployé en `production` depuis cette refonte : l'environnement `production` exécute encore le module du 2026-10-25 matin (Economy).
