using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TurnManager {
        [Header("Dependencies")]
        private TienLenRuleValidator validator;

        private IReadOnlyList<TienLenPlayer> players;
        private int currentPlayerIndex;
        private int lastPlayIndex;
        private int passedPlayers;

        public TienLenPlayer CurrentPlayer => players != null && players.Count > 0 && currentPlayerIndex < players.Count
            ? players[currentPlayerIndex]
            : null;

        public event Action OnTurnChanged;

        [Inject]
        public TurnManager( TienLenRuleValidator validator ) {
            this.validator = validator;
        }

        public void Initialize( IReadOnlyList<TienLenPlayer> players ) {
            this.players = players;
            currentPlayerIndex = 0;
            passedPlayers = 0;
        }

        /// <summary>Override which ordered-list index starts (e.g. ♠3 holder).</summary>
        public void SetStartingPlayer( int orderIndex ) {
            if ( players == null || players.Count == 0 ) return;
            currentPlayerIndex = Mathf.Clamp(orderIndex, 0, players.Count - 1);
            passedPlayers = 0;
        }

        public bool TryPlay( TienLenPlayer player, CardCombination combination ) {
            if ( player == null || combination == null ) return false;

            // Make sure it is actually this player's turn.
            if ( CurrentPlayer != player ) return false;

            // Check whether the combination is legal against the previous combination.
            if ( !validator.CanPlay(combination) ) return false;

            // The play is valid.
            validator.SetCurrentCombination(combination);

            // A new play resets the pass count and marks this player as the round leader.
            passedPlayers = 0;
            lastPlayIndex = currentPlayerIndex;

            AdvanceTurn();
            return true;
        }

        public bool TryPass( TienLenPlayer player ) {
            if ( player == null ) return false;
            if ( CurrentPlayer != player ) return false;

            // Cannot pass when there is no active combination.
            if ( validator.CurrentCombination == null ) return false;

            passedPlayers++;

            // Everyone except the player who made the last combination has passed.
            if ( passedPlayers >= players.Count - 1 ) {
                StartNewRound();
                return true;
            }

            AdvanceTurn();
            return true;
        }

        private void AdvanceTurn() {
            currentPlayerIndex++;
            if ( currentPlayerIndex >= players.Count ) currentPlayerIndex = 0;
            OnTurnChanged?.Invoke();
        }

        public void ForceAdvanceTurn() {
            AdvanceTurn();
        }

        public void SetCurrentPlayer( TienLenPlayer player ) {
            if ( players == null || player == null ) return;
            for ( int i = 0; i < players.Count; i++ ) {
                if ( ReferenceEquals(players[i], player) ) {
                    currentPlayerIndex = i;
                    OnTurnChanged?.Invoke();
                    return;
                }
            }
        }

        public void SetLastPlayPlayer( TienLenPlayer player ) {
            if ( players == null || player == null ) return;
            for ( int i = 0; i < players.Count; i++ ) {
                if ( ReferenceEquals(players[i], player) ) {
                    lastPlayIndex = i;
                    return;
                }
            }
        }

        public void ResetRound() {
            StartNewRound();
        }

        private void StartNewRound() {
            validator.Reset();
            passedPlayers = 0;

            // The player who played the last valid combination starts the new round.
            currentPlayerIndex = lastPlayIndex;
            OnTurnChanged?.Invoke();
        }
    }
}
