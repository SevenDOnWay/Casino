using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using System.Collections;
using UnityEngine;

namespace Assets.Script.TienLen.Game {
    public class TienLenGameController : MonoBehaviour {
        private TienLenGame game;

        private void Start() {
            game = new TienLenGame();

            game.OnTurnChanged += HandleTurnChanged;
            game.OnCardsPlayed += HandleCardsPlayed;
            game.OnPlayerWon += HandlePlayerWon;

            CreatePlayers();

            game.StartGame();
        }

        // hard player for now
        private void CreatePlayers() {
            game.AddPlayer(
                new TienLenPlayer(
                    0,
                    "Player",
                    true));

            game.AddPlayer(
                new TienLenPlayer(
                    1,
                    "Bot 1",
                    false));

            game.AddPlayer(
                new TienLenPlayer(
                    2,
                    "Bot 2",
                    false));

            game.AddPlayer(
                new TienLenPlayer(
                    3,
                    "Bot 3",
                    false));
        }


        private void HandleTurnChanged( TienLenPlayer player ) {
            Debug.Log($"Turn: {player.PlayerName}");
        }

        private void HandleCardsPlayed( CardCombination combination ) {
            Debug.Log($"Played {combination.Type}");
        }

        private void HandlePlayerWon( TienLenPlayer player ) {
            Debug.Log($"{player.PlayerName} wins!");
        }

    }
}