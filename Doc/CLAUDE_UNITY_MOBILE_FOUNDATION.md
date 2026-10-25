# Brief de développement — Socle Unity mobile à partir de Gem Hunter

Version du document : 1.0 — 24 octobre 2026  
Destinataire : Claude, agent de développement du projet  
Statut : spécification de travail ; aucune implémentation n’est déclarée terminée par ce document.

## 1. Mission et décisions actées

Transformer progressivement le sample **GemHunterMatch3** du dépôt Unity fourni en un projet de référence professionnel, réutilisable pour plusieurs petits jeux mobiles. Le propriétaire a choisi de **partir de ce dépôt**. Ne pas substituer un nouveau projet sans rapport avec le sample, ni effectuer une réécriture intégrale par défaut.

- Dépôt : <https://github.com/Unity-Technologies/com.unity.services.samples.use-cases>
- Projet source : `GemHunterMatch3/`
- Code client de services : `GemHunterMatch3/Assets/GemHunterUGS/Scripts/`
- Backend C# : `GemHunterMatch3/GemHunterUGSCloud/`
- Configurations UGS : `GemHunterMatch3/Assets/UGSConfigs/`
- Référence de la revue préalable : commit `575661c6f83e721e9f20d27f6c66e7249a929889`.

Le résultat attendu comprend :

1. Un **projet template** permettant de démarrer un nouveau jeu.
2. Un **package Unity partagé et versionné** contenant les fonctions communes hors gameplay.
3. Un **backend réutilisable**, avec configurations et données isolées par jeu.
4. Une **scène de démonstration technique** qui exerce les intégrations sans dépendre du match-3.
5. Des tests ciblés, des builds reproductibles et une documentation d’exploitation.

Le choix Unity/C# est acté. UGS, Unity IAP et LevelPlay constituent le point de départ cohérent avec le dépôt. Ne pas ajouter Firebase, un autre backend ou une autre médiation publicitaire sans besoin concret démontré.

## 2. Contexte produit et limites du périmètre

Les jeux visés sont solo, principalement 2D ou en 3D simple, avec peu de ressources et une boucle de jeu courte. Références produit : Geometry Tower, Stack, Block Blast et 2248. S’inspirer de leur simplicité de gameplay ; ne pas reproduire automatiquement leurs fonctions commerciales ou sociales complètes.

La première version doit intégrer :

- Publicités récompensées.
- Achats intégrés.
- Analytics produit.
- Rapports d’erreurs et de crash.
- Gestion des consentements nécessaire aux services retenus.
- Sauvegarde locale fiable.
- Validation et suivi fiables des achats côté serveur.
- Configuration Android et iOS, avec vérification sur appareils réels.

Le gameplay doit pouvoir fonctionner localement. Une panne du backend ou de l’analytics ne doit pas empêcher une partie lorsque celle-ci ne nécessite aucune opération protégée.

### Hors périmètre par défaut — YAGNI

Multijoueur, système social, amis, clans, chat, battle pass, loot boxes, événements saisonniers, abonnements, boutique web, économie entre plusieurs jeux, catalogue distant complexe, framework générique de gameplay, ECS/DOTS, serveur de simulation, microservices et orchestration Kubernetes.

Le sample contient certaines de ces fonctions. Les retirer du chemin obligatoire du template ; conserver une référence Git avant suppression. Les rendre optionnelles seulement si un besoin réel le justifie. « Optionnel » ne signifie pas initialisé ou embarqué dans tous les builds.

## 3. Lecture de ce brief et niveau de preuve

La revue préalable portait sur le code et la configuration au commit indiqué. Aucun build mobile ni paiement réel n’a été exécuté. Les observations ci-dessous ne constituent pas un audit exhaustif de sécurité.

Avant de modifier : identifier le commit courant, lire les consignes applicables du dépôt, relever les différences avec cette référence et vérifier si les défauts signalés existent encore. Ne pas corriger une ancienne version à l’aveugle.

Les versions observées étaient : Unity `6000.1.7f1` dans `ProjectVersion.txt`, Unity IAP `4.12.2`, LevelPlay `8.9.0`, Analytics `6.1.0`. Le README racine annonçait des essais sous Unity 6.3 pour Gem Hunter. Cette différence doit être résolue par une vérification réelle de compatibilité.

Choisir et fixer une version Unity maintenue et un ensemble compatible de SDK. Vérifier les documentations officielles lors du développement ; ne pas traiter les numéros ci-dessus comme des recommandations actuelles. Séparer une migration majeure de SDK d’un refactoring fonctionnel autant que possible.

## 4. Principes de développement — DRY, KISS, YAGNI et SOLID

| Principe | Application attendue | Dérive à éviter |
| --- | --- | --- |
| DRY | Une seule implémentation des flux communs et une source d’autorité par règle/configuration | Copier le module d’achat dans chaque jeu ; définir les mêmes récompenses à plusieurs endroits |
| KISS | Flux explicites, composition simple, modules peu nombreux, API lisible | Bus global omniprésent, magie de réflexion, framework maison disproportionné |
| YAGNI | Implémenter les besoins de cette V1 et les extensions déjà justifiées | Ajouter plusieurs fournisseurs ou une abstraction universelle pour un futur hypothétique |
| Responsabilité unique | Séparer paiement, attribution, affichage et analytics | Une classe qui initialise tous les SDK et décide toutes les récompenses |
| Ouvert/fermé | Ajouter des produits et placements par configuration dans les capacités prévues | Modifier le cœur du package pour chaque jeu |
| Substitution | Simulations et fournisseurs respectent les mêmes garanties publiques | Une simulation toujours réussie qui masque annulations et erreurs |
| Ségrégation des interfaces | Contrats petits, centrés sur un usage | Une interface globale exposant tous les services |
| Inversion des dépendances | Le jeu consomme les contrats du socle ; les adaptateurs encapsulent les SDK | Le package partagé importe les classes de gameplay |

Créer des interfaces aux frontières utiles : SDK externe, persistance, horloge pour logique temporelle testée, backend. Ne pas créer une interface et une factory pour chaque classe. Une implémentation concrète suffit lorsqu’aucun remplacement ni test ne nécessite une abstraction.

Une ressemblance de code ne prouve pas une responsabilité commune. Préférer deux petits comportements explicites à une abstraction générique mal définie.

## 5. Corrections prioritaires du sample

Tous les chemins ci-dessous sont relatifs à `GemHunterMatch3/`. Reproduire et documenter les comportements sur environnement de test avant de conclure à leur résolution.

| Priorité | Constat préalable | Traitement attendu |
| --- | --- | --- |
| P0 | `GemHunterUGSCloud/Project/Services/StoreService.cs` accepte une branche `fake` qui saute la validation puis accorde des récompenses | Exclure ce chemin du backend de production. Un type de store fourni par le client ne doit jamais permettre de désactiver une vérification |
| P0 | Dans ce service, `PurchaseDataSignature` reçoit `transactionId` et les données sont transmises sans extraction correcte du payload Google | Utiliser le format exact attendu par l’API retenue, vérifier les réponses et les erreurs structurées ; tests Google Play sandbox |
| P0 | La branche Apple attend `apple`, alors que les enveloppes Unity utilisent habituellement `AppleAppStore` ; le reçu complet est passé plutôt que le payload attendu | Vérifier le format réel de la version IAP retenue et corriger l’adaptateur Apple ; ne pas transposer les hypothèses de l’ancienne API vers StoreKit 2 |
| P0 | `Redeem…PurchaseAsync` est suivi de `GrantRewards`, tandis que les fichiers `.ecr` définissent déjà les récompenses | Un seul chemin d’attribution. Vérifier le risque de double crédit ; une opération de redeem Economy n’est pas une simple validation sans effet |
| P0 | Un achat déjà utilisé provoque une erreur sans restitution claire du résultat précédemment livré | Rendre la reprise idempotente : reconnaître l’achat du même bénéficiaire et retrouver le résultat durable. Ne jamais considérer tout doublon, notamment d’un autre joueur, comme un succès |
| P0 si économie protégée | `Ads/AdRewardManagerClient.cs` génère un jeton côté client ; `AdRewardsService.cs` le contrôle sans preuve signée du réseau | Choisir explicitement entre récompense locale de faible valeur et confirmation serveur authentifiée. Ne pas présenter le jeton client comme preuve de visionnage |
| P0 si économie protégée | Le service publicitaire crédite puis stocke le dernier jeton dans une opération séparée | Garantir l’absence de double crédit face aux appels simultanés, reprises et pannes intermédiaires |
| P1 | `Store/StoreClient.cs` ne réinitialise pas `m_IsPurchaseInProgress` dans ses callbacks d’échec | Réinitialiser l’état sur tous les chemins terminaux : échec, annulation, réussite ; tester un nouvel achat après échec |
| P1 | La reprise après échec d’achat est laissée en commentaire et les reçus complets sont journalisés | Ajouter une reprise contrôlée et supprimer les données sensibles des logs |
| P1 | `Core/GameInitializer.cs` fixe `production` et compose directement des systèmes de zones, de profils et Facebook | Configuration explicite par environnement ; bootstrap indépendant du match-3 ; fonctionnalités activables |
| P1 | Package Analytics installé, sans couche d’événements métier identifiée dans les scripts examinés | Implémenter le plan de mesure et le contrôle de collecte ; ne pas assimiler présence du SDK et intégration complète |
| P1 | `Core/LocalStorageSystem.cs` utilise des écritures directes de fichiers et des modèles propres au jeu | Écriture atomique adaptée à la plateforme, sauvegarde de secours, schéma versionné et migrations testées |
| P1 | Pas de suite de tests automatisés métier identifiée dans le sous-projet | Ajouter des tests des invariants et incidents critiques, avec procédures de validation sur stores |

P0 : bloque l’usage en production du flux concerné. P1 : nécessaire avant de qualifier le template de prêt à publier.

Les corrections doivent porter sur le comportement, pas uniquement sur le renommage des classes ou les commentaires. Ne pas supposer que déplacer une opération dans Cloud Code la rend sûre : vérifier authentification, autorisations et données d’entrée.

## 6. Architecture cible et stratégie de migration

### 6.1 Organisation initiale proposée

Conserver le dépôt source comme base et préserver l’historique. Exemple d’organisation cible, à adapter sans casser les références Unity :

| Chemin | Rôle |
| --- | --- |
| `GemHunterMatch3/` | Projet source devenu hôte d’intégration, puis template nettoyé |
| `SharedPackages/com.studio.mobilefoundation/` | Package commun : `Runtime/`, `Editor/`, `Tests/`, `package.json`, documentation |
| `GemHunterMatch3/GemHunterUGSCloud/` | Backend existant refactorisé ; emplacement conservé au début |
| `docs/` | Décisions, configuration, incidents, intégration d’un nouveau jeu |
| `Samples/` | Éventuel second projet minimal de validation de réutilisation |

`Studio` est un nom provisoire à remplacer une seule fois par le nom de namespace/package retenu. Ne pas créer plusieurs noms concurrents pour le même socle.

Utiliser d’abord une dépendance UPM locale vers le package pendant l’extraction. Pour les jeux consommateurs, distribuer des versions immuables par Git avec chemin de package ou registre privé. Ne pas dépendre d’une branche flottante `main` pour une version publiée.

Créer seulement les assemblies utiles à l’isolation : runtime, éditeur et tests ; ajouter une séparation par adaptateur si elle permet réellement de retirer une dépendance. Les bibliothèques serveur restent compatibles avec leur propre cible .NET et ne dépendent pas de `UnityEngine`.

### 6.2 Modules du package

| Module | Responsabilité |
| --- | --- |
| Core | Composition, cycle de vie, états d’initialisation et configuration |
| Identity | Session invitée et récupération/liaison de compte si nécessaire |
| Purchases | Catalogue client, achat, restauration, communication de validation |
| Ads | Disponibilité, chargement, affichage et résultat des publicités récompensées |
| Privacy | État des consentements, propagation aux SDK et retrait |
| Analytics | Événements communs, schéma et prévention des doublons |
| Persistence | Sauvegarde locale versionnée et mécanisme de migration |
| Diagnostics | Logs structurés et adaptateur vers le service d’erreurs |

Remote Config, Cloud Save et économie persistante restent des capacités optionnelles si le premier jeu en a besoin. Une définition « partagée » n’oblige pas chaque jeu à activer tous les modules.

### 6.3 Frontières

- Le gameplay utilise les contrats publics ; il n’appelle pas directement Unity IAP, LevelPlay ou les clients cloud.
- Les adaptateurs traduisent les réponses fournisseurs en résultats internes explicites.
- Les vues présentent l’état ; elles ne valident pas un paiement et ne créditent pas un portefeuille protégé.
- Le bootstrap compose les services et leurs dépendances. Limiter l’accès global par `GameSystemLocator` durant la migration, puis l’éliminer des nouveaux modules lorsque l’injection explicite suffit.
- Pas de dépendance du package vers `GemHunter`, `AreaManager`, un type de gemme ou une scène du jeu.
- Les récompenses permanentes et monétaires sont décidées par le backend ; les effets de gameplay locaux restent décidés par le jeu.

Contrats possibles, sans obligation de figer exactement ces noms : `IPurchaseService`, `IRewardedAdsService`, `IAnalyticsService`, `ISaveService`, `IConsentService`.

Ne pas répliquer toutes les API des fournisseurs. Exposer uniquement les opérations nécessaires aux jeux. Définir les états d’indisponibilité, d’annulation, d’attente et d’échec ; éviter un unique booléen de résultat.

## 7. Conventions de code et de nommage

Le code et les identifiants sont en anglais. La documentation de travail peut rester en français. Conserver les conventions existantes dans les zones non modifiées ; appliquer une convention unique au package extrait.

| Élément | Convention | Exemple |
| --- | --- | --- |
| Namespace | PascalCase, hiérarchie stable | `Studio.MobileFoundation.Purchases` |
| Classe, struct, enum | PascalCase, nom concret | `PurchaseResult`, `PurchaseStatus` |
| Interface | Préfixe `I` | `IPurchaseService` |
| Méthode et propriété | PascalCase | `RestorePurchasesAsync`, `IsInitialized` |
| Paramètre et variable locale | camelCase | `productId`, `purchaseResult` |
| Champ privé | `_camelCase` | `_purchaseService` |
| Constante | PascalCase | `MaxRetryAttempts` |
| Méthode asynchrone | Suffixe `Async` | `InitializeAsync` |
| Événement | PascalCase, événement métier explicite | `PurchaseCompleted`, `AvailabilityChanged` |
| Fichier C# | Nom du type principal | `PurchaseService.cs` |
| Test | Méthode_Situation_Résultat | `GrantPurchase_WhenRetried_DoesNotGrantTwice` |
| Événement analytics et attributs | snake_case | `purchase_completed`, `product_id` |
| Package UPM | Minuscules, préfixe organisation | `com.studio.mobilefoundation` |

Règles complémentaires :

- UTF-8, indentation de 4 espaces, accolades Allman, `.editorconfig` versionné.
- Un type public principal par fichier ; limiter la visibilité à `internal` ou `private` lorsque possible.
- Champs Unity sérialisés privés avec `[SerializeField]` ; ne pas exposer un champ public uniquement pour l’Inspector.
- Lors d’un renommage de champ sérialisé, préserver les données avec `FormerlySerializedAs` si applicable. Préserver les `.meta`, GUID, prefabs et références de scènes lors des déplacements.
- Ne pas renommer les identifiants de produits déjà publiés, clés de sauvegarde ou contrats réseau pour satisfaire une convention esthétique ; prévoir une migration explicite.
- Éviter `Manager`, `Helper` ou `Utils` sans responsabilité précise. Préférer `PurchaseService`, `PurchaseValidator`, `SaveMigrator`.
- Utiliser les capacités C# réellement supportées par la version Unity retenue ; ne pas supposer la parité avec le runtime du backend.
- Commenter les invariants et raisons des choix ; ne pas paraphraser chaque ligne. Documenter les contrats publics et effets de bord importants.
- Ne pas modifier du code généré ni le code d’un SDK tiers ; régénérer les bindings depuis leur source.

### Asynchronisme et cycle de vie

- Utiliser `Task`/`Task<T>` pour les services asynchrones et accepter un `CancellationToken` lorsqu’il est pertinent.
- Réserver `async void` aux callbacks imposés ; capturer et traiter leurs exceptions.
- Ne pas bloquer le thread Unity avec `.Wait()` ou `.Result`.
- Les callbacks externes manipulant des objets Unity doivent revenir sur le thread principal.
- S’abonner et se désabonner de façon symétrique. Gérer destruction, changement de scène, pause et reprise.
- Annuler l’attente de l’interface ne doit pas abandonner la livraison d’un achat déjà payé.
- Prévoir délais d’expiration et reprises bornées pour les erreurs transitoires ; aucune boucle infinie silencieuse.

## 8. Achats et backend professionnel minimal

### 8.1 Responsabilités serveur

Vérifier l’identité authentifiée, l’application, le store, le produit, l’état du paiement et le bénéficiaire. Ne jamais faire confiance à un montant, une récompense ou un `playerId` librement envoyé par le client.

Les clés privées et secrets des stores restent dans un gestionnaire de secrets côté serveur. Les identifiants publics nécessaires aux SDK ne sont pas des secrets, mais doivent être configurés par jeu et environnement.

Vérifier les règles d’accès UGS : un client ne doit pas pouvoir contourner Cloud Code pour modifier un solde ou un droit protégé. Tester ce refus au niveau des API, au-delà de l’absence de bouton dans l’interface.

### 8.2 Cycle de transaction

1. Le joueur déclenche l’achat via Unity IAP.
2. Une transaction payée/en attente de livraison est transmise au backend.
3. Le backend la vérifie avec le mécanisme officiel adapté au store et au SDK.
4. Il livre ou retrouve une livraison déjà enregistrée au même bénéficiaire.
5. Le client reçoit un résultat exploitable et actualise l’affichage.
6. La transaction est finalisée auprès du store selon le chemin documenté retenu.

Les états différés/en attente de paiement ne donnent pas lieu à attribution. Définir clairement qui effectue acknowledgement/consommation/finalisation sur chaque plateforme ; éviter deux implémentations concurrentes qui se contredisent.

**Invariant : une transaction valide donne lieu à une seule attribution logique, même après dix requêtes, une panne ou deux appels simultanés.**

La clé de déduplication utilise l’identifiant autoritatif adapté au store, avec portée store/application/environnement. Un ID arbitraire fourni par le client ne suffit pas. Rejeter les preuves d’un autre environnement ou d’une autre application.

### 8.3 Attribution et reprise

Choisir un propriétaire unique de l’attribution : par exemple Economy redeem, ou un autre mécanisme documenté. Ne pas appeler redeem puis créditer les mêmes ressources manuellement.

> Note du 2026-10-25 : Unity Economy n’accepte plus de nouveaux projets depuis le 2026-09-08 et répond 403 sur le projet UGS du template. Le remplaçant proposé (validation Cloud Code + registre Cloud Save) est en attente de validation : `Doc/decisions/0008-remplacement-economy.md`.

L’idempotence exige un état durable et une gestion de concurrence. Si validation, registre et crédits sont répartis entre plusieurs services, ne pas prétendre disposer d’une transaction atomique globale. Définir une machine à états et une réconciliation des opérations partielles, en réutilisant les garanties natives du fournisseur lorsqu’elles existent.

Une réponse réseau perdue après livraison doit permettre de retrouver le succès antérieur. Un événement « déjà utilisé » par un autre joueur doit rester un refus.

### 8.4 Droits, consommables et hors ligne

- Achats permanents : restaurables selon le compte store ; cache local pour l’utilisation hors ligne suivant la politique produit.
- Consommables : suivre les quantités livrées et dépensées ; les stores ne restaurent pas un portefeuille de consommables déjà livrés.
- Portefeuille protégé : crédits **et dépenses** contrôlés par le serveur. Valider seulement l’achat puis accepter un solde local librement modifiable ne protège pas l’économie.
- Une dépense de monnaie protégée nécessite la connexion pour la V1. Ne pas ajouter une synchronisation économique hors ligne complexe par défaut.
- Pour un portefeuille ou une sauvegarde serveur récupérable, prévoir la liaison/récupération d’identité ; une identité anonyme perdue à la désinstallation ne suffit pas.
- Ne pas promettre une restauration entre Android et iOS ou entre plusieurs jeux sans une règle produit et un mécanisme dédiés.

### 8.5 Remboursements et exploitation

Recevoir et authentifier les notifications nécessaires du store, les dédupliquer et appliquer une politique explicite de révocation ou de correction. Prévoir les notifications retardées, désordonnées et répétées, ainsi qu’une réconciliation si nécessaire.

Ne pas imposer automatiquement un solde négatif ou retirer arbitrairement la progression : documenter la règle retenue pour un consommable déjà dépensé.

Le support doit pouvoir retrouver une transaction sans accéder à des reçus complets dans les logs. Prévoir un identifiant de corrélation, les états de traitement et une procédure de reprise.

## 9. Publicités récompensées

Le module gère chargement, disponibilité, affichage, absence d’inventaire, échecs et reprise. Une publicité indisponible ne bloque pas la partie.

Définir la politique par placement :

| Placement | Autorité | Comportement recommandé |
| --- | --- | --- |
| Seconde chance locale sans valeur persistante | Callback de récompense du SDK, risque accepté | Accorder une seule fois à l’événement de récompense, jamais à la simple fermeture |
| Monnaie/inventaire serveur protégé | Confirmation authentifiée du fournisseur | Vérifier, dédupliquer et attribuer côté serveur ; client en attente si nécessaire |

Pour la confirmation serveur, vérifier les capacités réelles de LevelPlay et des réseaux retenus, ainsi que leur protocole officiel. Ne pas réutiliser un protocole AdMob pour LevelPlay par analogie.

Relier la confirmation au joueur et au placement attendus ; déterminer la récompense depuis une configuration de confiance. Prévoir retard de confirmation, application fermée et nouvel essai. Ne pas créditer une fois sur callback client puis une seconde fois sur confirmation serveur.

Le cooldown local sert à l’expérience utilisateur. Les limites d’une économie protégée reposent sur une horloge et un état de confiance côté serveur.

Utiliser les modes et identifiants de test pendant le développement. Les builds de production ne doivent pas activer les suites de test du SDK.

## 10. Analytics, consentement et diagnostics

### 10.1 Plan de mesure minimal

Définir un dictionnaire d’événements versionné : signification, source, moment d’émission et propriétés autorisées. Exemple de base à adapter :

| Événement | Usage |
| --- | --- |
| `tutorial_completed` | Comprendre l’entrée dans le jeu |
| `run_started`, `run_completed` | Sessions de gameplay, résultat et durée |
| `rewarded_ad_requested`, `rewarded_ad_unavailable` | Comprendre la demande et la disponibilité |
| `rewarded_ad_reward_granted` | Mesurer l’attribution effective |
| `purchase_started`, `purchase_failed` | Diagnostiquer le parcours d’achat |
| `purchase_completed` | Mesurer une transaction validée/livrée selon une définition unique |

Vérifier les événements automatiques du SDK avant d’ajouter leur équivalent. Dédupliquer les mesures de revenu et séparer les données de test. L’analytics ne constitue jamais une preuve de paiement ou une autorité d’attribution.

Le package fournit le transport et les événements communs ; chaque jeu ajoute ses événements de gameplay sans modifier le cœur.

### 10.2 Confidentialité et fonctionnement dégradé

- Identifier les obligations et réglages applicables aux SDK, marchés et publics réellement visés ; s’appuyer sur leurs documentations actuelles.
- Centraliser l’orchestration des consentements tout en conservant les choix distincts par finalité/fournisseur.
- ATT, consentement publicitaire et autorisation de collecte analytics ne sont pas interchangeables.
- Initialiser/activer chaque collecte dans l’ordre requis ; gérer refus, modification et retrait.
- Éviter toute collecte non nécessaire, notamment email, nom réel, reçu complet et identifiant publicitaire dans les événements métier.
- Une indisponibilité d’analytics ou de crash reporting ne bloque ni le gameplay ni la livraison d’un achat.
- Ne pas construire de proxy analytics maison sans besoin précis.

### 10.3 Diagnostics

Logs structurés avec gravité, module et identifiant de corrélation ; pas de secrets, tokens, signatures ou reçus bruts. Distinguer erreurs transitoires, annulations normales et violations d’invariants. Fournir symboles nécessaires aux diagnostics des builds publiés.

## 11. Sauvegarde, mobile et configuration

### Sauvegarde

Une enveloppe de sauvegarde contient une version de schéma. Écrire via un fichier temporaire et un remplacement approprié à la plateforme ; conserver une copie de secours exploitable. Tester les fichiers incomplets/corrompus et la récupération.

Les migrations sont explicites et testées. Éviter la désérialisation polymorphique arbitraire depuis un fichier modifiable par le joueur ; utiliser des DTO et types autorisés. Le chiffrement local ne constitue pas une autorité serveur ni une garantie anti-triche.

Sauvegarder aux transitions pertinentes et lors de la mise en arrière-plan ; ne pas dépendre uniquement de la fermeture normale. Si Cloud Save est activé, définir la résolution des conflits et empêcher l’écrasement d’un état serveur protégé par une ancienne sauvegarde locale.

### Mobile

- UI adaptée aux ratios d’écran, safe areas et zones tactiles.
- Pause/reprise cohérentes, y compris après publicité, passage au store et perte de focus.
- Profilage sur Android modeste et iPhone réel, en build proche de la release.
- Objectif initial : 60 images/s sur l’appareil de référence ; 30 images/s possible si acceptable pour le gameplay.
- Mesurer aussi démarrage, mémoire, taille du build, chauffe et stabilité d’une session prolongée.
- Ne pas confondre faible nombre de polygones et faible coût : textures, transparences, allocations et SDK comptent aussi.
- Choisir les API graphiques et versions minimales d’OS selon les SDK réellement embarqués ; distinguer minimum d’exécution et version cible requise par le store.
- Prévoir macOS/Xcode ou un runner macOS adapté pour les builds iOS.

### Configuration

Isoler par jeu : application/bundle ID, projet UGS, produits store, placements publicitaires, analytics et données. Isoler développement/test/production et éviter les mesures ou transactions de test dans la production.

Les catalogues et identifiants publics sont configurables ; les secrets sont injectés par l’environnement sécurisé. Ne pas imposer un backend multi-tenant commun à tous les jeux : partager le code et automatiser le déploiement suffit pour commencer.

## 12. Stratégie de tests et critères d’acceptation

Tester les règles et incidents à risque plutôt que reproduire chaque ligne du code. Les tests simulés ne remplacent pas les essais sandbox des stores ni les appareils réels.

| Scénario | Résultat attendu |
| --- | --- |
| Achat valide Android/iOS | Produit attendu livré une seule fois ; transaction finalisée correctement |
| Paiement différé/en attente | Aucun droit accordé avant validation du paiement |
| Annulation/échec | UI débloquée ; nouvel achat possible |
| Application fermée après paiement | Livraison récupérée au prochain démarrage |
| Réponse perdue après attribution | Même résultat retrouvé, aucun second crédit |
| Requêtes simultanées identiques | Une seule attribution logique |
| Reçu invalide, mauvais produit/application/environnement | Refus sans crédit |
| Reçu associé à un autre joueur | Refus ou procédure explicite, jamais réattribution silencieuse |
| Store simulé en production | Rejet garanti côté serveur |
| Restauration après réinstallation | Droits restaurables récupérés ; limites des consommables documentées |
| Remboursement/notification répétée | Politique appliquée une seule fois et traçable |
| Pub fermée sans récompense | Aucun crédit |
| Récompense pub signalée deux fois | Une attribution logique |
| Confirmation publicitaire tardive | Récompense récupérable sans double crédit |
| Pas de réseau / pas de publicité | Partie locale possible, message et actions disponibles cohérents |
| Écriture directe client sur économie protégée | Refus de l’API |
| Consentement refusé/retiré | Collectes concernées arrêtées selon les exigences du SDK |
| Sauvegarde interrompue/corrompue | Récupération de secours ou défaut contrôlé sans crash |
| Migration de sauvegarde | Progression conservée selon le contrat de migration |
| Second jeu consommateur | Utilise le socle sans référence aux types/scènes Gem Hunter |

Automatisation minimale : tests C# de logique, tests Unity nécessaires, compilation serveur, validation de configuration et build Android. Build iOS et essais intégrés sur runner adapté. Séparer jobs PR, tests de staging et release signée ; ne pas exposer de secrets de production aux contributions non fiables.

## 13. Plan d’exécution par étapes

### Étape A — Établir la référence

Lire les consignes, inventorier les dépendances réelles, compiler l’état initial lorsque l’environnement le permet, relever les défauts existants et conserver un point de retour Git. Documenter les SDK externes à obtenir et la configuration UGS nécessaire. Vérifier les licences du code et des ressources réutilisées.

Livrable : état initial reproductible, matrice des dépendances et backlog priorisé. Ne pas annoncer une compilation réussie si Unity ou les SDK manquent.

### Étape B — Corriger la monétisation et les frontières de confiance

Traiter les P0, corriger les états d’achat, choisir l’autorité d’attribution et la politique publicitaire. Ajouter les tests de reprise, doublons et concurrence. Isoler les environnements.

Livrable : parcours d’achat et récompenses cohérents, démontrés sur staging/sandbox pour les plateformes disponibles.

### Étape C — Extraire le package commun

Déplacer progressivement les services hors du match-3, préserver les références Unity, introduire les petits contrats nécessaires, supprimer les dépendances inversées et mettre la configuration à la place des constantes propres au jeu.

Livrable : package utilisé par le projet hôte et scène de démonstration indépendante.

### Étape D — Compléter la V1 du socle

Ajouter analytics, consentement, diagnostics, persistance robuste et documentation. Nettoyer du template les fonctions non retenues et leurs dépendances inutiles après vérification des références.

Livrable : template minimal configuré et automatisation des vérifications.

### Étape E — Prouver la réutilisation

Créer un second hôte très simple, sans développer un jeu complet. Brancher achat, publicité, sauvegarde et analytics uniquement par contrats/configuration. Vérifier qu’une correction du package peut être adoptée par les deux hôtes en changeant sa version.

Livrable : preuve de réutilisation et première version stable du package. Ne généraliser davantage que si ce second usage révèle un besoin réel.

## 14. Versionnement, livraison et documentation

- Versionner le package selon SemVer ; documenter les changements incompatibles et migrations.
- Fixer versions moteur, packages et outils de build ; conserver les lockfiles pertinents.
- Chaque jeu choisit quand adopter une nouvelle version du socle. Aucune mise à jour automatique de tous les jeux vers `main`.
- Le backend doit tolérer les anciennes versions client encore distribuées, ou appliquer une politique de fin de support explicite. Éviter les ruptures silencieuses de contrats.
- Prévoir séparation des déploiements, rollback et migration des données ; un rollback de code n’annule pas automatiquement une migration persistante.
- Garder les commits de correction, extraction et mise à niveau lisibles. Ne pas formater l’intégralité du sample sans nécessité.

Documentation attendue :

| Document | Contenu |
| --- | --- |
| `README.md` du template | Prérequis, ouverture, lancement et build |
| Guide de nouveau jeu | Identifiants à changer, modules à activer, produits et environnements à configurer |
| Architecture | Responsabilités, flux d’achat/pub et frontières de confiance |
| Guide de déploiement backend | Configuration UGS, secrets, droits, staging et production |
| Guide de tests | Commandes exactes, sandbox, appareils et scénarios manuels |
| Dictionnaire analytics | Événements, schémas, règles de consentement et déduplication |
| Guide d’incident | Achat bloqué, notification perdue, panne fournisseur, récupération |
| Changelog | Versions du socle, migrations et limites connues |

Enregistrer les décisions structurantes en courtes notes : problème, choix, alternatives pertinentes et conséquences. Ne pas produire une documentation d’architecture plus lourde que le système.

## 15. Instructions de travail pour Claude

1. Lire ce brief et inspecter le dépôt courant avant de proposer des changements.
2. Considérer la transformation du sample comme l’objectif ; avancer par étapes exécutables et révisables.
3. Commencer par les risques de monétisation et les frontières de confiance, pas par un renommage massif.
4. Réutiliser une intégration existante lorsqu’elle est correcte ; remplacer une partie uniquement si le besoin est établi.
5. Garder le premier socle petit. Aucun fournisseur supplémentaire ou système métier hors périmètre sans justification.
6. Préserver les modifications du propriétaire, les ressources utiles et les GUID Unity. Ne pas publier, déployer en production ou lancer des transactions réelles sans instruction correspondante.
7. Écrire des tests ciblés pour les invariants financiers, la reprise et les migrations ; éviter les tests qui ne font que recopier l’implémentation.
8. Vérifier les références officielles des SDK réellement retenus ; ne pas inventer d’API ni appliquer des exemples d’une autre version sans adaptation.
9. Rapporter séparément : implémenté, compilé, testé automatiquement, vérifié sur appareil, vérifié en sandbox, restant à faire.
10. Si un outil, compte, SDK ou appareil manque, terminer le travail indépendant possible et préciser le blocage exact. Ne pas présenter une simulation comme une validation réelle.

### Décisions produit encore ouvertes

Le propriétaire n’a pas encore fixé les produits vendus, les récompenses de chaque placement, la nécessité d’un portefeuille persistant, les fournisseurs de connexion, les pays/publics visés ni la politique de restauration entre plateformes.

Continuer l’extraction et les corrections indépendantes de ces réponses. Pour la démonstration, utiliser des produits sandbox et des récompenses de test explicitement identifiés. Avant de figer l’économie, faire préciser si les achats sont permanents, consommables ou les deux. Ne pas ajouter des abonnements par supposition.

## 16. Définition de « prêt à servir de template »

- [ ] Le projet dérive du dépôt fourni et conserve sa traçabilité et ses notices applicables.
- [ ] Les défauts P0 pertinents ont été vérifiés puis corrigés avec preuves de validation.
- [ ] Le package commun ne dépend plus du gameplay Gem Hunter.
- [ ] Les services fonctionnent dans un hôte minimal distinct.
- [ ] Les configurations par jeu/environnement sont explicites, sans secrets dans le client.
- [ ] Achats, reprises, doublons et restauration ont été vérifiés selon la matrice.
- [ ] La politique de récompense publicitaire est explicite et conforme à son niveau de protection.
- [ ] Les consentements contrôlent effectivement les collectes concernées.
- [ ] La sauvegarde supporte interruption et migration.
- [ ] Les builds Android/iOS ciblés sont vérifiés ; les éventuelles plateformes non validées sont signalées comme telles.
- [ ] Les budgets de performance sont définis puis mesurés sur les appareils de référence.
- [ ] Les tests et procédures de build sont reproductibles depuis un checkout propre.
- [ ] Une mise à jour du package peut être adoptée séparément par chaque jeu.
- [ ] Les fonctions exclues n’imposent plus de dépendances ou de services actifs au template.

## 17. Sources et points d’entrée

Les liens documentaires peuvent évoluer. Le commit fixe les observations de code ; consulter les guides actuels lors des migrations.

- [README Gem Hunter au commit examiné](https://github.com/Unity-Technologies/com.unity.services.samples.use-cases/blob/575661c6f83e721e9f20d27f6c66e7249a929889/GemHunterMatch3/README.md)
- [StoreService.cs](https://github.com/Unity-Technologies/com.unity.services.samples.use-cases/blob/575661c6f83e721e9f20d27f6c66e7249a929889/GemHunterMatch3/GemHunterUGSCloud/Project/Services/StoreService.cs)
- [StoreClient.cs](https://github.com/Unity-Technologies/com.unity.services.samples.use-cases/blob/575661c6f83e721e9f20d27f6c66e7249a929889/GemHunterMatch3/Assets/GemHunterUGS/Scripts/Store/StoreClient.cs)
- [AdRewardManagerClient.cs](https://github.com/Unity-Technologies/com.unity.services.samples.use-cases/blob/575661c6f83e721e9f20d27f6c66e7249a929889/GemHunterMatch3/Assets/GemHunterUGS/Scripts/Ads/AdRewardManagerClient.cs)
- [AdRewardsService.cs](https://github.com/Unity-Technologies/com.unity.services.samples.use-cases/blob/575661c6f83e721e9f20d27f6c66e7249a929889/GemHunterMatch3/GemHunterUGSCloud/Project/Services/AdRewardsService.cs)
- [GameInitializer.cs](https://github.com/Unity-Technologies/com.unity.services.samples.use-cases/blob/575661c6f83e721e9f20d27f6c66e7249a929889/GemHunterMatch3/Assets/GemHunterUGS/Scripts/Core/GameInitializer.cs)
- [Dépendances observées](https://github.com/Unity-Technologies/com.unity.services.samples.use-cases/blob/575661c6f83e721e9f20d27f6c66e7249a929889/GemHunterMatch3/Packages/manifest.json)
- [Unity IAP — Validation](https://docs.unity.com/en-us/iap/receipt-validation)
- [Unity IAP — Gestion des achats](https://docs.unity.com/en-us/iap/purchases)
- [Unity Economy — Achats et attribution](https://docs.unity.com/en-us/economy/sdk/purchases)
- [Google Play Billing — Sécurité](https://developer.android.com/google/play/billing/security)
- [Apple — Validation des transactions et transition depuis verifyReceipt](https://developer.apple.com/documentation/storekit/validating-receipts-with-the-app-store)
- [Apple — Notifications serveur](https://developer.apple.com/documentation/AppStoreServerNotifications/receiving-app-store-server-notifications)
- [Unity Analytics — Confidentialité et consentement](https://docs.unity.com/en-us/analytics/privacy-and-consent/manage-data-privacy)

Ce document sert de contrat de travail pour développer et vérifier le socle. Il ne certifie ni l’état actuel du sample, ni la conformité d’un futur build, ni la réussite de tests non exécutés.
