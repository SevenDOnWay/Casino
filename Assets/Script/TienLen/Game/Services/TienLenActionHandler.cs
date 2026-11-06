using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Game.Controller;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game.Services {
    public class TienLenActionHandler : IActionHandler {
        private readonly CardCombinationEvaluator cardCombinationEvaluator;
        private readonly TienLenRuleValidator validator;
        private readonly TurnManager turnManager;
        private readonly ISeatQueryService seatQueryService;
        private readonly TienLenGameController gameController;
        private readonly IAnimationPlayer animationPlayer;
        private readonly MatchController matchController;

        public bool IsFirstPlayOfGame { get; set; } = true;

        [Inject]
        public TienLenActionHandler(
            CardCombinationEvaluator cardCombinationEvaluator,
            TurnManager turnManager,
            TienLenRuleValidator validator,
            ISeatQueryService seatQueryService = null,
            TienLenGameController gameController = null,
            IAnimationPlayer animationPlayer = null,
            MatchController matchController = null
        ) {
            this.cardCombinationEvaluator = cardCombinationEvaluator;
            this.turnManager = turnManager;
            this.validator = validator;
            this.seatQueryService = seatQueryService;
            this.gameController = gameController;
            this.animationPlayer = animationPlayer;
            this.matchController = matchController;
        }

        public bool TryValidateAndExecutePlay(
            TienLenPlayer player,
            NetworkCard[] requestedCards,
            out CardCombination combination,
            out string failureReason
        ) {
            combination = null;
            failureReason = string.Empty;

            if ( player == null || requestedCards == null || requestedCards.Length == 0 ) {
                failureReason = "Invalid player or card selection.";
                return false;
            }

            // Reject actions if match has concluded or is not active
            if ( gameController != null && (gameController.WinnerSeat != -1 || !gameController.IsGameStarted) ) {
                failureReason = "Match is not in progress or has already concluded.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            // Must be player's turn
            if ( turnManager != null && !turnManager.IsPlayerTurn(player) ) {
                failureReason = $"Player {player.Id} attempted to play out of turn.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            List<Card> cards = requestedCards.Select(c => c.ToCard()).ToList();

            // Verify player actually has all requested cards in hand
            if ( !player.HasCards(cards) ) {
                failureReason = $"Player {player.Id} does not possess all requested cards.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            // Evaluate combination
            if ( cardCombinationEvaluator == null || !cardCombinationEvaluator.TryEvaluate(cards, out combination) ) {
                failureReason = "Cards do not form a valid Tien Len combination.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            // Enforce first play of game requires 3 of Spades
            if ( IsFirstPlayOfGame ) {
                bool hasThreeOfSpades = cards.Any(c => c.Rank == CardRank.Three && c.Suit == CardSuit.Spades);
                if ( !hasThreeOfSpades ) {
                    failureReason = "First play of the match must include the 3 of Spades.";
                    Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                    return false;
                }
            }

            // Check legality against current table combination
            if ( validator != null && !validator.CanPlay(combination) ) {
                failureReason = $"Combination cannot beat current table combination ({validator.CurrentCombination?.Type}).";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            // Execute play
            IsFirstPlayOfGame = false;
            if ( gameController != null ) {
                gameController.IsFirstPlayOfGame = false;
            }
            player.IsInactive = false;
            player.TryRemoveCards(cards);
            validator?.SetCurrentCombination(combination);

            int seatIndex = SeatOfPlayer(player);
            PlayerRef senderRef = FindPlayerRef(player);

            if ( gameController != null ) {
                gameController.CurrentTableCardCount = requestedCards.Length;
                for ( int i = 0; i < requestedCards.Length && i < 13; i++ ) {
                    gameController.CurrentTableCards.Set(i, requestedCards[i]);
                }
                gameController.LastPassSeat = -1;
            }

            // Register leader and advance turn
            turnManager?.SetLastPlayPlayer(player);
            turnManager?.AdvanceTurn();
            gameController?.MirrorTurnSeat();

            // Animate card play
            animationPlayer?.PlayCardPlayAnimation(senderRef, seatIndex, requestedCards);

            // Win check
            if ( CheckPlayerWin(player) ) {
                Debug.Log($"[TienLenActionHandler] Player {player.Id} (Seat {seatIndex}) has emptied hand and won!");
                matchController?.EndGame(seatIndex);
            }

            return true;
        }

        public bool TryExecutePass(
            TienLenPlayer player,
            out bool shouldClearTableCenter,
            out string failureReason
        ) {
            shouldClearTableCenter = false;
            failureReason = string.Empty;

            if ( player == null ) {
                failureReason = "Player is null.";
                return false;
            }

            // Reject actions if match has concluded or is not active
            if ( gameController != null && (gameController.WinnerSeat != -1 || !gameController.IsGameStarted) ) {
                failureReason = "Match is not in progress or has already concluded.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            if ( turnManager != null && !turnManager.IsPlayerTurn(player) ) {
                failureReason = $"Player {player.Id} attempted to pass out of turn.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            // Cannot pass when table is empty / leading the round
            if ( validator == null || validator.CurrentCombination == null ) {
                failureReason = "Cannot pass when leading the round.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            player.IsInactive = false;
            turnManager?.AddPassPlayer(player);

            int seatIndex = SeatOfPlayer(player);
            if ( gameController != null ) {
                gameController.LastPassSeat = seatIndex;
            }

            if ( turnManager != null && turnManager.CheckRoundFinished() ) {
                shouldClearTableCenter = true;
                if ( gameController != null ) {
                    gameController.CurrentTableCardCount = 0;
                }
                validator?.Reset();
                animationPlayer?.ClearTableCenter();
                turnManager.ResetRoundToLeader();
            }
            else {
                turnManager?.AdvanceTurn();
            }
            gameController?.MirrorTurnSeat();
            return true;
        }

        public bool CheckPlayerWin( TienLenPlayer player ) {
            return player != null && player.Hand != null && player.Hand.Count == 0;
        }

        private int SeatOfPlayer( TienLenPlayer player ) {
            if ( seatQueryService == null || player == null ) return -1;
            foreach ( var kvp in seatQueryService.GetSeatedPlayers() ) {
                if ( ReferenceEquals(kvp.Value, player) ) return kvp.Key;
            }
            return -1;
        }

        private PlayerRef FindPlayerRef( TienLenPlayer player ) {
            if ( seatQueryService == null || player == null ) return PlayerRef.None;
            var seated = seatQueryService.GetSeatedPlayers();
            var netMap = seatQueryService.GetNetworkPlayerMap();
            foreach ( var kvp in seated ) {
                if ( ReferenceEquals(kvp.Value, player) ) {
                    if ( netMap != null && netMap.TryGetValue(kvp.Key, out var netPlayer) && netPlayer != null ) {
                        return netPlayer.PlayerRef;
                    }
                }
            }
            return PlayerRef.None;
        }
    }
}
