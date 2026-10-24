---
name: monetization-change
description: À utiliser pour toute modification touchant aux achats intégrés (Unity IAP, StoreClient, StoreService), aux publicités récompensées (LevelPlay, AdRewardManagerClient, AdRewardsService), à Economy/portefeuille, à la validation de reçus ou au backend Cloud Code qui crédite le joueur. Impose les invariants de confiance et la matrice de tests du brief.
---

# Modification de la monétisation

Référence : sections 5, 8, 9 et 12 de `Doc/CLAUDE_UNITY_MOBILE_FOUNDATION.md`. Les relire pour la partie touchée.

## Avant de coder

1. Lire le code actuel du flux de bout en bout : bouton → client → Cloud Code → Economy → retour client → finalisation store. Vérifier si le défaut P0/P1 visé existe encore.
2. Vérifier le format réel des données dans la version installée du SDK (Unity IAP, LevelPlay) via context7 ou la doc officielle. Ne pas transposer une ancienne API (ex. `verifyReceipt` vs StoreKit 2, protocole AdMob vs LevelPlay).
3. Identifier **le propriétaire unique de l'attribution** (ex. Economy redeem). Si deux chemins créditent, c'est le bug à corriger.

## Invariants (à vérifier sur chaque diff)

- Une transaction valide → une seule attribution logique, même après retries, panne ou appels concurrents.
- Clé de déduplication = identifiant autoritatif du store, portée store/app/environnement. Jamais un ID choisi par le client.
- Le serveur ne fait jamais confiance au client pour : montant, récompense, `playerId`, type de store, environnement.
- Aucun chemin `fake`/simulé n'est accepté par le backend de production.
- Doublon du même joueur → retourner le résultat déjà livré. Doublon d'un autre joueur → refus.
- Paiement différé/en attente → aucun droit.
- Côté client : l'état "achat en cours" est réinitialisé sur **tous** les chemins terminaux (succès, échec, annulation, exception).
- Annuler l'attente UI n'abandonne pas la livraison d'un achat payé ; la reprise au démarrage récupère les achats en attente.
- Pub : récompense uniquement sur l'événement de récompense, jamais à la fermeture. Récompense protégée → confirmation serveur authentifiée, sans crédit client en plus.
- Pas de reçu complet, signature ou token dans les logs. Utiliser un identifiant de corrélation.
- Un client ne peut pas écrire directement sur un solde protégé (règles d'accès UGS) : tester le refus au niveau API.

## Tests à écrire

Logique backend en tests `dotnet` sans dépendance UGS réelle (fournisseurs simulés qui savent aussi échouer, annuler et dupliquer). Couvrir au minimum les lignes de la matrice section 12 touchées par le changement, par exemple :

- `RedeemPurchase_WhenCalledTwiceBySamePlayer_ReturnsSameResultWithoutSecondGrant`
- `RedeemPurchase_WhenReceiptBelongsToOtherPlayer_IsRejected`
- `RedeemPurchase_WhenStoreIsFakeInProduction_IsRejected`
- `RedeemPurchase_WhenConcurrentIdenticalRequests_GrantsOnce`
- `Purchase_AfterFailure_AllowsNewPurchase` (client)

Lancer avec le skill `run-checks`.

## Rapport

Préciser explicitement ce qui n'a **pas** été vérifié : sandbox Google Play, sandbox App Store, appareil réel, notifications de remboursement. Un test simulé n'est pas une validation store.
