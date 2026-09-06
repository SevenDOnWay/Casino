using Assets.Script.TienLen.Player;
using System.Collections;
using UnityEngine;

namespace Assets.Script.TienLen.Player {
    public class TienLenPlayer {

        public int Id { get; }
        public string PlayerName { get; }

        public PlayerHand Hand { get; } = new();

        public bool IsHuman { get; }

        public bool HasWon => Hand.Count == 0;

        public TienLenPlayer(
            int id,
            string playerName,
            bool isHuman ) {
            Id = id;
            PlayerName = playerName;
            IsHuman = isHuman;
        }



    }
}