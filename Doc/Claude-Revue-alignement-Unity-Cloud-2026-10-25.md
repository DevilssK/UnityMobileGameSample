# Note de transmission à Claude — Réalignement du socle Unity mobile avec Unity Cloud

Date : 25 octobre 2026  
Projet : UnityMobileGameSample  
Statut : mission de revue, mise à jour documentaire et implémentation  
Document à transmettre tel quel à Claude.

## 1. Mission et cadre de travail

Reprends l’analyse de l’architecture du projet, de la spec « Économie joueur sur Cloud Save », des plans, décisions, hypothèses et questionnements déjà écrits, à partir du but recherché et des capacités réelles des services Unity disponibles aujourd’hui.

Le but reste de construire une **boilerplate réutilisable pour plusieurs petits jeux mobiles**, avec un package commun indépendant du gameplay, un backend réutilisable et une démonstration technique. Il faut réutiliser les fonctions natives pertinentes et limiter le code spécifique aux responsabilités du jeu.

Tu es le développeur du projet. Tu es **autorisé à modifier la documentation et le projet** pour réaliser ce réalignement : spécifications, décisions, code client et serveur, tests, configurations et dépendances lorsque leur évolution est justifiée. Inspecte d’abord le dépôt et les sources officielles, confronte les plans existants au besoin réel, puis prépare les choix et changements proposés. Avance de manière autonome sur les corrections et les détails d’implémentation qui respectent le périmètre et les décisions validés.

**Les choix importants nécessitent ma validation explicite avant leur mise en œuvre.** Cela comprend notamment un changement d’architecture ou de persistance, de modèle économique ou de garanties d’achat, de fournisseur, une migration majeure d’éditeur ou de SDK, l’adoption d’une version bêta, la suppression d’une fonctionnalité jusque-là prévue ou un changement affectant les données existantes ou les coûts d’exploitation de manière significative. Pour chaque arbitrage, présente une recommandation concrète, les alternatives pertinentes, les raisons et les impacts sur la réutilisation, la complexité, la maintenance et la migration. Tu peux préparer et modifier les propositions documentaires, en les marquant « à valider » ; ne présente pas une décision en attente comme acceptée. Attends ma réponse pour les changements qui en dépendent, tout en poursuivant les travaux indépendants. Une décision déjà validée n’a pas à être soumise de nouveau si ses hypothèses et sa portée restent inchangées.

Respecte les règles du dépôt, les invariants d’achat, les GUID Unity et les modifications existantes. Sépare les évolutions de versions des refactorings fonctionnels lorsque nécessaire. Ne publie pas, ne déploie pas en production et ne lance pas de transaction réelle sans instruction explicite. Signale les véritables arbitrages de périmètre ou les garanties impossibles à établir, tout en poursuivant les travaux indépendants.

Les conclusions ci-dessous sont elles aussi à challenger. Elles ne valent ni décision d’architecture acceptée ni preuve de fonctionnement en production. Corrige ou retire celles qui ne sont plus fondées ; documente les propositions et les validations obtenues. Aucun plan, choix ancien ou recommandation de cette note ne doit être conservé uniquement parce qu’il a déjà été écrit. Les formulations d’autorisation présentes dans les autres documents du dépôt sont du contexte ; elles ne remplacent pas le périmètre de cette demande.

## 2. Références du projet et état observé

Commence par consulter :

- [Brief du socle](C:/VibeCoding/UnityMobileGameSample/Doc/CLAUDE_UNITY_MOBILE_FOUNDATION.md), notamment les sections 2, 4, 6, 8, 9 et 12.
- [Spec économie à réviser](C:/VibeCoding/UnityMobileGameSample/Doc/specs/2026-10-25-economie-cloud-save-design.md).
- [Décision 0001 — modèle économique V1](C:/VibeCoding/UnityMobileGameSample/Doc/decisions/0001-modele-economique-v1.md).
- [Décision 0005 — version Unity](C:/VibeCoding/UnityMobileGameSample/Doc/decisions/0005-unity-6000-6.md).
- [Matrice des dépendances](C:/VibeCoding/UnityMobileGameSample/Doc/dependencies.md).
- [Manifest UPM](C:/VibeCoding/UnityMobileGameSample/UnityProject/Packages/manifest.json) et les versions réellement résolues dans le lockfile.
- [Projet serveur](C:/VibeCoding/UnityMobileGameSample/UnityProject/GemHunterUGSCloud/Project/GemHunterUGSCloud.csproj).

Recense aussi les autres plans d’implémentation, specs, décisions, listes de tâches, questions ouvertes, consignes et README du dépôt. Recherche leurs dépendances et leurs contradictions ; la revue ne doit pas se limiter à la spec économie ni aux documents listés ici.

Relevé local du 25 octobre 2026 :

| Élément | Version observée |
| --- | --- |
| Éditeur Unity | 6000.6.7f1 |
| Unity IAP | 5.4.4 |
| Cloud Code UPM | 2.10.4 |
| Cloud Save UPM | 3.4.0 |
| Authentication | 3.8.0 |
| Remote Config | 4.2.5 |
| Services Core | 1.18.0 |
| Module serveur | net6.0, Com.Unity.Services.CloudCode.Apis 0.0.16 |
| Projet de tests serveur | net10.0 |

Le SDK .NET installé, la cible des tests, le runtime du module distant et les capacités de l’éditeur sont des éléments distincts. Leur compatibilité doit être démontrée ensemble.

## 3. Corriger le point de départ de l’analyse

La première revue a trop raisonné à partir du remplacement d’Economy dans le sample. Elle avait insuffisamment pris en compte les batches Cloud Save, Stateful Cloud Code et Remote Catalog IAP. Il faut corriger cette lacune sans présenter une fonctionnalité récente comme une solution déjà qualifiée.

### 3.1 Statut d’Economy

Depuis le 8 septembre 2026, Economy n’accepte plus de nouveaux projets. Les projets déjà configurés restent maintenus. Unity recommande Cloud Code, Cloud Save et Remote Config pour les nouveaux projets.

C’est un problème concret pour la réutilisation du template avec un nouveau projet UGS. Cela ne signifie pas que tous les projets Economy existants sont arrêtés. Vérifie séparément les affirmations de la spec concernant le projet UGS de ce dépôt.

Source : [Unity Economy](https://docs.unity.com/en-us/economy).

### 3.2 Cloud Save possède des écritures groupées atomiques

L’opération **Set Protected Player Item Batch** permet d’écrire jusqu’à 20 clés protégées du même joueur de manière atomique, avec un writeLock par clé. Un échec fait échouer l’ensemble du batch.

L’alternative « plusieurs clés » ne peut donc pas être rejetée au seul motif qu’elle imposerait plusieurs écritures indépendantes. Un document unique reste possible, mais doit être justifié par la simplicité, la taille, la contention et les frontières métier.

Ne pas extrapoler cette garantie à plusieurs joueurs, plusieurs services ou plusieurs classes d’accès. Atomicité du batch, contrôle de concurrence et déduplication d’une requête sont trois garanties différentes.

Sources : [API Cloud Save](https://docs.unity.com/en-us/oas-cloud-save/1.0.0), [write locks](https://docs.unity.com/en-us/cloud-save/concepts/write-locks).

### 3.3 Stateful Cloud Code mérite une comparaison explicite

La documentation actuelle décrit un état persistant de scope Player, adapté notamment aux soldes, inventaires et progressions. Les appels vers un même couple type de scope / identifiant s’exécutent successivement, même entre modules. Le TTL Player vaut deux ans par défaut.

Cela pourrait simplifier le stockage et l’ordonnancement des opérations. La revue n’a toutefois pas établi précisément le moment du commit durable, le rollback après exception, le comportement après interruption, ni les garanties couvrant des effets dans un autre service. La déduplication et les migrations de schéma restent à concevoir.

Source : [State scopes](https://docs.unity.com/en-us/cloud-code/3.0.0/modules/stateful-cloud-code/state-scopes).

Cloud Code 3.0.0, publié le 23 octobre 2026, exige Unity 6000.7 ou plus et cible .NET 10 pour les modules et les outils locaux. Le template référence Core 0.0.9 ; la documentation des scopes exige APIs 0.0.27 ou plus. Unity 6.7 est actuellement proposé en bêta.

Cette option n’est donc pas démontrée compatible avec la base 6000.6.7f1 / Cloud Code 2.10.4. Ne propose pas une montée de version automatique.

Sources : [Changelog Cloud Code 3.0](https://docs.unity3d.com/Packages/com.unity.services.cloudcode@3.0/changelog/CHANGELOG.html), [Unity Beta](https://unity.com/releases/editor/beta).

### 3.4 Remote Catalog IAP doit être évalué

Unity recommande désormais Remote Catalog pour les produits payants ; IAP 5.4.4 est déjà installé. Évalue ses capacités avant de reconstruire cette gestion dans Remote Config.

Distingue le catalogue des produits payants, leurs identifiants par store, les réglages économiques du jeu et les règles serveur d’attribution. Désigne une source d’autorité pour chacun et préviens leurs divergences. Un payout déclaré côté client ne prouve pas une attribution serveur sécurisée.

Sources : [Catalogues IAP](https://docs.unity.com/en-us/iap/catalogs), [schéma Remote Catalog](https://docs.unity.com/en-us/iap/catalog-schema).

### 3.5 Les achats natifs et D2C ne fournissent pas les mêmes services

Pour les achats natifs Apple/Google, la validation distante et l’attribution des biens protégés restent à implémenter. Les services de commandes et de livraison proposés pour D2C/Webshop ne doivent pas être supposés disponibles à l’identique sur ce parcours.

Cloud Code C# et Secret Manager permettent d’héberger la validation dans UGS. Un second backend n’est pas nécessaire par principe. En revanche, la réception directe des notifications Apple/Google et des callbacks publicitaires doit être établie séparément : un webhook sortant UGS ne prouve pas l’existence d’un connecteur entrant compatible.

Sources : [Comparaison des parcours IAP](https://docs.unity.com/en-us/iap/introduction), [validation des reçus](https://docs.unity.com/en-us/iap/receipt-validation), [Secret Manager avec Cloud Code](https://docs.unity.com/en-us/services/secret-manager/tutorials/integrations/cloud-code), [structure des Triggers](https://docs.unity.com/en-us/triggers/tutorials/define-triggers/trigger-structure).

## 4. Challenger les plans, les questions et le périmètre existants

Le brief et la décision 0001 définissent une V1 centrée sur les achats permanents validés côté serveur et les récompenses publicitaires locales de faible valeur. Le portefeuille protégé est optionnel.

La spec plus récente indique vouloir refaire fonctionner tout Gem Hunter sans Economy. Vérifie si cet objectif sert toujours la création de la boilerplate, s’il doit être réduit ou s’il doit être abandonné en partie. Il ne faut pas imposer la conservation intégrale du sample comme une fin en soi. Présente les arbitrages importants à valider, en distinguant :

| Niveau | Question à résoudre |
| --- | --- |
| Socle commun obligatoire | Quelles capacités chaque futur jeu doit-il embarquer et initialiser ? |
| Modules optionnels | Quelles capacités peuvent être absentes sans casser le socle ? |
| Hôte Gem Hunter | Quelles fonctions doivent être conservées pour la démonstration et rester propres au jeu ? |

Pour chaque plan, étape, question ouverte, hypothèse ou fonctionnalité, vérifie le besoin concret servi, sa place dans le socle ou dans la démonstration, ses dépendances, sa validité technique et son coût de maintenance. Une question peut être mal posée, déjà résolue ou devenue inutile parce que le besoin dont elle dépend a disparu : reformule-la, clôture-la ou supprime-la au lieu de la reconduire automatiquement.

Classe les éléments en **conserver / corriger ou simplifier / supprimer / décision à valider**, avec une justification courte. Ne transforme pas systématiquement ce qui est inutile en module optionnel ou en tâche « pour plus tard ». Un report n’a de sens que si un besoin identifié le justifie.

**Ce qui ne sert plus le but doit être supprimé du projet actif.** Retire les instructions obsolètes, doublons, tâches et questions sans objet, ainsi que le code, les dépendances, configurations et assets devenus inutiles lorsque leur suppression relève du périmètre validé. Si une suppression constitue un choix important au sens de la section 1, prépare son impact puis demande ma validation avant de l’appliquer. Mets à jour les références, tests et guides concernés ; respecte les GUID et les données existantes. Utilise l’historique Git et, lorsqu’elle est utile, une courte trace de décision remplacée pour conserver la compréhension du changement, sans laisser d’anciennes consignes actives ni de code mort « au cas où ».

Préserve les acquis : contrats Purchases et Ads, absence de types Gem Hunter dans le package, adaptateurs SDK et gameplay local indépendant des services facultatifs. N’ajoute ni fournisseur ni abstraction universelle sans besoin concret.

## 5. Points précis à corriger ou démontrer dans la spec

1. **Création initiale concurrente.** La spec affirme qu’une création concurrente sera rejetée. Cette garantie n’est pas établie : l’omission du writeLock permet aussi de contourner le contrôle de conflit lors d’une mise à jour. Décrire un mécanisme qui ne peut pas effacer un état déjà acquis. Un verrou en mémoire d’un worker n’est pas une preuve de sérialisation distribuée.

2. **Atomicité d’une opération métier complète.** Un débit atomique ne suffit pas si la progression ou le marqueur de récompense est écrit séparément. Examiner les parcours quotidiens, coffres, zones et cadeaux. Identifier toutes les données touchées, puis les regrouper dans une unité atomique lorsque l’API le permet. Traiter séparément les opérations concernant plusieurs joueurs.

3. **Idempotence durable.** Conserver seulement les 200 dernières transactions ne garantit pas l’absence de nouvelle attribution d’une transaction évincée. La confirmation du store n’est pas une transaction atomique avec l’écriture du backend. Proposer une politique de déduplication, de rétention et de reprise adaptée aux droits permanents et aux consommables.

4. **Droits permanents.** Évaluer une attribution déterministe par droit et bénéficiaire : répéter « ce joueur possède ce droit » peut simplifier la reprise. Cela ne dispense pas de vérifier le paiement, son application, son produit et son bénéficiaire ; ni d’étudier révocation et remboursement si ces parcours sont activés.

5. **Contrat client/serveur.** Ne conserver les signatures et DTO à tout prix que si les garanties restent réalisables. Un identifiant d’opération ou une précondition métier peut être nécessaire. Examiner aussi les quantités nulles attendues par les vues, les unités des timestamps et la régénération des bindings.

6. **Correspondance des produits.** Le serveur doit pouvoir associer le produit réellement vérifié auprès du store à la récompense autorisée. Un identifiant de catalogue librement fourni par le client ne suffit pas. Documenter la source de cette correspondance, éventuellement articulée avec Remote Catalog.

7. **Transition des achats réels.** Tant que leur validation et leur attribution sont indisponibles, ne pas proposer de nouveaux achats réels. Ne pas présenter « RetryLater indéfiniment » comme une garantie d’absence de perte. Prévoir séparément le traitement des transactions existantes en attente et les délais imposés par chaque store.

8. **Données sensibles.** Supprimer l’affirmation générale « identifiant de transaction autorisé dans les logs ». Avec IAP 5, l’identifiant Google peut être le purchase token. Utiliser une corrélation non sensible ; aucun reçu, token, signature ou secret dans les logs.

9. **Versions et outillage.** Corriger « Cloud Code n’annonce pas .NET 10 » en distinguant la nouvelle chaîne 3.0 du chemin compatible avec le projet actuel. Séparer Unity CLI, UGS CLI, fenêtre Deployment et serveur Cloud Code local. Vérifier leurs versions et fonctions réelles au lieu d’en désigner un comme méthode unique.

10. **Environnements et configuration.** Le faux store doit être refusé par le backend hors environnement de développement, indépendamment des paramètres client. Préciser la sélection d’environnement, le comportement en cas de configuration absente et l’autorité du catalogue serveur. Si les configurations sont ciblées par joueur, segment ou environnement, le cache doit respecter cette portée.

11. **Accès et exploitation.** Définir les droits client/serveur, quotas, coûts d’appels, migrations, rétention, suppression de compte et diagnostics nécessaires au périmètre retenu. Une simulation locale ne prouve pas les politiques du service distant.

Références complémentaires pour ces vérifications : [API GoogleOrderInfo d’IAP 5.4](https://docs.unity3d.com/Packages/com.unity.purchasing@5.4/api/UnityEngine.Purchasing.IGoogleOrderInfo.html), [sécurisation Google Play Billing](https://developer.android.com/google/play/billing/security), [cycle des achats Google Play](https://developer.android.com/google/play/billing/integrate).

## 6. Comparaison attendue avant décision

Compare ces options sans présélectionner un gagnant :

| Option | Intérêt à évaluer | Conditions à démontrer |
| --- | --- | --- |
| Cloud Save protégé, document unique | Simplicité d’une mutation regroupée | Initialisation sûre, concurrence, taille, rétention, reprise |
| Cloud Save protégé, batch de clés | Séparation des données avec atomicité native | Frontière du batch, writeLocks, initialisation et déduplication |
| Stateful Cloud Code | Persistance et sérialisation des appels gérées par Unity | Migration d’éditeur, maturité, commit durable, interruption, TTL, schéma, limites |

Pour chaque option, indique le code métier nécessaire, les mécanismes réellement fournis par Unity, les contraintes de versions, les coûts opérationnels et les inconnues bloquantes.

La recommandation doit être adaptée au besoin établi et à la base actuelle du projet. Si le portefeuille n’est plus retenu dans le périmètre, ne transforme pas cette comparaison en chantier obligatoire. Une option future ne mérite un suivi que si un besoin identifié le justifie. Présente le choix structurant retenu à ma validation avant de modifier l’architecture correspondante.

## 7. Plan de preuve à mettre en œuvre

Définis et exécute les tests ciblés applicables au périmètre validé, avec le résultat attendu et le niveau de preuve nécessaire. Adapte cette liste aux fonctionnalités effectivement conservées ; retire les scénarios devenus sans objet en expliquant pourquoi. Identifie précisément les vérifications qui restent impossibles faute d’accès, d’environnement ou d’appareil :

- Deux premières requêtes simultanées, avec une mutation entre les deux créations : aucun état acquis n’est écrasé.
- Deux dépenses concurrentes dépassant ensemble le solde : aucun double débit accepté.
- Réponse perdue après écriture effective, puis répétition : aucune seconde attribution.
- Rejeu d’un achat après plus de 200 transactions : aucun nouveau crédit.
- Récompense quotidienne, coffre ou amélioration : état métier et récompense restent cohérents après une panne.
- Deux droits permanents distincts attribués simultanément : aucun droit perdu.
- Reçu invalide, mauvaise application, mauvais produit, mauvais environnement ou autre bénéficiaire : refus explicite.
- Restauration, changement d’appareil et changement de compte : politique d’identité documentée et droits corrects.
- Tentative d’écriture protégée avec un jeton client : refus par le service distant.
- Configuration absente, panne réseau et échec de confirmation store : reprise définie.
- Si Stateful est retenu : exception, timeout, interruption, migration de schéma et expiration/rétention.

Sépare tests de règles pures, tests d’adaptateurs, intégration UGS en développement et validation sur appareil/store sandbox. N’assimile pas un faux store, un backend simulé ou un serveur local à une validation Apple/Google réelle.

## 8. Livrables attendus dans le dépôt et le compte rendu

1. **Diagnostic d’alignement et revue des plans et questions existants** : conserver, corriger ou simplifier, supprimer, soumettre à validation ; justification et références aux fichiers et sources.
2. **Matrice de responsabilités** : besoin → capacité native Unity → code métier restant → version compatible → preuve disponible ou manquante.
3. **Arbitrages importants préparés pour validation**, dont la comparaison des options de persistance si le besoin est conservé : recommandation, alternatives, impacts, décisions attendues et travaux dépendants.
4. **Specs et plans révisés dans le dépôt**, cohérents avec le but du socle, les décisions validées et l’état réel de l’implémentation ; distinguer clairement les propositions encore à valider.
5. **Documentation et projet nettoyés** : mettre à jour le brief, les décisions, dépendances et guides selon les impacts réels ; supprimer les éléments devenus inutiles dans le périmètre autorisé et corriger leurs références. Rendre compte des suppressions et de leur raison.
6. **Corrections implémentées et qualification exécutée dans le périmètre validé** : progresser par étapes vérifiables, documenter les résultats, corriger les échecs et distinguer les travaux terminés des points en attente de validation, bloqués ou non vérifiés.

Distingue systématiquement fait documenté, observation du dépôt, inférence et point non vérifié. Les skills et samples sont utiles, mais peuvent être en retard sur les versions actuelles. En cas de contradiction entre sources Unity, cite les deux et précise la version visée.

Termine avec les rubriques : **implémenté / compilé / testé automatiquement / vérifié sur appareil / vérifié en sandbox / restant à faire**. Indique les modifications et vérifications réellement réalisées, leurs résultats et leurs limites ; ne présente pas une simulation comme une validation réelle.

## 9. Niveau de preuve de cette note

Cette note compile une lecture du dépôt et une recherche documentaire effectuées le 25 octobre 2026. Les capacités et prérequis cités proviennent des sources officielles liées ; leur disponibilité effective dans le projet et les garanties encore incertaines restent à qualifier.

La revue initiale à l’origine de cette note n’a modifié aucun code du projet et n’a exécuté aucune compilation, aucun test automatique, aucun essai sur appareil, aucun paiement sandbox ni déploiement. Ce constat décrit le niveau de preuve initial ; il ne limite pas ton mandat de développeur. La présente version autorise la mise à jour de la documentation et du projet ainsi que leur qualification, dans le cadre défini en section 1.

