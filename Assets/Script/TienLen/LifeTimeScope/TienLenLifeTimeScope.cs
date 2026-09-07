using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Assets.Script.TienLen.LifeTimeScope {
    public class TienLenLifeTimeScope : LifetimeScope{

        [SerializeField] private CardHolder[] cardHolders;

        protected override void Configure( IContainerBuilder builder ) {

            //rule
            builder.Register<CardCombination>(Lifetime.Singleton);
            builder.Register<CardCombinationType>(Lifetime.Singleton);
            builder.Register<CardComparer>(Lifetime.Singleton);


            builder.Register<CardSpriteAtlas>(Lifetime.Singleton);


            builder.RegisterComponentInHierarchy<CardSpawner>();
            builder.RegisterComponentInHierarchy<TienLenGameController>();

            builder.RegisterInstance<IReadOnlyList<CardHolder>>(cardHolders);

        }

    }
}