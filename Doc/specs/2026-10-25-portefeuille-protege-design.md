# Portefeuille protégé sur Cloud Save — design

Date : 2026-10-25 · Statut : **à relire par le propriétaire**. Sa validation vaut aussi validation des recommandations A2 à A7 reprises ici (diagnostic `Doc/revue/2026-10-25-realignement-diagnostic.md`).

Remplace `2026-10-25-economie-cloud-save-design.md` (supprimée).

## Décisions acquises (2026-10-25)

- **A1-B** : le socle fournit un portefeuille protégé optionnel, géré par le serveur (décision 0008).
- **Persistance** : Cloud Save, classe protected. La création est protégée par un délai de grâce. Un adaptateur Stateful Cloud Code est prévu à la migration Unity 6.7 LTS.
- **Démo Gem Hunter conservée** : boutique de pièces et pack gratuit, améliorations de zones, fin de niveau et cœurs, récompenses quotidiennes.
- **Retirés** : loot box, amis, cadeaux et cœurs-cadeaux, classement, profils (nom, image).

## Objectif et critères de réussite

1. Plus aucun appel à Economy. Le package `com.unity.services.economy` et les fichiers `.ecr/.ecc/.eci/.ecv` sont retirés.
2. Soldes, objets, cœurs, étoiles, zones et état des récompenses quotidiennes sont **protected** : le joueur les lit, seul Cloud Code les écrit.
3. Chaque opération métier écrit toutes ses données en **une seule écriture atomique**, sans perte de mise à jour face aux appels concurrents. C'est un batch protected avec un writeLock par clé.
4. Une transaction d'achat valide est livrée **une seule fois, sans limite de durée** : une clé par transaction, jamais évincée.
5. Une requête de jeu répétée (réponse perdue) ne s'applique qu'une fois : identifiant d'opération fourni par le client.
6. Une initialisation concurrente ne peut écraser aucun état acquis (délai de grâce, §Écriture).
7. Le code réutilisable par un autre jeu vit dans une bibliothèque serveur sans aucun type Gem Hunter.

**Hors périmètre** (spec suivante, « validation des achats ») :
- vérification Apple (JWS) et Google (API Play Developer + Secret Manager) ;
- liaison `obfuscatedAccountId` / `appAccountToken` ;
- achats permanents et restauration ;
- remboursements et notifications store.

## Architecture

| Élément | Emplacement | Rôle |
| --- | --- | --- |
| `DevilCorp.MobileFoundation.Server` | `SharedPackages/Server/DevilCorp.MobileFoundation.Server/` (bibliothèque .NET, sans `UnityEngine` ni Gem Hunter) | `IProtectedStateStore` et `CloudSaveProtectedStateStore` ; `StateTransaction` (lire → règle pure → batch conditionnel → retries) ; initialisation sous délai de grâce ; `Wallet` (monnaies, objets, effets temporisés) et ses règles pures ; registre des achats (`PurchaseLedger`) ; `TransactionLogId` |
| Module Gem Hunter | `UnityProject/GemHunterUGSCloud/Project/` | Règles du jeu (niveaux, cœurs, étoiles, zones, quotidien, pack gratuit) écrites comme fonctions pures `(état, catalogue, horloge) → nouvel état \| erreur`. Les fonctions Cloud Code ne font qu'orchestrer |
| Tests | `SharedPackages/Server/Tests/` et `UnityProject/GemHunterUGSCloud/Tests/` | Règles pures, transaction (store simulé qui sait renvoyer des conflits, échouer et dupliquer), intégration en `development` |

Le module référence la bibliothèque par `ProjectReference`. À vérifier au premier déploiement : la publication déclenchée par la fenêtre Deployment (`.ccmr` → `.sln`) embarque bien la bibliothèque. Sinon, on la copie dans la solution du module.

## Modèle de données (Cloud Save, classe protected, par joueur)

| Clé | Contenu | Écrite par |
| --- | --- | --- |
| `WALLET` | `{schemaVersion, createdAtUnixMs, currencies{COIN}, items{…}, infiniteHeartExpiresAtUnixSec, freeCoinPackClaimed, recentOperations[≤50]}` | toutes les opérations qui touchent monnaie ou objets |
| `PLAYER_DATA` | Format actuel, sans `giftHearts` ni `displayName` | niveaux, zones |
| `DAILY_REWARDS_STATUS` | Format actuel, **déplacé de default vers protected** | quotidien |
| `PURCHASE_<sha256 hex de l'id de transaction>` | `{schemaVersion, store, productId, grants[], deliveredAtUnixMs}` | achat, dans le même batch que `WALLET` |

- Une `schemaVersion` inconnue fait échouer l'opération (`CONFIGURATION_ERROR`) sans aucune écriture. Une migration de schéma est une étape explicite, testée (skill `save-schema-migration`).
- Limites Cloud Save : 5 Mo et 2000 clés par joueur et par classe ; 600 requêtes/min par joueur. À partir de 1900 clés `PURCHASE_*`, l'achat est refusé avec `CONFIGURATION_ERROR` et une alerte est journalisée (risque accepté pour de petits jeux).

## Écriture

**Opération ordinaire** (`StateTransaction`) :
1. Lire les clés concernées, avec leur writeLock.
2. Appliquer la règle pure.
3. Écrire **un batch protected** avec, pour chaque clé existante, son writeLock. Seule une clé nouvelle et déterministe (`PURCHASE_*`) est écrite sans writeLock.
4. En cas de `409`, relire et réessayer, 5 tentatives au plus avec un court délai aléatoire, puis `RETRY_LATER`.

**Initialisation** (`EnsureInitializedAsync`, appelée par `OnSignInHandlePlayerInitialization` juste après la connexion, et par toute fonction qui trouve `WALLET` absent) :
- Elle n'écrit que si sa propre lecture, dans la même exécution, a trouvé `WALLET` absent.
- Elle écrit en un batch, sans writeLock, l'état initial de `WALLET`, `PLAYER_DATA` et `DAILY_REWARDS_STATUS`, avec `createdAtUnixMs` égal à l'heure de cette lecture.

**Délai de grâce** : toute mutation autre que l'initialisation est refusée (`RETRY_LATER`) tant que `now < WALLET.createdAtUnixMs + 30 000 ms`.

**Pourquoi c'est sûr :**
- Une fonction C# est tuée après 15 s. Une écriture d'initialisation fondée sur une lecture périmée atterrit donc au plus tard 15 s après cette lecture, plus le transit réseau.
- Aucune mutation n'est acceptée avant 30 s. Une initialisation périmée ne peut écraser qu'une autre initialisation, au contenu équivalent.
- Hypothèses à prouver par le spike :
  - écart d'horloge entre workers inférieur à la marge ;
  - aucune écriture en vol au-delà de la marge.
- Si le spike montre une vraie création conditionnelle (un writeLock réservé qui signifie « doit être absent »), elle remplace le délai de grâce.

## Idempotence

| Opération | Clé de déduplication | Durée |
| --- | --- | --- |
| Achat | Id de transaction du store → clé `PURCHASE_*` | Permanente. Déjà présente pour ce joueur → on renvoie l'état actuel comme un succès (le client confirme) |
| Pack gratuit | `WALLET.freeCoinPackClaimed` | Permanente |
| Quotidien | Jour courant dans `DAILY_REWARDS_STATUS` | Par jour (horloge serveur) |
| Zones, niveaux | `operationId` (GUID généré par le client à chaque action, réutilisé à chaque répétition) gardé dans `WALLET.recentOperations` (50 derniers) | Fenêtre de répétition. Une éviction ne peut que faire refuser ou rejouer une action du joueur sur ses propres ressources, et ne crée rien de vendable |

**Limite honnête :** « niveau gagné » est déclaré par le client. Le serveur ne peut pas prouver une victoire. La protection repose sur les plafonds existants (étoiles ≤ 25, cœurs ≤ 100) et sur `operationId`. Les biens **vendus** (pièces achetées), eux, ne dépendent d'aucune déclaration du client.

## Catalogue et configuration (A3, A6)

- **Achats** : un fichier `purchase_catalog.json` versionné dans le dépôt, incorporé au module comme ressource. Il associe à chaque id produit store (Google, Apple) et à l'id catalogue un type et des gains. **Le serveur dérive le gain de l'id produit du reçu**, jamais d'un id choisi librement par le client. Le client garde ses `StoreProductDefinition`. Un test vérifie que ses ids existent dans le fichier.
- **Réglages de jeu** (soldes initiaux, coût des zones, récompenses de zones et du quotidien) : Remote Config, fichiers `.rc` déployés par environnement (clés existantes `DAILY_REWARDS_CONFIG`, `AREA_COMPLETE_REWARDS`, plus `WALLET_DEFAULTS`).
  - Lecture serveur avec un cache de 60 s **par environnement**.
  - Règle : aucune campagne ni ciblage par joueur sur ces clés, sinon le cache serait faux.
  - Configuration absente ou invalide → `CONFIGURATION_ERROR`, sans écriture.
- **Environnement** : le serveur accepte le faux store seulement si `context.EnvironmentName == "development"` (déjà en place). Côté client, l'éditeur et les builds de développement utilisent `development`, la release `production`, avec un champ de forçage.

## Contrat client / serveur

Fonctions conservées :
- `OnSignInHandlePlayerInitialization`, `GetPlayerData`, `GetPlayerEconomyData` ;
- `HandlePurchase`, `HandleFreeCoinPackPurchase` ;
- `HandleLevelWon(operationId)`, `HandleLevelLost(operationId)` ;
- `HandleUnlock(…, operationId)`, `HandleUpgrade(…, operationId)`, `ProcessAreaCompleteCommandBatch(…, operationId)` ;
- `GetDailyRewardsStatus`, `ClaimDailyReward`.

Les paramètres `operationId` sont nouveaux : les bindings doivent être régénérés et les appels client adaptés.

`PlayerEconomyData` :
- même format ;
- `ItemInventory` contient **tous** les objets du catalogue, y compris à 0, parce que `StoreView` les indexe directement ;
- `InfiniteHeartsExpiryTimestamp` reste en **secondes** Unix (lu ainsi par `PlayerDataManager`).

Erreurs (préfixe du message d'exception, lu par le client) :

| Code | Sens | Réaction client |
| --- | --- | --- |
| `PURCHASE_REJECTED` | Reçu refusé définitivement | Ne pas confirmer, ne pas relancer |
| `PURCHASE_RETRY_LATER` | Achat non traitable pour l'instant | Garder en attente |
| `INSUFFICIENT_FUNDS` | Solde ou étoiles insuffisants | Message, aucune écriture |
| `RETRY_LATER` | Conflits épuisés, délai de grâce, panne Cloud Save / Remote Config | Relancer plus tard |
| `CONFIGURATION_ERROR` | Configuration absente, schéma inconnu, limite de clés | Log serveur, message générique |

## Achats pendant cette étape (A4)

- Faux store : livré en `development` seulement, dédupliqué par transaction comme un vrai achat.
- Vrai reçu Google ou Apple : `PURCHASE_RETRY_LATER`. Ce n'est **pas** une garantie sans perte : Google rembourse un achat non confirmé au bout de 3 jours.
- En conséquence, les builds hors développement **n'affichent pas** les achats réels tant que la spec « validation des achats » n'est pas livrée (option de configuration côté client).
- Aucune application n'est publiée, donc aucune transaction réelle en attente à reprendre. **À confirmer par le propriétaire.**

## Retraits

- **Module** : `LootBoxService`, `LootBoxCooldownService`, `FriendsService`, `PlayerGiftsService`, `GiftHeartCooldownService`, `AllPlayersService`, `HandleProfileChangeService`, `DailyRewardsMonthlyResetService` (code commenté), `PlayerEconomyService` et `PurchaseRedeemer` (Economy), et leurs modèles et tests.
- **Client** : dossiers `LootBoxWithCooldown/`, `Friends/`, `EditProfile/`, et les parties correspondantes du hub ; connexions Facebook et Game Center par défaut.
- Chaque retrait d'asset, de prefab ou de champ sérialisé suit `unity-asset-safety`, après recherche de références. Une scène ou un prefab partagé n'est modifié que par l'éditeur (Unity CLI), jamais en éditant le YAML à la main.
- **Données existantes** : aucun joueur réel ; les comptes de test en `production` peuvent être réinitialisés.

## Versions (A5)

- Module et bibliothèque : `net6.0` → `net8.0`, dans un commit séparé avant les changements fonctionnels.
- `Com.Unity.Services.CloudCode.Apis` 0.0.16 → 0.0.27, dans un commit séparé.
- .NET 8 est hors support le 2026-11-10. Migration Unity 6.7 LTS + Cloud Code 3.0 + .NET 10 dès qu'une 6.7 stable sort.

## Accès et exploitation

- Classe protected : écriture réservée au serveur. Test de refus avec un jeton client contre le service réel (spike, point 4).
- **Logs** : `context.CorrelationId`, `TransactionLogId`, `operationId`, nom de l'opération, résultat. Jamais de reçu, token, signature ni id de transaction brut.
- **Coût** : une opération = 1 lecture + 1 batch, plus les relances en cas de conflit. Le catalogue de jeu est mis en cache.
- **Suppression de compte** : ce qu'UGS fait des données Cloud Save à la suppression d'un joueur n'est pas vérifié [NV]. À établir avant publication.

## Tests

**Règles pures** (xUnit) :
- crédit, dépense, solde insuffisant ;
- cœurs et étoiles bornés ; cœur infini actif ou expiré ;
- pack gratuit réclamé une seule fois ; quotidien une fois par jour, mois écoulé ;
- déblocage et amélioration de zone ;
- schéma inconnu.

**Transaction** (store simulé) :
- conflit puis succès ;
- abandon après 5 conflits sans écriture partielle ;
- **réponse perdue après écriture, puis répétition** : une seule application (achat, `operationId`) ;
- **rejeu d'un achat après plus de 200 transactions** : aucun crédit ;
- **deux dépenses concurrentes au-delà du solde** : une seule acceptée ;
- **deux initialisations concurrentes avec une mutation pendant le délai** : la mutation est refusée et rien n'est perdu ;
- mutation après le délai : acceptée.

**Catalogue** : JSON valide ou invalide, gain dérivé de l'id produit store, ids client présents.

**Intégration en `development`** (après le spike, via la Unity CLI) :
- nouveau joueur ;
- achat faux store livré une fois, puis répété ;
- pack gratuit ;
- victoire et défaite ;
- amélioration de zone ;
- quotidien ;
- écriture protected avec un jeton client refusée.

**Non couvert ici** : sandbox Google et Apple, appareils réels.

## Ordre de réalisation

1. **Spike Cloud Save** en `development`. Bloqué tant que l'environnement n'existe pas.
2. Montées de version du module (net8, puis Apis 0.0.27).
3. Bibliothèque serveur, règles pures et transaction, en TDD.
4. Règles Gem Hunter et réécriture des fonctions conservées.
5. Retraits côté module, puis côté client, bindings régénérés.
6. Environnement client et masquage des achats réels hors développement.
7. Déploiement en `development` et intégration.

## Documentation à mettre à jour

- `Doc/backend-achats.md` : réécrit pour le nouveau propriétaire de l'attribution.
- `Doc/dependencies.md`, `CLAUDE.md` (organisation : bibliothèque serveur), README du package.
- Décision 0008 : statut mis à jour après validation de cette spec.

## Risques

- Hypothèses du délai de grâce (horloge, écritures en vol) : réduites par le spike, éliminées par Stateful Cloud Code après la migration 6.7.
- Contention : opérations du même joueur sur `WALLET`, absorbées par les relances ; rare dans un jeu solo.
- .NET 8 hors support le 2026-11-10.
- L'environnement `development` est à créer par le propriétaire.
