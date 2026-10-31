using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Script.TienLen.Game.Services {
    public class TienLenActionHandler {
        private readonly CardCombinationEvaluator cardCombinationEvaluator;
        private readonly TurnManager turnManager;
        private readonly TienLenRuleValidator validator;

        public bool IsFirstPlayOfGame { get; set; } = true;

        public TienLenActionHandler(
            CardCombinationEvaluator cardCombinationEvaluator,
            TurnManager turnManager,
            TienLenRuleValidator validator
        ) {
            this.cardCombinationEvaluator = cardCombinationEvaluator;
            this.turnManager = turnManager;
            this.validator = validator;
        }

        public bool TryValidateAndExecutePlay(
            TienLenPlayer player,
            List<Card> requestedCards,
            out CardCombination combination,
            out string failureReason
        ) {
            combination = null;
            failureReason = string.Empty;

            if ( player == null ) {
                failureReason = "Player is null.";
                return false;
            }

            if ( requestedCards == null || requestedCards.Count == 0 ) {
                failureReason = "No cards provided.";
                return false;
            }

            // First round of match rule: must play 3 of Spades if rule is active
            if ( IsFirstPlayOfGame ) {
                bool hasThreeOfSpades = requestedCards.Any(c => c.Rank == CardRank.Three && c.Suit == CardSuit.Spades);
                if ( !hasThreeOfSpades ) {
                    failureReason = $"Player {player.Id} first play of the match must include 3 of Spades.";
                    Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                    return false;
                }
            }

            if ( !player.HasCards(requestedCards) ) {
                failureReason = $"Player {player.Id} tried to play cards they don't own.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            if ( cardCombinationEvaluator == null || !cardCombinationEvaluator.TryEvaluate(requestedCards, out combination) ) {
                failureReason = $"Player {player.Id} submitted an invalid combination.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            if ( turnManager == null || !turnManager.TryPlay(player, combination) ) {
                failureReason = $"Player {player.Id} cannot play this combination under current rules.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            if ( !player.TryRemoveCards(requestedCards) ) {
                failureReason = $"Failed to remove played cards from player {player.Id}.";
                Debug.LogError($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            IsFirstPlayOfGame = false;
            return true;
        }

        public bool TryExecutePass( TienLenPlayer player, out bool shouldClearTableCenter, out string failureReason ) {
            shouldClearTableCenter = false;
            failureReason = string.Empty;

            if ( player == null ) {
                failureReason = "Player is null.";
                return false;
            }

            if ( turnManager == null || !turnManager.TryPass(player) ) {
                failureReason = $"Player {player.Id} cannot pass.";
                Debug.LogWarning($"[TienLenActionHandler] {failureReason}");
                return false;
            }

            if ( validator != null && validator.CurrentCombination == null ) {
                shouldClearTableCenter = true;
            }

            return true;
        }

        public bool CheckPlayerWin( TienLenPlayer player ) {
            return player != null && player.Hand != null && player.Hand.Count == 0;
        }
    }
}
