using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.Player;
using System.Collections;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Assets.Script.TienLen.LifeTimeScope {
    public class TienLenLifeTimeScope : LifetimeScope{


        protected override void Configure( IContainerBuilder builder ) {

            //rule
            builder.Register<CardCombination>(Lifetime.Singleton);
            builder.Register<CardCombinationType>(Lifetime.Singleton);
            builder.Register<CardComparer>(Lifetime.Singleton);


        }

    }
}