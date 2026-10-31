using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Script.TienLen.Game.Services {
    public class TienLenMigrationHandler {
        private readonly ISeatQueryService seatQueryService;
        private readonly TurnManager turnManager;
        private readonly CardCombinationEvaluator cardCombinationEvaluator;
        private readonly TienLenRuleValidator validator;

        public TienLenMigrationHandler(
            ISeatQueryService seatQueryService,
            TurnManager turnManager,
            CardCombinationEvaluator cardCombinationEvaluator,
            TienLenRuleValidator validator
        ) {
            this.seatQueryService = seatQueryService;
            this.turnManager = turnManager;
            this.cardCombinationEvaluator = cardCombinationEvaluator;
            this.validator = validator;
        }

        public bool TryResumeState(
            int currentTableCardCount,
            IReadOnlyList<Card> tableCards,
            int currentTurnSeat,
            out int singleRemainingWinnerSeat,
            out bool turnAdvanced
        ) {
            singleRemainingWinnerSeat = -1;
            turnAdvanced = false;

            if ( seatQueryService == null || turnManager == null ) return false;

            List<TienLenPlayer> ordered = seatQueryService.GetSeatedPlayers()
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();

            if ( ordered.Count == 0 ) return false;

            if ( ordered.Count == 1 ) {
                var seated = seatQueryService.GetSeatedPlayers();
                singleRemainingWinnerSeat = seated.Keys.First();
                return true;
            }

            turnManager.Initialize(ordered);

            // Reconstruct validator table combination from table cards
            if ( currentTableCardCount > 0 && tableCards != null && tableCards.Count > 0 ) {
                if ( cardCombinationEvaluator != null && cardCombinationEvaluator.TryEvaluate(tableCards.ToList(), out CardCombination combo) ) {
                    validator?.SetCurrentCombination(combo);
                    Debug.Log($"[TienLenMigrationHandler] Restored table combination: {combo.Type} ({combo.Cards.Count} cards).");
                }
            }
            else {
                validator?.Reset();
            }

            // Restore active turn player
            if ( currentTurnSeat != -1 ) {
                var seated = seatQueryService.GetSeatedPlayers();
                if ( seated.TryGetValue(currentTurnSeat, out var turnPlayer) && turnPlayer != null ) {
                    turnManager.SetCurrentPlayer(turnPlayer);
                }
                else {
                    // Player left -> advance turn
                    turnManager.ForceAdvanceTurn();
                    turnAdvanced = true;
                }
            }

            return true;
        }

        public bool ProcessPlayerLeftMidGame(
            PlayerRef playerRef,
            out int remainingWinnerSeat,
            out bool shouldAutoActCurrentPlayer
        ) {
            remainingWinnerSeat = -1;
            shouldAutoActCurrentPlayer = false;

            if ( seatQueryService == null ) return false;

            Debug.Log($"[TienLenMigrationHandler] Player {playerRef.PlayerId} left mid-game.");

            var logicPlayer = seatQueryService.GetLogicPlayer(playerRef);
            if ( logicPlayer != null ) {
                logicPlayer.IsInactive = true;
                Debug.Log($"[TienLenMigrationHandler] Marked player {logicPlayer.Id} as Inactive.");
            }

            var seated = seatQueryService.GetSeatedPlayers();
            if ( seated == null ) return false;

            int activeCount = seated.Values.Count(p => p != null && !p.IsInactive);
            int lastActiveSeat = -1;
            foreach ( var kvp in seated ) {
                if ( kvp.Value != null && !kvp.Value.IsInactive ) {
                    lastActiveSeat = kvp.Key;
                    break;
                }
            }

            if ( activeCount <= 1 ) {
                if ( activeCount == 1 && lastActiveSeat != -1 ) {
                    remainingWinnerSeat = lastActiveSeat;
                }
                return true;
            }

            if ( turnManager?.CurrentPlayer != null && logicPlayer != null && ReferenceEquals(turnManager.CurrentPlayer, logicPlayer) ) {
                Debug.Log("[TienLenMigrationHandler] Active turn player disconnected. Signal auto-act.");
                shouldAutoActCurrentPlayer = true;
            }

            return true;
        }

        public void RestoreReportedHand( TienLenPlayer player, IEnumerable<Card> cards ) {
            if ( player == null || cards == null ) return;
            player.Hand.Clear();
            player.Hand.AddCard(cards);
            Debug.Log($"[TienLenMigrationHandler] Restored hand for player {player.Id} on new Host.");
        }
    }
}
