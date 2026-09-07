using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using System.Collections;
using UnityEngine;

namespace Assets.Script.TienLen.Player {
    public class TienLenPlayer {

        public int Id { get; }
        public string PlayerName { get; }

        public PlayerHand Hand { get; } = new();

        public bool IsHuman { get; }

        public CardHolder CardHolder { get; private set; }
        public bool HasWon => Hand.Count == 0;

        public TienLenPlayer(
            int id,
            string playerName,
            bool isHuman ) {
            Id = id;
            PlayerName = playerName;
            IsHuman = isHuman;
        }

        public void SetCardHolder( CardHolder cardHolder ) {
            CardHolder = cardHolder;
        }

    }
}