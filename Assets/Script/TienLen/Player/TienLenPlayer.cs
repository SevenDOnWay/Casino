using Assets.Script.NetWorkScript;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Script.TienLen.Player {
    /// <summary>
    /// Pure domain model for a Tiến Lên player.
    /// Manages identity, hand state, and logic validation without any UI references.
    /// </summary>
    public class TienLenPlayer {
        public int Id { get; }
        public string PlayerName { get; }
        public PlayerHand Hand { get; }
        public bool IsHuman { get; }

        public bool HasWon => Hand != null && Hand.Count == 0;

        public TienLenPlayer() {
            Hand = new PlayerHand();
        }

        public TienLenPlayer(
            int id,
            string playerName,
            bool isHuman = true ) {
            Id = id;
            PlayerName = playerName;
            IsHuman = isHuman;
            Hand = new PlayerHand();
        }

        public TienLenPlayer( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            Id = tienLenNetWorkPlayer.PlayerRef.PlayerId;
            PlayerName = (string)tienLenNetWorkPlayer.PlayerName;
            IsHuman = true;
            Hand = new PlayerHand();
        }

        public bool HasCards( List<Card> cards ) {
            if ( Hand == null || Hand.Cards == null ) return false;
            if ( cards == null || cards.Count == 0 ) return false;
            var playerCards = Hand.Cards;

            foreach ( var card in cards ) {
                if ( card == null || !playerCards.Contains(card) ) {
                    return false;
                }
            }

            return true;
        }

        public bool TryRemoveCards( List<Card> cards ) {
            if ( cards == null || cards.Count == 0 ) return false;
            if ( Hand == null ) return false;

            if ( !HasCards(cards) ) return false;

            Hand.Remove(cards);
            return true;
        }
    }
}
