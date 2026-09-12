using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Assets.Script.TienLen.Game {
    public class TienLenGame {

        private readonly List<TienLenPlayer> players = new();

        private Deck deck;

        private int currentPlayerIndex;

        public TienLenGameState State { get; private set; }

        public IReadOnlyList<TienLenPlayer> Players => players;

        public TienLenPlayer CurrentPlayer => players[currentPlayerIndex];


        public event Action<TienLenPlayer> OnTurnChanged;
        public event Action<CardCombination> OnCardsPlayed;
        public event Action<TienLenPlayer> OnPlayerWon;
        public event Action OnRoundStarted;
        public event Action OnRoundEnded;


        public void AddPlayer( TienLenPlayer player ) {

            Debug.Log($"[AddPlayer] Adding player: {player.PlayerName}");

            if ( players.Count >= 4 )
                throw new InvalidOperationException(
                    "Tiến Lên supports 4 players.");

            players.Add(player);
        }

        public void StartGame() {
            if ( players.Count < 1 ) //TODO: Change this to 2 on real game
                throw new InvalidOperationException(
                    "Tiến Lên requires at least 2 players.");

            StartRound();
        }

        public void StartRound() {
            RunRoundRoutineAsync().Forget();
        }

        private async UniTaskVoid RunRoundRoutineAsync( CancellationToken cancellationToken = default ) {
            State = TienLenGameState.Dealing;

            deck.Shuffle();

            foreach ( TienLenPlayer player in players ) {
                player.Hand.Clear();
            }

            // Waits here until all cards finish animating
            await DealCardsAsync(cancellationToken);

            State = TienLenGameState.Playing;
            OnRoundStarted?.Invoke();
        }

        //TODO: Refactor this to handle ui,
        public async UniTask DealCardsAsync( CancellationToken cancellationToken = default ) {
            int delayMs = 80; // 0.08s

            Debug.Log($"[DealCardsAsync] Starting deal. Total players: {players?.Count ?? 0}");

            if ( deck == null ) {
                Debug.LogError("[DealCardsAsync] FAILED: 'deck' reference is NULL!");
                return;
            }

            if ( players == null || players.Count == 0 ) {
                Debug.LogError("[DealCardsAsync] FAILED: 'players' list is NULL or EMPTY!");
                return;
            }

            for ( int i = 0; i < 13; i++ ) {
                //Debug.Log($"[DealCardsAsync] --- Dealing Round {i + 1}/13 ---");

                foreach ( TienLenPlayer player in players ) {
                    if ( cancellationToken.IsCancellationRequested ) {
                        Debug.LogWarning("[DealCardsAsync] Dealing cancelled via CancellationToken.");
                        return;
                    }

                    if ( player == null ) {
                        Debug.LogError("[DealCardsAsync] An entry in 'players' list is NULL!");
                        continue;
                    }

                    CardView card = deck.Draw();
                    if ( card == null ) {
                        Debug.LogError($"[DealCardsAsync] deck.Draw() returned NULL! Deck ran out of cards at round {i + 1}.");
                        return;
                    }

                    Debug.Log($"[DealCardsAsync] Dealing card '{card.name}' to player '{player.PlayerName}' (ID: {player.Id})");

                    // Check Player Hand
                    if ( player.Hand == null ) {
                        Debug.LogError($"[DealCardsAsync] player.Hand is NULL for '{player.PlayerName}'!");
                    }
                    else {
                        player.Hand.AddCard(card.Card);
                    }

                    // Check Player CardHolder
                    if ( player.CardHolder == null ) {
                        Debug.LogError($"[DealCardsAsync] FAILED: player.CardHolder is NULL for '{player.PlayerName}' (ID: {player.Id})! Check if SetCardHolder was called properly.");
                    }
                    else {
                        try {
                            player.CardHolder.AddCard(card, true);
                        }
                        catch ( System.Exception ex ) {
                            Debug.LogError($"[DealCardsAsync] Exception caught inside CardHolder.AddCard for '{player.PlayerName}': {ex.Message}\n{ex.StackTrace}");
                        }
                    }

                    await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
                }
            }

            Debug.Log("[DealCardsAsync] Finished dealing 13 rounds. Starting sorting & final arrangement...");

            foreach ( TienLenPlayer player in players ) {
                if ( player == null ) continue;

                Debug.Log($"[DealCardsAsync] Arranging cards for '{player.PlayerName}'");

                if ( player.CardHolder != null ) {
                    player.CardHolder.SortCards();
                    player.CardHolder.ArrangeCards(true);
                }
                else {
                    Debug.LogWarning($"[DealCardsAsync] Cannot arrange CardHolder: player.CardHolder is NULL for '{player.PlayerName}'");
                }
            }

            Debug.Log("[DealCardsAsync] DealCardsAsync completed successfully.");
        }

        public void SetDeck( Deck deck ) {
            this.deck = deck;
        }
    }
}