using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using Fusion;
using System.Collections;
using UnityEngine;

namespace Assets.Script.TienLen.Player {
    public class TienLenPlayer {

        public int Id { get; }
        public PlayerRef PlayerRef { get; }
        public string PlayerName { get; }

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
            bool isHuman = true ) {
            Id = id;
            PlayerRef = playerRef;
            PlayerName = playerName;
            IsLocalPlayer = isLocalPlayer;
            IsHuman = isHuman;

            Hand = new PlayerHand();
        }

        public void SetCardHolder( CardHolder cardHolder ) {
            CardHolder = cardHolder;
        }

    }
}