# 0001 — Modèle économique V1 : permanents + pub locale, portefeuille protégé plus tard

Date : 2026-10-24 · Statut : acceptée ; « Economy redeem » caduc (Economy fermé aux nouveaux projets, 403 sur ce projet UGS) — remplaçant proposé dans 0008, à valider

## Problème
La moitié des P0 du brief (§5) ne s'appliquent que si une économie protégée existe. Un portefeuille serveur impose tout le §8 (registre idempotent, dépenses serveur, remboursements) et une confirmation S2S des pubs (§9), qui exigent un endpoint HTTPS public dont la disponibilité dans UGS n'est pas vérifiée.

## Décision
- V1 livrée avec : achats **permanents** (ex. « retirer les pubs », déblocages) validés côté serveur, propriétaire unique de l'attribution = Economy redeem ; pubs récompensées = **effet local de faible valeur**, accordé une seule fois sur l'événement de récompense du SDK.
- Les contrats du package (`IPurchaseService`, `IRewardedAdsService`) sont conçus pour accueillir plus tard un portefeuille protégé (résultats en attente, confirmation serveur), sans l'implémenter.
- Le chemin `AdRewardsService` (jeton client) est retiré du chemin obligatoire du template.

## Alternatives écartées
- Portefeuille protégé dès la V1 — coût ×2-3, dépendance probable à une infra hors UGS (webhooks).
- Économie 100 % locale sans validation d'achat — contraire au brief (§2 : validation serveur des achats).

## Conséquences
- P0 « si économie protégée » (pubs) : traités par retrait du chemin, pas par correction.
- Consommables : si un jeu en vend, leur politique (portefeuille local non protégé ou serveur) sera une nouvelle décision.
- Avant d'activer le portefeuille protégé : vérifier la réception des callbacks LevelPlay S2S et des notifications Apple/Google dans UGS.
