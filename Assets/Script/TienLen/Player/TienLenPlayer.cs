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
        public string PlayerName { get; private set; }
        public int AvatarId { get; set; } = 0;
        public int Level { get; set; } = 1;
        public long Money { get; set; } = 10000;
        public PlayerHand Hand { get; }
        public bool IsHuman { get; }

        public bool HasWon => Hand != null && Hand.Count == 0;

        public TienLenPlayer() {
            Hand = new PlayerHand();
        }

        public TienLenPlayer(
            int id,
            string playerName,
            bool isHuman = true,
            int avatarId = 0,
            int level = 1,
            long money = 10000 ) {
            Id = id;
            PlayerName = playerName;
            IsHuman = isHuman;
            AvatarId = avatarId;
            Level = level;
            Money = money;
            Hand = new PlayerHand();
        }

        public TienLenPlayer( TienLenNetWorkPlayer tienLenNetWorkPlayer ) {
            Id = tienLenNetWorkPlayer.PlayerRef.PlayerId;
            PlayerName = (string)tienLenNetWorkPlayer.PlayerName;
            AvatarId = tienLenNetWorkPlayer.AvatarId;
            Level = tienLenNetWorkPlayer.Level > 0 ? tienLenNetWorkPlayer.Level : 1;
            Money = tienLenNetWorkPlayer.Money > 0 ? tienLenNetWorkPlayer.Money : 10000;
            IsHuman = true;
            Hand = new PlayerHand();
        }

        public void UpdateFromNetwork( TienLenNetWorkPlayer netPlayer ) {
            if ( netPlayer == null ) return;
            if ( !string.IsNullOrEmpty((string)netPlayer.PlayerName) ) {
                PlayerName = (string)netPlayer.PlayerName;
            }
            AvatarId = netPlayer.AvatarId;
            Level = netPlayer.Level > 0 ? netPlayer.Level : 1;
            Money = netPlayer.Money > 0 ? netPlayer.Money : 10000;
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
