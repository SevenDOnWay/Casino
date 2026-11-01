using Assets.Script.Data.SO;
using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Effects;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Game.Services;
using Assets.Script.TienLen.Game.Visual;
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
            // ScriptableObject Assets / Databases (scene-local copy)
            builder.RegisterComponentInNewPrefab(prefab, Lifetime.Scoped);

            // Rules & Evaluation
            builder.Register<CardComparer>(Lifetime.Singleton);
            builder.Register<CardCombinationEvaluator>(Lifetime.Singleton);
            builder.Register<TienLenRuleValidator>(Lifetime.Singleton);

            // Domain Services
            builder.Register<TienLenMatchOutcomeReporter>(Lifetime.Singleton);
            builder.Register<TienLenActionHandler>(Lifetime.Singleton);
            builder.Register<TienLenTurnHandler>(Lifetime.Singleton);
            builder.Register<TienLenMigrationHandler>(Lifetime.Singleton);

            // Session & Table State
            builder.RegisterComponentInHierarchy<SessionController>();
            builder.RegisterComponentInHierarchy<TienLenGameController>();
            builder.RegisterComponentInHierarchy<TableVisualLayoutManager>();
            builder.RegisterComponentInHierarchy<SeatProvider>();

            builder.RegisterComponentInHierarchy<SeatManager>()
                   .As<IPlayerRegisterService>()
                   .As<ISeatQueryService>()
                   .AsSelf();

            builder.RegisterComponentInHierarchy<TurnManager>();

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
