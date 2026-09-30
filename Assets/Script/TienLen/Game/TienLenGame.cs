using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Cysharp.Threading.Tasks;
using Fusion;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TienLenGame {
        [Header("Dependencies")]
        private LocalPlayerService localPlayerService;

        private List<TienLenPlayer> players = new();
        public IReadOnlyList<TienLenPlayer> Players => players;

        private Deck deck;
        private int currentPlayerIndex;

        public TienLenGameState State { get; private set; }

        public TienLenPlayer CurrentPlayer => players != null && players.Count > currentPlayerIndex ? players[currentPlayerIndex] : null;

        public event Action<TienLenPlayer> OnPlayerWon;
        public event Action OnRoundStarted;
        public event Action OnRoundEnded;

        [Inject]
        public TienLenGame( LocalPlayerService localPlayerService ) {
            this.localPlayerService = localPlayerService;
        }

        public void Initialize() {
            Debug.Log("[InitializeGame] Initializing Tiến Lên game.");
            State = TienLenGameState.DealingCard;
            currentPlayerIndex = 0;
        }

        public void AddPlayer( TienLenPlayer player ) {
            if ( players.Count >= 4 ) {
                throw new InvalidOperationException("Tiến Lên supports a maximum of 4 players.");
            }

            players.Add(player);
        }

        public void RemovePlayer( PlayerRef playerRef ) {
            players.RemoveAll(p => p.Id == playerRef.PlayerId);
        }

        public void StartRound() {
            DealHandsAuthoritative();
        }

        public void DealHandsAuthoritative() {
            if ( deck == null ) return;
            var tempDeck = deck;
            tempDeck.Shuffle();

            for ( int i = 0; i < 13; i++ ) {
                foreach ( var player in Players ) {
                    var drawnCard = tempDeck.DrawCard();
                    player.Hand.AddCard(drawnCard);
                }
            }
        }

        public void SetDeck( Deck deck ) {
            this.deck = deck;
        }
    }
}
