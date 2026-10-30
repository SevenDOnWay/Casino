using Assets.Script.NetWorkScript;
using Assets.Script.UI.MainMenu;
using Assets.Script.UI.MainMenu.Panel;
using System.Collections;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Assets.Script.TienLen.LifeTimeScope {
    public class MainMenuLifeTimeScope : LifetimeScope {

        protected override void Configure( IContainerBuilder builder ) {

            builder.RegisterComponentInHierarchy<TienLenNetworkController>();

            builder.Register<LobbySessionBrowserController>(Lifetime.Scoped);

            builder.RegisterComponentInHierarchy<LobbySessionBrowserPanelView>()
                                                .AsSelf()
                                                .AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<PlayerProfileUI>()
                                                .AsSelf()
                                                .AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<hostUiPanel>();
            builder.RegisterComponentInHierarchy<PlayOptionUI>();

            builder.RegisterComponentInHierarchy<PlayerProfilePanelView>()
                                                .AsSelf()
                                                .AsImplementedInterfaces();

        }
    }
}
