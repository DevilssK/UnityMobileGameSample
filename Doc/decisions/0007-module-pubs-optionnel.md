# 0007 — Module Pubs récompensées : cœur commun, adaptateur LevelPlay optionnel

Date : 2026-10-25 · Statut : acceptée

## Problème
Le bouton pub de Gem Hunter appelait `HandleGrantVideoAdReward`, un endpoint qui créditait des pièces serveur sur la foi d'un jeton fabriqué par le client. Cet endpoint a été retiré du backend (décision 0001), ce qui cassait le bouton. Le gameplay appelait aussi LevelPlay directement, et la suite de test LevelPlay était activée dans tout build de développement, sans possibilité de la couper par configuration. Enfin, iOS utilisait l'ad unit Android.

## Décision
- Module `Ads` dans le package, découpé comme `Purchases` : un cœur sans moteur (`IRewardedAdsService`, `RewardedAdCoordinator`, `IRewardedAdGateway`) testé par `dotnet test`, et un adaptateur `Ads.LevelPlay`.
- L'adaptateur LevelPlay est **optionnel** : il n'est compilé que si `com.unity.services.levelplay` ≥ 9.0.0 est présent (`versionDefines` → `DEVILCORP_LEVELPLAY`). Le package ne déclare pas de dépendance LevelPlay, pour qu'un jeu sans pubs ne l'embarque pas.
- Récompense : uniquement sur l'événement de récompense du SDK, au plus une fois par pub. Si la pub se ferme sans récompense, le coordinateur attend 1 s de grâce, parce que certains réseaux envoient la récompense après la fermeture.
- Récompense = effet local de faible valeur (décision 0001). Dans Gem Hunter, le sample ne fait que signaler la récompense : aucune logique métier (demande utilisateur du 2026-10-25, Gem Hunter n'est qu'un exemple d'intégration).
- Suite de test LevelPlay : activable par `LevelPlayAdsSettings.EnableTestSuite`, mais seulement si `Debug.isDebugBuild` est vrai.

## Alternatives écartées
- Dépendance LevelPlay obligatoire dans `package.json` : elle impose le SDK pub (et ses dépendances natives) à tout jeu consommateur.
- Conclure `NotRewarded` dès la fermeture : risque de perdre une récompense légitime sur les réseaux qui l'envoient après la fermeture.
- Garder le crédit de pièces serveur avec un jeton client : ce jeton n'est pas une preuve de visionnage (brief §5).

## Conséquences
- Une récompense protégée (monnaie serveur) demandera une confirmation S2S LevelPlay et une nouvelle décision.
- Le délai de grâce retarde de 1 s le résultat d'une pub fermée sans récompense.
- `HandleGrantVideoAdReward` existe encore dans les bindings Cloud Code générés côté client, mais n'est plus appelé. Il disparaîtra à la prochaine régénération des bindings après déploiement du backend.
