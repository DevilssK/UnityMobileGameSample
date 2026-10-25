# Économie joueur sur Cloud Save — design (sous-projet 1/2)

Date : 2026-10-25 · Statut : à relire

## Contexte

Depuis le 8 septembre 2026, Unity Economy n'accepte plus de nouveaux projets (docs.unity.com/en-us/economy). Pour un nouveau projet, Unity recommande une économie gérée par le serveur avec **Cloud Code + Cloud Save + Remote Config**, et Unity IAP pour les achats. Le projet UGS du template (DevilCorp-MobileGameSample) est neuf : Economy y est inutilisable.

Décision utilisateur du 2026-10-25 : tout migrer, avec les technologies et pratiques Unity actuelles, pour que le projet serve de référence aux prochains jeux. Le travail est découpé en deux sous-projets :

1. **Économie joueur sur Cloud Save** (ce document) : tout Gem Hunter refonctionne sans Economy.
2. **Validation des achats sans Economy** (spec séparée) : vérification Apple par signature, vérification Google par l'API Google Play, identifiant du joueur attaché à la transaction.

## Objectif et critères de réussite

- Plus aucun appel à Economy, ni dans le module Cloud Code ni dans le client. Le package `com.unity.services.economy` et les fichiers `.ecr/.ecc/.eci/.ecv` sont retirés.
- Toutes les fonctions Cloud Code existantes gardent leur nom, leurs paramètres et leur format de retour (`PlayerEconomyData`) : le client et les bindings générés ne changent pas.
- Une opération sur le solde ou l'inventaire est atomique par joueur : deux appels simultanés ne peuvent ni dépenser deux fois, ni créditer deux fois, ni perdre une écriture.
- Le client ne peut pas modifier son solde : les données sont en accès *protected* (le joueur lit, seul le serveur écrit).
- Les définitions (soldes initiaux, récompenses des packs, pack gratuit) sont des données déployables (Remote Config), pas du code.
- Achats, en attendant le sous-projet 2 : faux store accepté en `development` uniquement, livraison idempotente par identifiant de transaction ; un reçu Google ou Apple réel répond « réessayer plus tard » (rien n'est perdu, le client garde la transaction en attente).

Hors périmètre : validation réelle Google/Apple (sous-projet 2), restauration iOS, abonnements, remboursements, portefeuille multi-devises au-delà de `COIN`.

## Approche retenue

**Un document « économie » par joueur dans Cloud Save, écrit par lecture-modification-écriture conditionnelle (`writeLock`).**

Vérifié dans le SDK Cloud Code `Com.Unity.Services.CloudCode.Apis` 0.0.27 : `CloudSaveData.SetProtectedItemAsync(…, SetItemBody)` accepte un `writeLock` ; une écriture dont le `writeLock` ne correspond plus à l'élément stocké est refusée (conflit). Toutes les valeurs d'économie d'un joueur vivent dans **un seul élément**, ce qui rend chaque opération atomique sans transaction multi-clés.

Alternatives écartées :
- Une clé par devise ou par item : plusieurs écritures par opération, donc des états intermédiaires visibles et des échecs partiels.
- Un registre global des transactions en données « custom » : pas d'écriture « créer seulement si absent » et 2000 clés au plus par entité. Le sous-projet 2 évite ce besoin en liant chaque transaction au joueur.
- Un autre fournisseur (PlayFab, Firebase) : exclu par la règle du brief contre les nouveaux fournisseurs.

## Modèle de données

Élément Cloud Save **protected** du joueur, clé `ECONOMY`, en JSON :

```json
{
  "schemaVersion": 1,
  "currencies": { "COIN": 120 },
  "items": { "SMALL_BOMB": 2, "LARGE_BOMB": 1 },
  "infiniteHeartExpiresAtUnixMs": 0,
  "freeCoinPackClaimedAtUnixMs": 0,
  "appliedTransactions": [
    { "id": "fake:GPA.1", "productId": "COIN_PACK_100", "appliedAtUnixMs": 1761400000000 }
  ]
}
```

- `schemaVersion` permet les migrations futures : une version inconnue fait échouer l'opération, sans écraser les données.
- `appliedTransactions` garde les 200 dernières transactions livrées. Au-delà, la protection contre une seconde livraison repose sur le store (consommation et confirmation, sous-projet 2). La valeur 200 est une constante nommée.
- Absence de l'élément = nouveau joueur. L'état initial est créé depuis le catalogue, dans la même écriture conditionnelle (une création concurrente fait échouer l'une des deux écritures, qui relit et réessaie).
- `PlayerEconomyData` (le contrat vers le client) est calculé depuis ce document : devises, items à quantité non nulle, `HasPurchasedFreeCoinPack`, `InfiniteHeartsExpiryTimestamp`.

## Catalogue (Remote Config)

Clé Remote Config `ECONOMY_CATALOG`, déployée depuis `Assets/UGSConfigs/economy_catalog.rc` (même mécanisme que `fetched_keys.rc`, déjà déployé par l'éditeur) :

```json
{
  "currencies": { "COIN": { "initial": 100 } },
  "items": { "SMALL_BOMB": { "initial": 3 }, "INFINITE_HEART": { "initial": 0, "durationMinutes": 30 } },
  "freeCoinPack": { "rewards": [ { "id": "COIN", "amount": 10 } ] },
  "purchases": {
    "COIN_PACK_100": { "rewards": [ { "id": "COIN", "amount": 100 } ] },
    "BUNDLE_PACK":   { "rewards": [ { "id": "COIN", "amount": 1000 }, { "id": "LARGE_BOMB", "amount": 5 } ] }
  }
}
```

Les valeurs réelles sont reprises à l'identique des fichiers Economy actuels (`.ecr`, `.ecv`, bonus nouveau joueur de `PlayerEconomyService`). Le serveur lit le catalogue avec `RemoteConfigSettings.AssignSettingsGetAsync` (déjà utilisé par le module) et le met en cache en mémoire pendant 60 s, comme Unity le recommande pour les données qui changent peu (guide « In-memory cache » de Cloud Code). Un catalogue absent ou invalide fait échouer l'opération avec une erreur explicite, sans écriture.

Les identifiants de produits store (`coin_pack_100_play`…) restent côté client dans `GemHunterProductCatalog`. Le serveur ne connaît que l'identifiant catalogue.

## Composants du module Cloud Code

| Composant | Rôle | Dépend de |
| --- | --- | --- |
| `Economy/EconomyState` | Le document `ECONOMY` (DTO + version de schéma) | — |
| `Economy/EconomyCatalog` | Le catalogue Remote Config typé + validation | — |
| `Economy/EconomyRules` | Fonctions pures : créditer, dépenser si le solde suffit, ajouter/consommer un item, activer un cœur infini, réclamer le pack gratuit, appliquer un achat (idempotent par transaction), état initial | `EconomyState`, `EconomyCatalog` |
| `Economy/IEconomyStateStore` + `CloudSaveEconomyStateStore` | Lire l'état avec son `writeLock`, écrire sous condition ; un conflit est un résultat, pas une exception | Cloud Save |
| `Economy/EconomyTransaction` | Boucle lire → appliquer une règle → écrire sous condition, 5 tentatives au plus avec un court délai aléatoire, puis erreur « réessayer plus tard » | store, règles |
| `Economy/ICatalogProvider` + `RemoteConfigCatalogProvider` | Lecture et cache du catalogue | Remote Config |
| `Services/PlayerEconomyService` | Garde ses méthodes publiques actuelles ; les réimplémente par `EconomyTransaction`. Les helpers propres aux instances d'inventaire Economy disparaissent | les composants ci-dessus |
| `Purchases/…` | `HandlePurchase(productId, receipt, transactionId)` garde sa signature. Validation : faux store en `development` seulement, store réel → `RetryLater` jusqu'au sous-projet 2. Livraison : `EconomyRules.ApplyPurchase` | catalogue, transaction |

Les autres services (zones, récompenses quotidiennes, coffres, cadeaux) passent déjà par `PlayerEconomyService` : leur code ne change pas, sauf là où ils faisaient « lire le solde puis décrémenter » en deux appels. Ils utilisent alors l'opération atomique « dépenser si le solde suffit ».

## Flux d'erreur

| Situation | Réponse |
| --- | --- |
| Conflit d'écriture (appel concurrent) | Relire et réessayer, 5 tentatives au plus, puis `RETRY_LATER` |
| Solde insuffisant | Erreur métier `INSUFFICIENT_FUNDS`, aucune écriture |
| Catalogue absent ou invalide | `CONFIGURATION_ERROR`, aucune écriture, log serveur |
| `schemaVersion` inconnue | `CONFIGURATION_ERROR`, aucune écriture |
| Achat déjà appliqué pour ce joueur | Renvoie l'économie actuelle comme un succès (le client confirme la transaction) |
| Faux store hors `development` | `PURCHASE_REJECTED` (inchangé) |
| Reçu Google/Apple réel | `PURCHASE_RETRY_LATER` jusqu'au sous-projet 2 |
| Panne Cloud Save / Remote Config | `RETRY_LATER` |

Les erreurs gardent le préfixe actuel (`PURCHASE_REJECTED`…), dont le client dépend déjà. Logs : identifiant joueur et identifiant de transaction autorisés côté serveur ; jamais de reçu.

## Plateforme et outillage

- Module Cloud Code : `net6.0` → **`net8.0`**. Cloud Code accepte jusqu'à .NET 9 ; .NET 6 n'est plus maintenu depuis novembre 2024. Attention : .NET 8 et .NET 9 arrivent tous deux en fin de support le 10 novembre 2026, et Cloud Code n'annonce pas encore .NET 10. À revoir dès que Unity publie la prise en charge de .NET 10.
- `Com.Unity.Services.CloudCode.Apis` 0.0.16 → **0.0.27** (dernière stable), `Core` aligné sur la version qu'elle exige.
- Déploiement : fenêtre Deployment de l'éditeur, pilotée par la Unity CLI (méthode recommandée par Unity), du module et des fichiers `.rc`. D'abord dans `development`, puis dans `production` sur instruction.
- Environnement côté client : le nom codé en dur `production` dans `GameInitializer` devient `development` dans l'éditeur et les builds de développement, `production` sinon, avec un champ sérialisé pour forcer une valeur. Cela corrige le P1 du brief et rend le faux store utilisable dans l'éditeur.
- Client : retrait du package `com.unity.services.economy` et des fichiers de configuration Economy, après vérification qu'aucune référence n'existe (skill `unity-asset-safety`).

## Tests

- **Unitaires (xUnit, projet de tests existant)** : les règles pures, couvertes ligne par ligne sur les cas de la matrice du brief §12 :
  - créditer, dépenser, solde insuffisant ;
  - achat appliqué deux fois → une seule livraison ;
  - plafond de `appliedTransactions` ;
  - état initial depuis le catalogue ;
  - cœur infini actif ou expiré ;
  - pack gratuit réclamé une seule fois ;
  - version de schéma inconnue.
- **Transaction** : un store simulé qui renvoie des conflits → nouvelle tentative, abandon après 5 conflits, aucune écriture partielle.
- **Catalogue** : JSON valide ou invalide, cache.
- Les tests actuels liés aux instances d'inventaire Economy sont remplacés par leurs équivalents sur le nouveau modèle.
- **Intégration** : après déploiement dans `development`, parcours réel dans l'éditeur, par la Unity CLI :
  - connexion d'un nouveau joueur et soldes initiaux ;
  - achat faux store livré une fois ;
  - pack gratuit ;
  - victoire et défaite de niveau ;
  - amélioration de zone ;
  - récompense quotidienne.

## Documentation à mettre à jour

- Nouvelle décision `0008` : Economy remplacé par Cloud Save + Remote Config. Elle remplace la partie « Economy redeem » de la décision 0001.
- `Doc/backend-achats.md` : nouveau propriétaire de l'attribution, nouvelles erreurs.
- `Doc/dependencies.md` : .NET 8, SDK Cloud Code 0.0.27, retrait d'Economy.
- `CLAUDE.md` : environnements et règles de déploiement.
- README du package : section « Ce que le backend doit garantir » sans Economy.

## Risques

- **Volume** : un élément Cloud Save est limité en taille. Le document reste petit (quelques devises et items, 200 transactions au plus).
- **Contention** : un joueur n'a qu'un document. Des écritures fréquentes et concurrentes du même joueur (rare dans ce jeu) passent par les nouvelles tentatives.
- **Fin de support .NET 8 / 9 (10 novembre 2026)**, voir Plateforme et outillage.
- **Environnement `development`** : à créer par l'utilisateur dans le dashboard avant les tests d'intégration.
