using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;

namespace Assets.Script.TienLen.Game {
    public class TienLenGame {

        private readonly List<TienLenPlayer> players = new();

        private Deck deck;

        private int currentPlayerIndex;

        private TienLen.Rule.CardCombination currentCombination;

        private int lastPlayerIndex;

        private int passedPlayers;

        public TienLenGameState State { get; private set; }

        public IReadOnlyList<TienLenPlayer> Players => players;

        public TienLenPlayer CurrentPlayer => players[currentPlayerIndex];

        public TienLen.Rule.CardCombination CurrentCombination => currentCombination;

        public event Action<TienLenPlayer> OnTurnChanged;
        public event Action<TienLen.Rule.CardCombination> OnCardsPlayed;
        public event Action<TienLenPlayer> OnPlayerWon;
        public event Action OnRoundStarted;
        public event Action OnRoundEnded;


        public void AddPlayer( TienLenPlayer player ) {
            if ( players.Count >= 4 )
                throw new InvalidOperationException(
                    "Tiến Lên supports 4 players.");

            players.Add(player);
        }

        public void StartGame() {
            if ( players.Count < 2 )
                throw new InvalidOperationException(
                    "Tiến Lên requires at least 2 players.");

            StartRound();
        }

        public void StartRound() {
            State = TienLenGameState.Dealing;

            deck ??= new Deck();
            deck.Shuffle();

            foreach ( TienLenPlayer player in players )
                player.Hand.Clear();

            DealCards();

            DetermineFirstPlayer();

            currentCombination = null;
            passedPlayers = 0;
            lastPlayerIndex = currentPlayerIndex;

            State = TienLenGameState.Playing;

            OnRoundStarted?.Invoke();
            OnTurnChanged?.Invoke(CurrentPlayer);
        }

        //TODO: Refactor this to handle ui,

        private void DealCards() {
            for ( int i = 0; i < 13; i++ ) {
                foreach ( TienLenPlayer player in players ) {
                    player.Hand.AddCard(deck.Draw());
                }
            }

            foreach ( TienLenPlayer player in players )
                player.Hand.Sort();
        }

        private void DetermineFirstPlayer() {
            for ( int i = 0; i < players.Count; i++ ) {
                if ( players[i].Hand.CardViews.Any(
                    c => c.Card.Rank == CardRank.Three &&
                         c.Card.Suit == CardSuit.Spades) ) {
                    currentPlayerIndex = i;
                    return;
                }
            }

            currentPlayerIndex = 0;
        }



    }
}