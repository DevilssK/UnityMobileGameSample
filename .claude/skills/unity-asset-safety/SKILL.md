---
name: unity-asset-safety
description: À utiliser avant de déplacer, renommer ou supprimer un fichier sous Assets/ ou un package UPM (script, prefab, scène, ScriptableObject, dossier), de renommer une classe MonoBehaviour/ScriptableObject, un namespace ou un champ sérialisé, ou d'extraire du code vers SharedPackages/. Évite de casser les GUID, prefabs et références de scènes.
---

# Sécurité des assets Unity

Unity référence les assets par le GUID du `.meta`, et les scripts par GUID + nom de classe. Une erreur ici casse silencieusement prefabs et scènes ("Missing Script").

## Déplacer / renommer un fichier ou dossier

1. Toujours déplacer le fichier **et** son `.meta` ensemble (`git mv` des deux si sous Git). Un dossier a aussi son `.meta`.
2. Ne jamais créer ni éditer un `.meta` à la main ; ne jamais en supprimer un d'un asset conservé.
3. Si l'éditeur Unity est ouvert sur le projet, préférer demander au propriétaire de faire le déplacement dans l'éditeur.
4. Après coup : `git status` doit montrer des renommages appariés (`.cs` + `.cs.meta`), pas un ajout + une suppression de `.meta`.

## Renommer une classe de script

- Le nom de fichier doit rester égal au nom de la classe MonoBehaviour/ScriptableObject.
- Renommer fichier + classe ensemble en gardant le même `.meta` (même GUID) : les références survivent.
- Changement de namespace ou d'assembly : ajouter `[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Old.Ns", sourceAssembly: "Old.Asm", sourceClassName: "OldName")]` si des données `[SerializeReference]` ou des assets en dépendent.

## Renommer un champ sérialisé

```csharp
[SerializeField, FormerlySerializedAs("m_OldName")] private int _newName;
```

Garder l'attribut tant que des prefabs/scènes n'ont pas été ré-enregistrés. Ne pas renommer pour une raison purement esthétique dans une zone non modifiée.

## Supprimer

Avant toute suppression, chercher le GUID dans tout le projet :

```bash
guid=$(grep -m1 '^guid:' "Chemin/Asset.ext.meta" | cut -d' ' -f2)
grep -rl "$guid" UnityProject/Assets UnityProject/ProjectSettings SharedPackages 2>/dev/null
```

Aucune référence restante → suppression du fichier et de son `.meta`. Sinon, lister les références au propriétaire. Pour une fonction retirée du template (social, Facebook, zones…), noter le commit de référence avant suppression.

## Extraction vers SharedPackages/

- Déplacer avec le `.meta` : le GUID reste valide si le package est référencé en local (`file:`) dans `Packages/manifest.json`.
- Créer/ajuster les `.asmdef` (Runtime, Editor, Tests) ; vérifier que l'asmdef du package ne référence aucune assembly Gem Hunter.
- Vérifier ensuite : `grep -rn "GemHunter\|AreaManager" SharedPackages/` doit être vide.

## Ne jamais modifier

`Library/`, `Temp/`, `obj/`, code généré, code de SDK tiers (`Packages/` cachés, plugins natifs fournis).
