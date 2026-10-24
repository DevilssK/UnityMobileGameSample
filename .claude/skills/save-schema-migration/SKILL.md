---
name: save-schema-migration
description: À utiliser pour ajouter, renommer ou supprimer une donnée de la sauvegarde locale, changer le format de sauvegarde, ou toucher au service de persistance (LocalStorageSystem, ISaveService, SaveMigrator).
---

# Évolution du schéma de sauvegarde

Référence : section 11 du brief.

## Règles

- La sauvegarde est une enveloppe `{ schemaVersion, data }`. Toute modification incompatible du DTO incrémente `schemaVersion`.
- Une migration par saut de version (`V3 → V4`), pure (données en entrée, données en sortie), chaînée jusqu'à la version courante.
- Ne jamais renommer une clé existante sans migration. Ajouter un champ avec une valeur par défaut explicite ne demande une migration que si le défaut C# est faux.
- DTO simples et types autorisés uniquement : pas de désérialisation polymorphique (`TypeNameHandling`, `[SerializeReference]` sur données joueur).
- Écriture atomique : fichier temporaire → remplacement → conservation d'une copie de secours. Lecture : principal invalide → secours → défaut contrôlé, sans crash.
- Fichier d'une version **future** (rollback de l'app) : ne pas l'écraser ; charger le secours ou refuser proprement.
- La sauvegarde locale n'est jamais une autorité pour un solde protégé.

## Étapes

1. Modifier le DTO, incrémenter la version courante.
2. Écrire la migration `vN → vN+1`.
3. Ajouter un fichier fixture JSON réel de la version N dans les tests (ne pas le générer avec le nouveau code).
4. Tests minimum :
   - `Load_WhenFileIsVersionN_MigratesToCurrentAndKeepsProgress`
   - `Load_WhenMainFileIsCorrupted_FallsBackToBackup`
   - `Load_WhenFileIsTruncated_DoesNotThrow`
   - `Load_WhenFileIsFromFutureVersion_DoesNotOverwrite`
5. Lancer via `run-checks`.
