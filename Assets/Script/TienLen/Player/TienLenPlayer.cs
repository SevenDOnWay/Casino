using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using Fusion;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.TienLen.Player {
    public class TienLenPlayer {

        public int Id { get; }
        public PlayerRef PlayerRef { get; }
        public string PlayerName { get; }
        public TienLenGameController controller;
        public PlayerHand Hand { get; }
        public CardHolder CardHolder { get; private set; }
        public bool IsLocalPlayer { get; }
        public bool IsHuman { get; }

        public bool HasWon => Hand.Count == 0;

        public TienLenPlayer(
            int id,
            PlayerRef playerRef,
            string playerName,
            bool isLocalPlayer,
            TienLenGameController gameController,            
            bool isHuman = true ) {
            Id = id;
            PlayerRef = playerRef;
            PlayerName = playerName;
            IsLocalPlayer = isLocalPlayer;
            controller = gameController;
            IsHuman = isHuman;
            
            Hand = new PlayerHand();
        }

        public void SetCardHolder( CardHolder cardHolder ) {
            CardHolder = cardHolder;
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCRequestPlayCard( NetworkCard[] cards, RpcInfo info = default ) {
            controller.HandlePlayRequest(info.Source,cards);
        }



        public bool HasCards(List<Card> cards ) {
            if ( Hand == null ) return false;

            var playercards = Hand.Cards;
            
            foreach(var card in cards) {
                if(!playercards.Contains(card)) return false;
            }

            return true;
        }

        public bool TryRemoveCards(List<Card> cards ) {
            if ( cards == null || cards.Count == 0 ) return false;
            if ( Hand == null ) return false;

            // Verify the player actually holds all required cards first
            if ( !HasCards(cards) ) return false;

            // 1. Remove from the logical hand model
            Hand.Remove(cards);

            return true;
        }

    }
}