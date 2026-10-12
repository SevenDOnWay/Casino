using Assets.Script.UI.MainMenu;
using System.Collections;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Assets.Script.TienLen.LifeTimeScope {
    public class SignInLifeTimeScope : LifetimeScope {

        protected override void Configure( IContainerBuilder builder ) {

            builder.RegisterComponentInHierarchy<SignInUIController>()
                .AsSelf()
                .AsImplementedInterfaces();

        }
    }
}