using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Effects;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Assets.Script.TienLen.LifeTimeScope {
    public class TienLenLifeTimeScope : LifetimeScope {
        [SerializeField] private TienLenNetWorkPlayer prefab;

        protected override void Configure( IContainerBuilder builder ) {
            builder.Register<LocalPlayerService>(Lifetime.Singleton)
                   .As<ILocalPlayerService>()
                   .AsSelf();

            builder.RegisterComponentInNewPrefab(prefab, Lifetime.Scoped);

            // Rules & Evaluation
            builder.Register<CardCombinationType>(Lifetime.Singleton);
            builder.Register<CardComparer>(Lifetime.Singleton);
            builder.Register<CardCombinationEvaluator>(Lifetime.Singleton);
            builder.Register<TienLenRuleValidator>(Lifetime.Singleton);

            // Session & Table State
            builder.RegisterComponentInHierarchy<LobbySessionController>();
            builder.RegisterComponentInHierarchy<TienLenGameController>();
            builder.RegisterComponentInHierarchy<TableVisualLayoutManager>();
            builder.RegisterComponentInHierarchy<SeatProvider>();

            builder.RegisterComponentInHierarchy<SeatManager>()
                   .As<IPlayerRegisterService>()
                   .As<ISeatQueryService>()
                   .AsSelf();

            builder.Register<TurnManager>(Lifetime.Singleton);

            // UI & Effects
            builder.RegisterComponentInHierarchy<UiManager>();
            builder.RegisterComponentInHierarchy<StartGameUI>();
            builder.RegisterComponentInHierarchy<CardSpawner>();
            builder.RegisterComponentInHierarchy<ActionPanel>();
            builder.RegisterComponentOnNewGameObject<PlayerWinEffectController>(Lifetime.Singleton, "PlayerWinEffectController")
                   .As<IPlayerWinEffect>()
                   .AsSelf();
        }
    }
}
