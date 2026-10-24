# 0006 — Package commun DevilCorp créé avec le module Achats

Date : 2026-10-24 · Statut : acceptée

## Problème
Le client d'achat du sample devait être réécrit sur Unity IAP 5 (décision 0003). Le réécrire dans le code Gem Hunter puis l'extraire à l'étape C revenait à faire le travail deux fois, et laissait le jeu appeler directement le SDK, ce que le `CLAUDE.md` interdit.

## Décision
- Créer dès maintenant le package UPM local `com.devilcorp.mobilefoundation` (`SharedPackages/`, dépendance `file:` du projet hôte), namespace `DevilCorp.MobileFoundation`. Le nom provisoire « Studio » du brief est remplacé une fois pour toutes par DevilCorp, avant tout consommateur.
- Module Achats en trois couches :
  - cœur `DevilCorp.MobileFoundation.Purchases` sans `UnityEngine` (contrats `IPurchaseService`, `IStoreGateway`, `IPurchaseFulfillment` et `PurchaseCoordinator`), testé par `dotnet test` en C# 9 ;
  - adaptateur `DevilCorp.MobileFoundation.Purchases.UnityIap` (Unity IAP 5) ;
  - côté jeu : la validation serveur (`CloudCodePurchaseFulfillment`) et le catalogue.
- Le service est créé au démarrage du jeu (`GameInitializer`), pas dans la scène du hub.
- L'auto-initialisation Codeless IAP est désactivée.

## Alternatives écartées
- Réécrire `StoreClient` sur IAP 5 dans le sample, extraction plus tard : double travail, SDK appelé par le jeu.
- Tests du cœur via Unity Test Framework : ils exigent l'éditeur ou un batchmode ; `dotnet test` donne une boucle TDD rapide sur le même code source.

## Conséquences
- Le projet de tests du cœur (`SharedPackages/Tests/`) compile les sources du package en C# 9 : toute API plus récente casse la compilation Unity et doit être refusée en revue.
- Les achats restent validés par le backend : le client n'attribue rien, il affiche l'économie renvoyée par le serveur.
- Versionnement : `0.1.0` ; la distribution par Git versionné aux jeux consommateurs reste à faire (étape E).
