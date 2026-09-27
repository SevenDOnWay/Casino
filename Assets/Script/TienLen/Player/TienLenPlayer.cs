using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.TienLen.Player {
    public class TienLenPlayer {
        public int Id { get; }
        public string PlayerName { get; }
        public PlayerHand Hand { get; }
        public CardHolder CardHolder { get; private set; }
        public bool IsHuman { get; }

        public bool HasWon => Hand.Count == 0;

        public TienLenPlayer(
            int id,
            string playerName,
            bool isHuman = true ) {
            Id = id;
            PlayerName = playerName;
            IsHuman = isHuman;

            Hand = new PlayerHand();
        }

        public TienLenPlayer(TienLenNetWorkPlayer tienLenNetWorkPlayer) {
            Id = tienLenNetWorkPlayer.PlayerRef.PlayerId;
            PlayerName = (String)tienLenNetWorkPlayer.PlayerName;
            //TODO: support additional player data from network player


        }

        public void SetCardHolder( CardHolder cardHolder ) {
            CardHolder = cardHolder;
        }

        public bool HasCards( List<Card> cards ) {
            if ( Hand == null ) return false;
            if ( cards == null || cards.Count == 0 ) return false;
            var playercards = Hand.Cards;

            foreach ( var card in cards ) {
                if ( !playercards.Contains(card) ) return false;
            }

            return true;
        }

        public bool TryRemoveCards( List<Card> cards ) {
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