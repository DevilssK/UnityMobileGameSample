# UnityMobileGameSample — socle Unity mobile réutilisable

Contrat de travail complet : `Doc/CLAUDE_UNITY_MOBILE_FOUNDATION.md`. Le lire (au moins les sections concernées) avant toute modification non triviale.

## Organisation

| Chemin | Rôle |
| --- | --- |
| `UnityProject/` | Projet Unity hôte, dérivé de `GemHunterMatch3/` (dépôt `Unity-Technologies/com.unity.services.samples.use-cases`, commit de référence `575661c6`) |
| `UnityProject/GemHunterUGSCloud/` | Backend Cloud Code C#, conservé dans le projet Unity (décision 0004) |
| `SharedPackages/com.devilcorp.mobilefoundation/` | Package UPM commun, namespace `DevilCorp.MobileFoundation` : modules Purchases (décision 0006) et Ads (décision 0007). Guide d'intégration : son `README.md` |
| `SharedPackages/Tests/` | Tests `dotnet test` des cœurs du package (hors Unity) |
| `Doc/` | Brief, décisions (`Doc/decisions/`), guides |

Éditeurs installés : Unity `6000.3.26f1`, `6000.6.5f1`, `6000.6.7f1` (`C:/Program Files/Unity/Hub/Editor/`). .NET SDK 10. Version cible : Unity `6000.6.7f1` (décision 0005, matrice dans `Doc/dependencies.md`).

## Règles non négociables

- Code et identifiants en anglais ; doc en français. Conventions de nommage : section 7 du brief (`_camelCase` pour les champs privés, suffixe `Async`, analytics en `snake_case`).
- Ne jamais casser un GUID Unity : déplacer/renommer un asset avec son `.meta`. Skill `unity-asset-safety`.
- Toute modification d'achat, de récompense pub ou d'économie protégée suit le skill `monetization-change`. Invariant : une transaction valide = une seule attribution logique.
- Le package commun ne référence jamais un type ou une scène Gem Hunter.
- Le gameplay n'appelle jamais directement Unity IAP, LevelPlay ou les clients UGS : il passe par les contrats du socle.
- Pas de nouveau fournisseur (Firebase, autre médiation, autre backend) sans besoin démontré. Périmètre exclu : section 2 du brief.
- Vérifier l'API réelle du SDK installé (context7 ou doc officielle) avant de l'utiliser. Ne pas inventer d'API.
- Jamais de secrets, reçus complets, tokens ou signatures dans le code client ou les logs.
- Ne pas publier, déployer en production ni lancer de transaction réelle sans instruction explicite.
- Pas de reformatage massif du sample. Commits lisibles : correction, extraction et montée de version séparées.

## Rapport de fin de tâche

Toujours séparer : **implémenté / compilé / testé automatiquement / vérifié sur appareil / vérifié en sandbox / restant à faire**. Ne jamais présenter une simulation comme une validation réelle ; si Unity, un SDK ou un compte manque, le dire précisément.

## Skills du projet

- `unity-asset-safety` — déplacer, renommer, supprimer des assets ou champs sérialisés
- `monetization-change` — achats, pubs récompensées, économie, backend de validation
- `run-checks` — compiler et tester Unity (batchmode) et backend (`dotnet`)
- `save-schema-migration` — faire évoluer le format de sauvegarde
- `analytics-event` — ajouter ou modifier un événement analytics
- `sdk-upgrade` — changer de version Unity ou de SDK
- `decision-record` — consigner une décision structurante

## Plugins activés (`.claude/settings.json` → `enabledPlugins`)

**Unity** (officiel, `unity@anthropic-plugin-directory`). Skills utiles ici :

| Besoin | Skill plugin | À combiner avec |
| --- | --- | --- |
| Unity IAP : catalogue, achat, restauration, validation | `unity:implement-in-app-purchases` | `monetization-change` |
| Pubs LevelPlay : SDK, rewarded, ATT/consentement | `unity:levelplay-unity-integration` | `monetization-change` |
| UGS : Auth, Cloud Code, Economy, Cloud Save, Remote Config, Analytics | `unity:build-live-game` | `monetization-change`, `analytics-event` |
| Éditeurs, ouvrir/build/test en CLI, Editor live | `unity:unity-cli` | `run-checks` |
| Ajouter/mettre à jour un package UPM | `unity:unity-package-management` | `sdk-upgrade` |
| UI uGUI / UI Toolkit (safe area, écrans) | `unity:ui-ugui`, `unity:ui-uitk` | — |
| Localisation, polices TMP | `unity:localization`, `unity:optimize-text-mesh-pro` | — |
| Sprites et atlas (jeux 2D) | `unity:manage-sprite-atlas`, `unity:sprite-editor`, `unity:2d-pixel-perfect` | `unity-asset-safety` |
| Audio | `unity:optimize-audio`, `unity:audio-setup-mixers` | — |
| Rendu URP | `unity:migrate-birp-to-urp`, `unity:urp-postprocessing` | `sdk-upgrade` |
| Trouver un asset ou un objet de scène | `unity:generate-editor-search-query` | — |

Non pertinents pour ce socle (hors périmètre, section 2 du brief) : `setup-multiplayer-services`, `setup-vivox-voice-chat`, `optimize-web`, `initialize-ai-navigation`. `new-unity-project` ne sert pas : on part du sample Gem Hunter.

**Priorité :** les skills du plugin donnent la mécanique des SDK ; les règles de ce fichier et du brief priment en cas de conflit (ex. un exemple d'achat qui crédite côté client, une récompense pub accordée sans confirmation serveur, un fournisseur supplémentaire).

**Superpowers** (`superpowers@anthropic-plugin-directory`) : méthode de travail. `superpowers:writing-plans` avant une étape du brief, `superpowers:test-driven-development` pour les invariants d'achat et de sauvegarde, `superpowers:systematic-debugging` sur un bug, `superpowers:verification-before-completion` avant d'annoncer un résultat.
