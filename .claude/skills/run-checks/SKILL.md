---
name: run-checks
description: À utiliser pour compiler et tester le projet — tests Unity EditMode/PlayMode en batchmode, compilation des scripts Unity, build et tests du backend .NET — et après toute modification de code avant d'annoncer un résultat.
---

# Compiler et tester

## Trouver l'éditeur Unity

```bash
ver=$(grep m_EditorVersion: UnityProject/ProjectSettings/ProjectVersion.txt | cut -d' ' -f2)
unity="/c/Program Files/Unity/Hub/Editor/$ver/Editor/Unity.exe"
[ -x "$unity" ] || { echo "Unity $ver non installé — versions dispo :"; ls "/c/Program Files/Unity/Hub/Editor/"; }
out="<scratchpad de la session>"   # logs et résultats hors du projet
```

Si la version du projet n'est pas installée, s'arrêter et le signaler : ne pas ouvrir le projet avec une autre version (cela déclenche une migration).

Le batchmode échoue si l'éditeur a déjà le projet ouvert ("another Unity instance is running") : demander au propriétaire de le fermer, ou de lancer les tests depuis le Test Runner.

## Tests Unity

```bash
"$unity" -batchmode -nographics -projectPath UnityProject \
  -runTests -testPlatform EditMode \
  -testResults "$out/editmode.xml" -logFile "$out/editmode.log"
echo "exit=$?"
```

- Pas de `-quit` avec `-runTests`.
- `-testPlatform PlayMode` pour les tests PlayMode.
- Filtrer : `-testFilter "Namespace.ClassName"` ou `-assemblyNames "Studio.MobileFoundation.Tests"`.
- Code retour : 0 = OK, 2 = tests en échec, 1 = erreur (souvent compilation). Lire `editmode.xml` (`result="Failed"`) et chercher `error CS` dans le log.
- Premier lancement : import complet, plusieurs minutes. Utiliser `run_in_background`.

## Compilation seule

```bash
"$unity" -batchmode -nographics -quit -projectPath UnityProject -logFile "$out/compile.log"
grep -E "error CS[0-9]+" "$out/compile.log"
```

## Backend .NET

```bash
dotnet build Backend --nologo -v q
dotnet test Backend --nologo
```

Le backend cible sa propre version .NET (Cloud Code) et ne doit pas référencer `UnityEngine`.

## Rapport

Indiquer exactement ce qui a tourné, avec le nombre de tests passés/échoués et les erreurs. Si une étape n'a pas pu tourner, dire pourquoi. Ne jamais écrire "compile" sans l'avoir compilé.
