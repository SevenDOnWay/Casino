using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game.Services {
    public enum AutoActionResultType {
        Pass,
        PlayCards,
        ForceAdvance
    }

    public struct AutoActionDecision {
        public AutoActionResultType ActionType;
        public List<Card> CardsToPlay;
        public CardCombination Combination;

        public static AutoActionDecision Pass() =>
            new AutoActionDecision { ActionType = AutoActionResultType.Pass };

        public static AutoActionDecision Play( List<Card> cards, CardCombination combo ) =>
            new AutoActionDecision { ActionType = AutoActionResultType.PlayCards, CardsToPlay = cards, Combination = combo };

        public static AutoActionDecision ForceAdvance() =>
            new AutoActionDecision { ActionType = AutoActionResultType.ForceAdvance };
    }

    public class TienLenTurnHandler {
        private readonly CardCombinationEvaluator cardCombinationEvaluator;
        private readonly TienLenRuleValidator validator;

        [Inject]
        public TienLenTurnHandler(
            CardCombinationEvaluator cardCombinationEvaluator,
            TienLenRuleValidator validator
        ) {
            this.cardCombinationEvaluator = cardCombinationEvaluator;
            this.validator = validator;
        }

        public int FindStartingSeatOrderIndex( List<TienLenPlayer> ordered ) {
            if ( ordered == null ) return -1;
            for ( int i = 0; i < ordered.Count; i++ ) {
                var cards = ordered[i]?.Hand?.Cards;
                if ( cards == null ) continue;
                foreach ( var card in cards ) {
                    if ( card.Rank == CardRank.Three && card.Suit == CardSuit.Spades ) {
                        return i;
                    }
                }
            }
            return -1;
        }

        public AutoActionDecision DecideAutoAction( TienLenPlayer currentPlayer, bool isFirstPlayOfGame ) {
            if ( currentPlayer == null ) {
                return AutoActionDecision.ForceAdvance();
            }

            // If there is an active table combination, timeout counts as a PASS
            if ( validator != null && validator.CurrentCombination != null ) {
                return AutoActionDecision.Pass();
            }

            // Leading a new round: cannot pass, so auto-play lowest valid single card (leftmost)
            var hand = currentPlayer.Hand?.Cards;
            if ( hand != null && hand.Count > 0 ) {
                // If first play of the game, must include 3 of Spades if held
                if ( isFirstPlayOfGame ) {
                    var threeOfSpades = hand.FirstOrDefault(c => c.Rank == CardRank.Three && c.Suit == CardSuit.Spades);
                    if ( threeOfSpades != null ) {
                        var threeList = new List<Card> { threeOfSpades };
                        if ( cardCombinationEvaluator != null &&
                             cardCombinationEvaluator.TryEvaluate(threeList, out var combo) &&
                             (validator == null || validator.CanPlay(combo)) ) {
                            return AutoActionDecision.Play(threeList, combo);
                        }
                    }
                }

                var sortedHand = hand.OrderBy(c => c.Rank).ThenBy(c => (int)c.Suit).ToList();
                foreach ( var card in sortedHand ) {
                    var singleList = new List<Card> { card };
                    if ( cardCombinationEvaluator != null &&
                         cardCombinationEvaluator.TryEvaluate(singleList, out var combo) &&
                         (validator == null || validator.CanPlay(combo)) ) {
                        return AutoActionDecision.Play(singleList, combo);
                    }
                }
            }

            Debug.LogWarning($"[TienLenTurnHandler] Auto-play could not find valid play for player {currentPlayer.Id}. Force advancing turn.");
            return AutoActionDecision.ForceAdvance();
        }
    }
}
