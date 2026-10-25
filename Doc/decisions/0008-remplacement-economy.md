# 0008 — Remplacement d'Unity Economy : validation serveur des achats et portefeuille protégé optionnel

Date : 2026-10-25 · Statut : **acceptée** (A1-B, persistance Cloud Save avec délai de grâce, spec validée le 2026-10-25) ; implémentée sur `feature/protected-wallet`, vérifiée en `development` (`Doc/backend-achats.md`). Validation des reçus Apple/Google : spec suivante.

## Problème
Unity Economy n'accepte plus de nouveaux projets depuis le 2026-09-08 ; le projet UGS du template répond `403` sur Economy. Or Economy redeem était le propriétaire unique de l'attribution des achats (décision 0001), et toute la méta de Gem Hunter reposait sur Economy.

## Décision (acceptée)
- Les futurs jeux, y compris solo, peuvent vendre des biens qui doivent être protégés. Le socle fournit donc un **module optionnel de portefeuille protégé géré par le serveur** : soldes, objets et droits, crédits **et** dépenses décidés par Cloud Code. Le client ne peut qu'en lire l'état. C'est l'architecture recommandée par Unity pour les nouveaux projets qui ont besoin d'une économie : Cloud Code, Cloud Save et Remote Config, plus Unity IAP pour les achats réels.
- Le serveur valide les achats et devient le seul propriétaire de l'attribution :
  - Apple : JWS StoreKit 2 ;
  - Google : API Google Play Developer, avec la clé du compte de service dans Secret Manager.
- Une transaction valide donne une seule attribution, avec une déduplication durable et sans éviction.
- Le module est optionnel : un jeu sans biens protégés ne l'initialise pas.
- Cette décision remplace dans 0001 « portefeuille protégé plus tard » et « propriétaire unique = Economy redeem ».

## À valider
- **Stockage du portefeuille.** Cloud Save seul n'offre aucune création conditionnelle, si bien qu'une première écriture peut écraser un état déjà acquis. Stateful Cloud Code sérialise les appels par joueur, mais exige Unity 6.7, aujourd'hui en bêta. Le choix intervient après le spike défini dans le diagnostic (A1-B, persistance).
- **Fonctions de Gem Hunter conservées** pour démontrer le portefeuille. Celles que le brief exclut (§2 : loot box, système social) restent retirées.
- A2 (registre), A3 (catalogue), A4 (transition), A5 (versions), A6 (environnements), A7 (identité).

## Alternatives écartées
- Pas de portefeuille serveur (A1-R) : insuffisant pour des biens vendus qui doivent rester protégés.
- Reconstruire toute l'économie de Gem Hunter côté serveur (spec du 2026-10-25) : elle couvre des fonctions exclues par le brief et contient des garanties non établies.

## Conséquences
- La spec économie du 2026-10-25 est supprimée et remplacée par `Doc/specs/2026-10-25-portefeuille-protege-design.md` (à relire), puis par une spec « validation des achats » (Apple/Google).
- Persistance retenue par le propriétaire le 2026-10-25 : Cloud Save avec délai de grâce à la création. Fonctions de démo conservées : boutique de pièces, zones, fin de niveau et cœurs, récompenses quotidiennes.
- Les tests de concurrence de la section 7 de la demande de revue redeviennent obligatoires : création initiale, dépenses concurrentes, réponse perdue, rejeu au-delà de 200 transactions.
