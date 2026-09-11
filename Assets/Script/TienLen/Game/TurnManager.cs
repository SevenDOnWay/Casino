using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Multiplayer.PlayMode;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TurnManager {
        [Header("Dependencies")]
        private TienLenRuleValidator validator;

        private IReadOnlyList<TienLenPlayer> players;

        private CardCombination currentCombination;

        public TienLenPlayer CurrentPlayer =>
            players != null && players.Count > 0 ? players[currentPlayerIndex] : null;

        private int currentPlayerIndex;
        private int lastPlayerIndex;
        private int passedPlayers;


        public event Action OnTurnChanged;


        [Inject]
        public TurnManager( TienLenRuleValidator validator ) {
            this.validator = validator;
        }

        public void Initialize( IReadOnlyList<TienLenPlayer> players ) {
            this.players = players;

            currentPlayerIndex = 0;
            lastPlayerIndex = -1;
            passedPlayers = 0;
            currentCombination = null;
        }

        public void ChangeState( TienLenGameState newState ) {
            OnTurnChanged?.Invoke();
        }

        public bool TryPlay(
           TienLenPlayer player,
           CardCombination combination ) {
            if ( player == null )
                return false;

            if ( combination == null )
                return false;

            // Make sure it is actually this player's turn.
            if ( CurrentPlayer != player )
                return false;

            // Check whether the combination is legal against
            // the previous combination.
            if ( !validator.CanPlay(combination) )
                return false;

            // The play is valid.
            validator.SetCurrentCombination(combination);

            lastPlayerIndex = currentPlayerIndex;
            passedPlayers = 0;

            AdvanceTurn();

            return true;
        }

        public bool TryPass( TienLenPlayer player ) {
            if ( player == null )
                return false;

            if ( CurrentPlayer != player )
                return false;

            // Cannot pass when there is no active combination.
            if ( validator.CurrentCombination == null )
                return false;

            passedPlayers++;

            AdvanceTurn();

            // Everyone except the player who made the last
            // combination has passed.
            if ( passedPlayers >= players.Count - 1 ) {
                StartNewRound();
            }

            return true;
        }

        private void AdvanceTurn() {
            currentPlayerIndex++;

            if ( currentPlayerIndex >= players.Count ) currentPlayerIndex = 0;
        }

        private void StartNewRound() {
            validator.Reset();

            passedPlayers = 0;

            // The player who played the last valid combination
            // starts the new round.
            currentPlayerIndex = lastPlayerIndex;

            lastPlayerIndex = -1;
        }

    }

}