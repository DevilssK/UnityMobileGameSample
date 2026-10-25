# Réalignement du socle avec Unity Cloud — diagnostic et arbitrages

Date : 2026-10-25 · Statut : **diagnostic établi ; arbitrages A1 à A7 à valider**
Demande d'origine : `Doc/Claude-Revue-alignement-Unity-Cloud-2026-10-25.md`.

Niveaux de preuve utilisés : **[D]** fait documenté par une source officielle, **[O]** observation du dépôt ou de l'outillage local, **[I]** inférence, **[NV]** non vérifié.

## 1. Conclusion courte

1. La spec « Économie joueur sur Cloud Save » part d'un mauvais objectif : faire refonctionner **tout** Gem Hunter sans Economy. Elle reconstruit côté serveur des fonctions que le brief exclut (loot box, amis, cadeaux, récompenses quotidiennes…). Elle contredit aussi la décision 0001 (pas de portefeuille protégé en V1). Elle contient enfin quatre garanties fausses : création concurrente, idempotence limitée à 200 transactions, journalisation de l'identifiant de transaction, et `RetryLater` présenté comme « sans perte ».
2. Ce dont le socle a besoin côté serveur est plus petit : **valider les achats et en garder un registre durable**. Sans solde protégé côté serveur, chaque écriture devient un fait déterministe (« la transaction T du produit P a été livrée au joueur J »). Ces écritures peuvent se répéter ou se croiser sans dommage. La course à la création initiale disparaît, et le registre ne perd rien après 200 transactions.
3. Stateful Cloud Code n'est pas utilisable sur la base actuelle. Il exige Unity 6.7 (en bêta), Cloud Code 3.0 et .NET 10. Ses garanties de commit et de rollback ne sont pas documentées. Aucun suivi n'est proposé tant qu'un besoin de solde serveur n'est pas retenu.
4. Recommandation : confirmer la décision 0001 (arbitrage A1). Gem Hunter devient un hôte de gameplay local : ses fonctions méta serveur sortent du template et le serveur ne garde que la validation d'achat. Cette suppression importante attend ta validation.

## 2. Corrections du point de départ (sources vérifiées le 2026-10-25)

| Sujet | Constat | Niveau |
| --- | --- | --- |
| Economy | « As of September 8, 2026, Economy no longer accepts new project sign-ups. […] Existing projects with an active Economy configuration are unaffected. » Unity recommande Cloud Code + Cloud Save + Remote Config, et Unity IAP pour les achats réels. ([source](https://docs.unity.com/en-us/economy)) | [D] |
| Economy dans **ce** projet UGS | Le déploiement des ressources Economy depuis l'éditeur répond `403 Forbidden` (console de l'éditeur, 2026-10-25). Le message du dashboard « no longer accepting new project signups » a été rapporté par le propriétaire. | [O] ; la cause « projet sans configuration Economy active » est une [I] |
| Module déployé | Le module Cloud Code a été déployé en `production` le 2026-10-25 (instruction explicite). Il appelle encore Economy redeem : un achat réel y échouerait au redeem. Le statut renvoyé (`RetryLater` ou `Rejected`) n'a pas été observé. | [O] / [NV] |
| Cloud Save, batch | `Set … Item Batch` : jusqu'à 20 clés, « considered atomic and if any of the set key operation fails, the entire operation is failed », writeLock par clé, `409` en cas de conflit. Variante multi-classes d'accès (100 clés) : atomique **par classe d'accès** seulement. ([source](https://docs.unity.com/en-us/oas-cloud-save/1.0.0)) | [D] pour les batches default et public ; la section protected n'a pas pu être lue, sa sémantique identique est une [I] à vérifier |
| Cloud Save, writeLock | « omitting this field ignores write conflicts » ; `null` écrase sans condition. **Aucune écriture « créer seulement si absent » n'est documentée.** La suppression accepte un writeLock. ([source](https://docs.unity.com/en-us/cloud-save/concepts/write-locks)) | [D] |
| Cloud Save, limites | 5 Mo et 2000 clés par joueur et par classe d'accès ; 600 requêtes/min par joueur (`429` + `Retry-After`). | [D] |
| SDK serveur | `SetProtectedItemBatchAsync` (jeton Cloud Code) existe **déjà** dans `Com.Unity.Services.CloudCode.Apis` 0.0.16, version actuelle du module ; `DeleteProtectedItemAsync(…, key, writeLock)` aussi. La 0.0.27 dépend de `Core` 0.0.7. | [O] (réflexion sur les DLL) |
| Stateful Cloud Code | Scope Player (TTL 2 ans par défaut) ; « Calls into the same scope type and scope ID run one at a time, whichever module they target ». Exige Unity 6.7+, Apis ≥ 0.0.27, Core ≥ 0.0.9. Commit durable, rollback après exception, limites de taille et prix : **non documentés**. ([source](https://docs.unity.com/en-us/cloud-code/3.0.0/modules/stateful-cloud-code/state-scopes)) | [D] ; Core 0.0.9 introuvable sur NuGet (dernière version vue : 0.0.7, août 2026) : [NV] |
| Cloud Code 3.0 | Publié le 2026-10-23 : « requires Unity 6000.7 or newer », « Cloud Code Modules now target .NET 10 » ([changelog](https://docs.unity3d.com/Packages/com.unity.services.cloudcode@3.0/changelog/CHANGELOG.html)). La page des modules de la même doc 3.0.0 dit encore « up to version 9 is supported » ([source](https://docs.unity.com/en-us/cloud-code/3.0.0/modules/overview)). | [D], **contradiction entre deux sources Unity** |
| Chemin actuel | Package client Cloud Code 2.10.4 (`"unity": "2021.3"`), éditeur 6000.6.7f1 : runtime des modules ≤ .NET 9. Le module est en net6.0 (hors support Microsoft depuis novembre 2024) ; .NET 8 et .NET 9 arrivent en fin de support le 2026-11-10. | [O] ; dates Microsoft : [D] |
| Unity 6.7 | Proposé en bêta. Ne pas l'adopter sans décision. | [D] |
| Validation IAP | « Unity IAP doesn't provide a built-in remote validation service ». Apple : `OrderInfo.Apple.jwsRepresentation` ; Google : `Payload` du reçu ([source](https://docs.unity.com/en-us/iap/receipt-validation)). Le service de commandes Unity ne couvre que D2C et Webshop ([source](https://docs.unity.com/en-us/iap/introduction)). | [D] |
| Identifiant Google | `IGoogleOrderInfo.PurchaseToken` « has the same value as `IOrderInfo.TransactionID` » ([source](https://docs.unity3d.com/Packages/com.unity.purchasing@5.4/api/UnityEngine.Purchasing.IGoogleOrderInfo.html)) : **l'identifiant de transaction est un token, à ne pas journaliser**. | [D] — corrigé, voir §9 |
| Liaison au joueur | IAP 5.4.4 fournit `IGooglePlayStoreExtendedService.SetObfuscatedAccountId(string)` et `AppleStoreExtendedService.SetAppAccountToken(Guid)`. | [O] (code du package) |
| Remote Catalog | Présent dans IAP 5.4.4 (`RemoteCatalogProvider`, lu par le client via UGS). Recommandé à la place du catalogue codeless, obligatoire pour D2C. Le schéma décrit `uSKU`, type, prix, `storeIdOverrides`, et **aucun gain (payout)**. La lecture du catalogue par un serveur n'est pas documentée. ([catalogues](https://docs.unity.com/en-us/iap/catalogs), [schéma](https://docs.unity.com/en-us/iap/catalog-schema)) | [D] / [O] ; lecture serveur [NV] |
| Notifications entrantes | Triggers documente des webhooks **sortants** et des événements UGS (planificateur, `signed-up`, `key-saved`…). Aucun point d'entrée HTTP pour les notifications Apple/Google ou les callbacks LevelPlay n'est documenté. ([source](https://docs.unity.com/en-us/triggers/tutorials/define-triggers/trigger-structure)) | [D] (absence dans la doc lue) |
| Google, achats non confirmés | Un achat non confirmé (acknowledged) sous 3 jours est remboursé et révoqué ([source](https://developer.android.com/google/play/billing/integrate)). | [D] |

## 3. Revue des documents, plans et questions

| Élément | Verdict | Justification |
| --- | --- | --- |
| Brief `CLAUDE_UNITY_MOBILE_FOUNDATION.md` | Conserver ; une correction | Toujours aligné sur le but. §8.3 citait Economy redeem comme exemple de propriétaire : note ajoutée sur sa fermeture. |
| Décision 0001 (permanents + pub locale, portefeuille plus tard) | Conserver la politique ; corriger | La politique reste la bonne base (A1). Seul « propriétaire de l'attribution = Economy redeem » tombe : remplacé par la proposition 0008 (à valider). |
| Décisions 0002, 0003, 0004, 0006, 0007 | Conserver | Hypothèses inchangées. |
| Décision 0005 (Unity 6000.6.7f1) | Conserver ; corriger une conséquence | « 6.7 annoncée » devient « 6.7 en bêta ; Cloud Code 3.0, Stateful et .NET 10 l'exigent ». |
| Spec `2026-10-25-economie-cloud-save-design.md` | **Suspendue ; à supprimer si A1 est validé** | Objectif hors du but du socle. Garanties fausses corrigées dans le texte, statut mis à jour. |
| Sous-projet 2 « validation sans Economy » (annoncé, pas écrit) | Conserver le besoin ; reformuler | C'est le vrai besoin serveur du socle. Il devient la spec unique « validation et registre des achats » (A2, A3, A7). |
| `Doc/backend-achats.md` | Corriger | Décrit un flux Economy qui ne peut pas tourner sur ce projet UGS. Les hypothèses 2 à 9, propres à Economy, deviennent sans objet si A1/A2 sont validés. |
| `Doc/dependencies.md` | Corriger | Economy inutilisable ici, .NET 6 hors support, contradiction Cloud Code 3.0, Remote Catalog. |
| Plans `2026-10-24-backend-p0-achats`, `2026-10-24-client-achats-iap5`, `2026-10-25-pubs-recompensees-et-doc-package` | **Supprimés** | Exécutés et fusionnés ; ils contenaient des consignes Economy devenues fausses. Historique Git conservé. |
| README du package | Corrigé | Garanties backend sans Economy, identifiant de transaction non journalisable, limite de `RetryLater` sur Google. |
| Fonctions `SayHello` / `TestFunction` du module | **Supprimées** | Restes du sample, exposés et jamais appelés. |
| Code Economy (`PlayerEconomyService`, `PurchaseRedeemer`, package `com.unity.services.economy`, fichiers `.ecr/.ecc/.eci/.ecv`) | À valider (A1) | Inutilisable sur ce projet ; son retrait dépend du remplaçant. |
| Fonctions Cloud Code et code client des loot box, amis, cadeaux, classement, profils, récompenses quotidiennes, améliorations de zones | À valider (A1) | Hors périmètre du brief (§2) ou dépendantes d'un portefeuille serveur. |
| Question « portefeuille persistant ? » (brief §15) | Reformulée en A1 | Doit être tranchée maintenant, puisque le remplaçant d'Economy en dépend. |
| Question « confirmation S2S des pubs » (0001, 0007) | Conserver, dormante | Utile seulement si une récompense protégée apparaît. Aucun point d'entrée HTTP UGS n'est documenté. |
| Question « recevoir les notifications Apple/Google dans UGS » | Conserver, dormante | Même constat. À rouvrir quand remboursements et révocation seront activés (brief §8.5). |
| Étape D du brief (analytics, consentement, persistance locale, diagnostics) | Conserver | Socle obligatoire, non commencé. La persistance locale devient porteuse si A1 est validé. |
| Mineurs différés Ads et Purchases (README, « Limites connues ») | Conserver | Documentés, sans impact sur les garanties d'attribution. |
| `com.unity.collab-proxy`, ancien projet UGS `cd9a8e0f` (module vulnérable du sample) | Question au propriétaire | Hors code ; à trancher par toi. |

## 4. Périmètre : socle, modules optionnels, hôte

| Niveau | Contenu proposé | Statut |
| --- | --- | --- |
| Socle obligatoire | Identity (session anonyme UGS), Purchases (client IAP 5 + validation serveur + registre durable), Ads (cœur), Persistence locale versionnée, Analytics, Privacy/consentement, Diagnostics, configuration par environnement | Purchases et Ads faits côté client ; le reste suit l'étape D |
| Modules optionnels | Adaptateur LevelPlay (fait) ; Remote Config ; synchronisation Cloud Save ; **portefeuille protégé** seulement si un jeu le justifie (A1) | — |
| Hôte Gem Hunter | Match-3 jouable en local ; démonstration du bouton pub et de la boutique ; méta (pièces, cœurs, zones) en sauvegarde locale | À valider (A1) |
| Hors template | Loot box, amis, cadeaux, classements, profils, récompenses quotidiennes serveur, Facebook/Game Center comme connexions par défaut | À valider (A1) |

Observation [O] : le gameplay du match-3 (`GemHunterMatch/Scripts/UI/UIHandler.cs`) lit `PlayerEconomyManager` et les modèles générés par Cloud Code. Le match-3 dépend donc aujourd'hui de la méta serveur. Le fonctionnement hors ligne n'a pas été vérifié [NV].

## 5. Matrice de responsabilités

| Besoin | Capacité native Unity | Code métier restant | Version compatible | Preuve |
| --- | --- | --- | --- | --- |
| Achat, attente, confirmation, reprise au démarrage | Unity IAP 5 (`StoreController`, `PendingOrder`, `ConfirmPurchase`) | Coordinateur du package (fait) | IAP 5.4.4 / 6000.6.7f1 | Tests `dotnet` 23/23 ; éditeur avec le faux store ; **aucun sandbox** |
| Catalogue client (ids par store) | Catalogue local (`ProductDefinition`) ou Remote Catalog | Liste des produits du jeu | IAP 5.4.4 | Compilé ; Remote Catalog non essayé |
| Validation Apple | Aucune côté Unity pour le natif ; Apple fournit le JWS signé (`jwsRepresentation`) | Vérification de la chaîne de certificats, `bundleId`, `environment`, `productId`, `appAccountToken` | Cloud Code C# ≤ .NET 9 | Manquante |
| Validation Google | Aucune côté Unity ; API Google Play Developer avec compte de service | Appel `purchases.products.get`, vérification `packageName`, `productId`, `purchaseState`, `obfuscatedExternalAccountId` | Cloud Code + Secret Manager | Manquante ; clé de compte de service à fournir |
| Secret Google | Secret Manager lisible depuis Cloud Code (`ISecretClient`) | — | Apis 0.0.16+ [I] | Manquante |
| Registre durable, une attribution par transaction | Cloud Save protected : batch atomique de 20 clés, writeLock, 2000 clés par joueur | Clé par transaction, valeur déterministe | Apis 0.0.16 (déjà là) | Manquante ; à prouver en `development` |
| Refus d'un reçu d'un autre joueur | Liaison store : `obfuscatedAccountId` (Google), `appAccountToken` (Apple) | Fixer l'id avant l'achat ; comparer côté serveur | IAP 5.4.4 | Manquante |
| Faux store refusé hors développement | `IExecutionContext.EnvironmentName` | `FakeStorePolicy` (fait, côté serveur) | — | Tests unitaires ; pas encore exercé dans un vrai environnement `development` |
| Sélection d'environnement client | `InitializationOptions.SetEnvironmentName` | Choix éditeur/dev → `development`, release → `production` | Core 1.18.0 | À faire ; l'environnement `development` n'existe pas encore |
| Déploiement module et config | Fenêtre Deployment (éditeur) ; Unity CLI pour piloter l'éditeur ; UGS CLI (non installée, compte de service requis) | — | Deployment 1.7.2 (API 1.1.3), Cloud Code 2.10.4 | Déploiement production réussi le 2026-10-25 |
| Récompense pub locale | LevelPlay 9.5.1 | Coordinateur du package (fait) | — | Tests 15/15 ; pas de vraie pub, clés factices |
| Récompense pub protégée, remboursements | Aucun point d'entrée HTTP entrant documenté dans UGS | — | — | Hors V1 (0001) |
| Portefeuille protégé (si A1-B) | Cloud Save + writeLock, **sans création conditionnelle** ; ou Stateful (6.7+) | Règles, transactions, migrations | voir §6 | Non engagé |

## 6. Comparaison des options de persistance

À lire selon A1. Si le portefeuille protégé n'est pas retenu, seule la ligne « registre d'achats » est nécessaire, et la comparaison ne débouche pas sur un chantier.

| | Cloud Save protected, document unique | Cloud Save protected, batch de clés | Stateful Cloud Code (scope Player) |
| --- | --- | --- | --- |
| Mécanismes Unity réels | writeLock par élément, conflit `409` | Batch atomique ≤ 20 clés du même joueur et de la même classe, writeLock par clé | Appels sérialisés par scope ; persistance gérée |
| Création initiale | **Non sûre** : omettre le writeLock écrase. Deux créations concurrentes, avec une mutation entre les deux, perdent la mutation | Même limite pour une clé à état mutable. Sans risque si la valeur est un fait déterministe (registre) | Sérialisée par scope [D] ; moment du commit [NV] |
| Atomicité d'une opération métier | Oui, une seule écriture | Oui pour ≤ 20 clés d'un joueur ; jamais entre joueurs | Oui dans un appel [I] ; effets dans d'autres services non couverts |
| Idempotence durable | Bornée par la taille du document (le « 200 » de la spec) | Une clé par transaction, sans éviction (≤ 2000 clés par joueur) | À concevoir ; TTL 2 ans |
| Contention | Toutes les opérations d'un joueur se disputent un writeLock | Seulement les clés touchées | Appels en file ; risque d'interblocage documenté |
| Versions | Base actuelle (Apis 0.0.16) | Base actuelle | Unity 6.7 (bêta), Cloud Code 3.0, .NET 10, Core 0.0.9 introuvable |
| Code métier | Règles + boucle de retries + migration de schéma | Faits déterministes ; boucle seulement pour un état mutable | Règles + migration du schéma sérialisé |
| Inconnues bloquantes | Création conditionnelle impossible à prouver | Sémantique du batch **protected** à vérifier en `development` | Commit, rollback, interruption, limites, prix |
| Verdict | Écarté pour le registre ; possible pour un portefeuille seulement avec une preuve de création sûre | **Recommandé pour le registre d'achats** | Pas de suivi tant qu'aucun besoin de solde serveur n'est retenu ; à réévaluer avec la migration vers Unity 6.7 LTS |

## 7. Arbitrages à valider

### A1 — Périmètre de l'économie serveur (structurant)

- **Recommandé : A1-R.** Confirmer la décision 0001 : pas de portefeuille protégé dans le socle V1.
  - Le serveur valide les achats et tient le registre.
  - Achats permanents : validés et enregistrés côté serveur.
  - Consommables, si un jeu en vend : livrés une fois par transaction vers l'état local du jeu, non protégé et de faible valeur. Le serveur renvoie la même livraison à chaque répétition, et le client ne l'applique qu'une fois par `LogId`.
  - Gem Hunter : abandon de l'objectif « tout refonctionne sans Economy ». Le match-3 et sa méta (pièces, cœurs, zones) passent dans la sauvegarde locale versionnée (étape D). Les fonctions serveur hors périmètre et leur code client sortent du template, et le package `com.unity.services.economy` est retiré.
  - Impacts :
    - plus de 3 000 lignes du module en moins (services hors achats) ;
    - plus aucune course à la création ;
    - un tricheur ne triche que contre lui-même (jeu solo).
  - Coût : refonte des scènes du hub Gem Hunter, à faire sous `unity-asset-safety`.
- **A1-B.** Module optionnel « portefeuille protégé » minimal (pièces et items) sur Cloud Save. Il faut d'abord prouver une création initiale sûre ; aucun mécanisme documenté n'existe aujourd'hui (§6). Coût : règles, retries, migrations, tests de concurrence en `development`.
- **A1-C.** Spec actuelle (tout Gem Hunter côté serveur). Déconseillé : elle reconstruit des fonctions exclues par le brief et garde la course à la création.
- Travaux dépendants : suppression ou réécriture de la spec économie, retrait d'Economy, refonte du hub, sauvegarde locale.

### A2 — Registre des achats (si A1-R)

- **Recommandé.** Une clé protected par transaction, `PURCHASE_<sha256 complet de l'id de transaction>`. Valeur : `{schemaVersion, store, productId, type, grant, deliveredAtUnixMs}`. Elle est écrite dans un batch, avec la clé de droit `ENTITLEMENT_<productId>` pour un permanent.
  - Répétition ou appels simultanés : même valeur réécrite ; le résultat livré est renvoyé.
  - Aucune éviction.
  - Au-delà de 2000 clés par joueur : refus explicite et alerte. Le risque est accepté pour des petits jeux.
- Alternative : document unique borné (spec actuelle). Écartée, parce qu'une transaction évincée peut être livrée une seconde fois.
- Alternative : registre global en données custom. Écartée : pas de création conditionnelle, et la liaison au joueur (A7) suffit contre la réutilisation par un autre joueur.

### A3 — Autorité du catalogue

- **Recommandé.** Un seul fichier JSON versionné dans le dépôt : id catalogue, ids par store, type, gain. Il est lu par le client (`TextAsset`) et embarqué dans le module Cloud Code (ressource incorporée). Le serveur part de l'id produit **lu dans le reçu vérifié** et en déduit le gain. Un test vérifie la cohérence du fichier.
- Remote Catalog : non retenu pour l'instant.
  - Son schéma ne porte pas de gain.
  - Sa lecture par un serveur n'est pas documentée.
  - Pour les stores natifs, les prix restent fixés dans les consoles Google et Apple.
  - Le réévaluer si D2C ou Webshop entrent dans le périmètre.
- Remote Config pour les gains : non retenu. Il ajoute une seconde source et des règles de ciblage par joueur qui obligeraient à adapter le cache.

### A4 — Transition des achats réels

- **Recommandé.** Tant que la validation Apple et Google n'est pas livrée :
  - aucun achat réel n'est proposé hors `development` (boutons masqués par configuration) ;
  - le serveur refuse les vrais reçus avec `RetryLater`, en sachant que ce n'est pas une garantie « sans perte » (Google rembourse après 3 jours).
- À confirmer par toi : aucune application publiée, donc aucune transaction réelle en attente à reprendre [NV].

### A5 — Versions

- **Recommandé.** Rester sur 6000.6.7f1, Cloud Code 2.10.4 et le chemin .NET ≤ 9.
  - Module : net6.0 → net8.0, au plus près de l'implémentation de la validation.
  - Apis : 0.0.16 → 0.0.27 dans un commit séparé (pas obligatoire pour le batch, déjà présent en 0.0.16).
  - Planifier la migration Unity 6.7 LTS + Cloud Code 3.0 + .NET 10 dès qu'une 6.7 stable sort : net8 et net9 sont hors support Microsoft le 2026-11-10.
- Alternative : attendre la 6.7 LTS sans toucher au module. Écartée, car net6 est déjà hors support.
- Rejeté : Unity 6.7 bêta.

### A6 — Environnements

- Déjà validé par le brief (P1). L'éditeur et les builds de développement utilisent `development`, la release utilise `production`, avec un champ de forçage.
- Le faux store reste refusé par le serveur hors `development` ; c'est déjà le cas.
- Un catalogue absent ou invalide déclenche `CONFIGURATION_ERROR` sans écriture.
- **Dépend de toi** : créer l'environnement `development`. Tant qu'il n'existe pas, l'éditeur ne peut pas basculer dessus.

### A7 — Identité et restauration

- **Recommandé.**
  - Consommables : liés au joueur. Un reçu d'un autre joueur est refusé.
  - Permanents : liés au compte store. Une restauration sur un nouveau compte anonyme (réinstallation) réattribue le droit au joueur courant et l'enregistre (« transféré depuis »), puisque le payeur est le titulaire du compte store.
  - Pas de liaison de compte (Google Play Games, Sign in with Apple) dans le socle V1.
- Alternative : refuser toute restauration vers un autre joueur. Écartée, car un joueur qui réinstalle perdrait ses achats permanents.
- Alternative : liaison de compte obligatoire. Coût et fournisseurs supplémentaires.

## 8. Plan de preuve (périmètre A1-R)

| Scénario | Niveau | Applicable | État |
| --- | --- | --- | --- |
| Deux premières requêtes simultanées avec mutation intermédiaire | Intégration `development` | **Sans objet** en A1-R : aucun état mutable serveur. Obligatoire si A1-B | — |
| Deux dépenses concurrentes au-delà du solde | — | Sans objet en A1-R (pas de solde serveur) | — |
| Réponse perdue après écriture, puis répétition | Règles pures + intégration | Oui | À écrire |
| Rejeu après plus de 200 transactions | Règles pures | Oui (pas d'éviction) | À écrire |
| Quotidien, coffre, zone cohérents après panne | — | Sans objet si ces fonctions sortent du template ; sinon test de sauvegarde locale | — |
| Deux droits permanents simultanés | Intégration (batch) | Oui | À écrire |
| Reçu invalide, mauvaise application, produit, environnement ou bénéficiaire | Règles pures + sandbox | Oui | Règles à écrire ; sandbox impossible sans comptes store |
| Restauration, changement d'appareil ou de compte | Sandbox + appareil | Oui (A7) | Impossible ici sans appareil ni comptes de test |
| Écriture protected avec un jeton client | Intégration `development` (REST avec jeton joueur) | Oui | Bloqué : environnement `development` absent |
| Configuration absente, panne réseau, échec de confirmation store | Règles + adaptateur | Oui | Partiellement couvert côté client (coordinateur) |
| Stateful : exception, timeout, interruption, migration, TTL | — | Sans objet (non retenu) | — |

Rappel : faux store, backend simulé et serveur local ne valent pas une validation Apple ou Google.

## 9. Fait pendant cette revue

- **Sécurité** (`fd64afc`) : l'identifiant de transaction (le purchase token sur Google) n'est plus journalisé, ni par le package ni par le module. Les deux journalisent une empreinte `TransactionLogId` commune (SHA-256 tronqué). Tests : package 23/23, backend 93/93, compilation Unity sans erreur.
- **Nettoyage** (`ab51ae5`) : suppression des fonctions `SayHello` et `TestFunction`.
- **Documentation** : ce diagnostic ; proposition 0008 (à valider) ; corrections du README du package, de la spec économie, de `backend-achats.md`, `dependencies.md`, des décisions 0001 et 0005 et du brief §8.3 ; suppression des trois plans exécutés.
- **En attente de validation** : A1 à A7, puis la spec « validation et registre des achats » qui remplacera la spec économie.
