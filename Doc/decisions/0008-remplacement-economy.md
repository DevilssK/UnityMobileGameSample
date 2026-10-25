# 0008 — Remplacement d'Unity Economy : registre d'achats sur Cloud Save, pas de portefeuille serveur

Date : 2026-10-25 · Statut : **proposée — à valider** (arbitrages A1, A2, A3 et A7 de `Doc/revue/2026-10-25-realignement-diagnostic.md`)

## Problème
Unity Economy n'accepte plus de nouveaux projets depuis le 2026-09-08. Le projet UGS du template répond `403` sur Economy. Or le propriétaire unique de l'attribution des achats était Economy redeem (décision 0001), et toute la méta de Gem Hunter (pièces, objets, coffres, zones) reposait sur Economy.

## Décision proposée
- Le socle V1 ne comporte **pas de portefeuille protégé** (confirmation de la décision 0001).
- Le serveur (Cloud Code) valide les achats et devient le seul propriétaire de l'attribution :
  - Apple : JWS StoreKit 2 ;
  - Google : API Google Play Developer, avec la clé du compte de service dans Secret Manager.
- Chaque livraison est enregistrée dans Cloud Save (classe protected) sous une clé par transaction. La valeur est déterministe, et le droit permanent est écrit dans le même batch.
- Le produit attribué est déduit de l'id produit du reçu vérifié, à l'aide d'un catalogue unique versionné dans le dépôt, partagé par le client et le module.
- Consommables : livrés une fois par transaction vers l'état local du jeu, sans valeur protégée.
- Gem Hunter devient un hôte de gameplay local. Ses fonctions méta serveur sortent du template, et le package `com.unity.services.economy` est retiré.

## Alternatives écartées
- Document Cloud Save unique par joueur, mis à jour par writeLock : la création initiale n'est pas sûre (aucune écriture conditionnelle de création), et l'idempotence est bornée par la taille du document.
- Stateful Cloud Code : exige Unity 6.7 (bêta), Cloud Code 3.0 et .NET 10 ; commit et rollback non documentés.
- Reconstruire toute l'économie de Gem Hunter côté serveur : fonctions exclues par le brief (§2).
- Remote Catalog comme source des gains : son schéma ne porte pas de gain, et sa lecture par un serveur n'est pas documentée.

## Conséquences
- Remplace dans 0001 la phrase « propriétaire unique de l'attribution = Economy redeem ».
- La spec `Doc/specs/2026-10-25-economie-cloud-save-design.md` est supprimée, au profit d'une spec « validation et registre des achats ».
- Un futur jeu qui vendrait une monnaie protégée devra prendre une nouvelle décision, avec la preuve d'une création initiale sûre ou une migration vers Stateful Cloud Code.
