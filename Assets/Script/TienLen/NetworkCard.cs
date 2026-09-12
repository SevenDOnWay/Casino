using Fusion;
using System.Collections;
using UnityEngine;

namespace Assets.Script.TienLen {
    public struct NetworkCard : INetworkStruct {

        public byte Rank;
        public byte Suit;

        public NetworkCard( Card card ) {
            Rank = (byte)card.Rank;
            Suit = (byte)card.Suit;
        }

        public Card ToCard() {
            return new Card((CardRank)Rank, (CardSuit)Suit);
        }
    }
}