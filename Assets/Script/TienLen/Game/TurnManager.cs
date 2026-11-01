using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TurnManager : NetworkBehaviour {
        [Header("Dependencies")]
        private TienLenRuleValidator validator;

        private IReadOnlyList<TienLenPlayer> players;
        private int currentPlayerIndex;
        private int lastPlayIndex;
        private readonly HashSet<TienLenPlayer> passedInRound = new();

        public TienLenPlayer CurrentPlayer => players != null && players.Count > 0 && currentPlayerIndex >= 0 && currentPlayerIndex < players.Count
            ? players[currentPlayerIndex]
            : null;

        public event Action<int> OnTurnChanged;

        [Networked] public int currentPlayerIndexNet { get; set; }
        [Networked] int WinnerSeatNet { get; set; } = -1;
        [Networked] int lastPassSeat { get; set; } = -1;
        [Networked] int CurrentTableCardCount { get; set; } = 0;


        [Inject]
        public TurnManager( TienLenRuleValidator validator ) {
            this.validator = validator;
        }

        public override void Spawned() {
        }

        public void Initialize( IReadOnlyList<TienLenPlayer> players ) {
            this.players = players;
            currentPlayerIndex = 0;
            lastPlayIndex = 0;
        }

        /// <summary>Override which ordered-list index starts (e.g. ♠3 holder).</summary>
        public void SetStartingPlayer( int orderIndex ) {
            if ( players == null || players.Count == 0 ) return;
            currentPlayerIndex = Mathf.Clamp(orderIndex, 0, players.Count - 1);
            lastPlayIndex = currentPlayerIndex;
            passedInRound.Clear();
        }

        public bool CanPass( TienLenPlayer player ) {
            if ( player == null ) return false;
            if ( CurrentPlayer != player ) return false;
            return validator != null && validator.CurrentCombination != null;
        }

        public bool TryPlay( TienLenPlayer player, CardCombination combination ) {
            if ( player == null || combination == null ) return false;

            // Make sure it is actually this player's turn.
            if ( CurrentPlayer != player ) return false;

            // Check whether the combination is legal against the previous combination.
            if ( validator != null && !validator.CanPlay(combination) ) return false;

            // The play is valid.
            validator?.SetCurrentCombination(combination);

            // A new play marks this player as the round leader.
            lastPlayIndex = currentPlayerIndex;

            AdvanceTurn();
            return true;
        }

        public bool TryPass( TienLenPlayer player ) {
            if ( player == null ) return false;
            if ( CurrentPlayer != player ) return false;

            // Cannot pass when there is no active combination on the table (must lead the round).
            if ( validator == null || validator.CurrentCombination == null ) return false;

            passedInRound.Add(player);

            // If all other active players have passed in this round, round is over.
            if ( CheckRoundFinished() ) {
                StartNewRound();
                return true;
            }

            AdvanceTurn();
            return true;
        }

        private bool IsPlayerActive( TienLenPlayer player ) {
            if ( player == null ) return false;
            if ( player.HasWon ) return false;
            return true;
        }

        private bool CheckRoundFinished() {
            if ( players == null || players.Count <= 1 ) return true;

            int activeCount = 0;
            foreach ( var p in players ) {
                if ( p != null && IsPlayerActive(p) && !passedInRound.Contains(p) ) {
                    activeCount++;
                }
            }

            return activeCount <= 1;
        }

        private void AdvanceTurn() {
            for ( int i = 1; i <= players.Count; i++ ) {
                int nextIndex = (currentPlayerIndex + i) % players.Count;
                var candidate = players[nextIndex];

                if(currentPlayerIndex == lastPlayIndex ) {
                    StartNewRound();
                }
                if ( candidate != null && IsPlayerActive(candidate) && !passedInRound.Contains(candidate) ) {
                    currentPlayerIndex = nextIndex;
                    OnTurnChanged?.Invoke(nextIndex);
                    return;
                }
            }


            Debug.LogError("[TurnManager] AdvanceTurn: No active player found to advance to. This should not happen.");
            return;
        }

        public void ForceAdvanceTurn() {
            AdvanceTurn();
        }

        public void SetCurrentPlayer( TienLenPlayer player ) {
            if ( players == null || player == null ) return;
            for ( int i = 0; i < players.Count; i++ ) {
                if ( ReferenceEquals(players[i], player) ) {
                    currentPlayerIndex = i;
                    OnTurnChanged?.Invoke(currentPlayerIndex);
                    return;
                }
            }
        }

        private void StartNewRound() {
            validator?.Reset();
            passedInRound.Clear();
        }
    }
}
