using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using Cysharp.Threading.Tasks;
using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Assets.Script.TienLen.Game.Visual {
    public class TienLenDealingAnimator : MonoBehaviour {
        [SerializeField] private CardHolder tableCenterPosition;

        public CardHolder TableCenterPosition {
            get => tableCenterPosition;
            set => tableCenterPosition = value;
        }

        public async UniTask AnimateDealingRoutineAsync(
            ISeatQueryService seatQueryService,
            LocalPlayerService localPlayerService,
            TableVisualLayoutManager tableVisualLayoutManager,
            CardSpawner cardSpawner,
            TienLenSO tienLenSO,
            Sprite cardBack,
            UiManager uiManager,
            Dictionary<PlayerRef, List<Card>> pendingHands,
            Func<PlayerRef, int, bool> isLocalChecker,
            CancellationToken ct,
            Dictionary<(CardSuit, CardRank), Sprite> sprites = null
        ) {
            const int delayMs = 60;
            const int cardsPerPlayer = 13;
            const int totalDeckSize = 52;

            if ( seatQueryService == null || cardSpawner == null ) return;

            var seatedPlayers = seatQueryService.GetSeatedPlayers();
            var networkPlayerMap = seatQueryService.GetNetworkPlayerMap();

            Vector3 centerDeckPos = tableCenterPosition != null
                ? tableCenterPosition.transform.position
                : Vector3.zero;

            Queue<CardView> deckStack = new Queue<CardView>(totalDeckSize);
            var orderedSeats = seatedPlayers.OrderBy(kvp => kvp.Key).ToList();

            // Clear any leftover visual cards from table and all seats before dealing new cards
            if ( tableCenterPosition != null ) {
                tableCenterPosition.Clear();
            }
            if ( tableVisualLayoutManager != null ) {
                tableVisualLayoutManager.RefreshLayout();
                var slots = tableVisualLayoutManager.GetAllVisualSlots();
                if ( slots != null ) {
                    foreach ( var slot in slots ) {
                        slot?.CardHolder?.Clear();
                    }
                }
            }

            try {
                // 1. Spawn deck stack
                for ( int i = 0; i < totalDeckSize; i++ ) {
                    ct.ThrowIfCancellationRequested();

                    CardView backView = cardSpawner.SpawnCardBack(cardBack);
                    backView.transform.position = centerDeckPos;
                    backView.transform.SetParent(transform, worldPositionStays: true);
                    deckStack.Enqueue(backView);
                }

                // 2. Deal cards round-by-round
                for ( int round = 0; round < cardsPerPlayer; round++ ) {
                    for ( int p = 0; p < orderedSeats.Count; p++ ) {
                        int seatIndex = orderedSeats[p].Key;
                        ct.ThrowIfCancellationRequested();

                        if ( deckStack.Count == 0 ) break;

                        CardView cardView = deckStack.Dequeue();

                        PlayerRef seatOwner = default;
                        foreach ( var kvp in networkPlayerMap ) {
                            if ( kvp.Key == seatIndex && kvp.Value != null ) {
                                seatOwner = kvp.Value.PlayerRef;
                                break;
                            }
                        }

                        CardHolder targetCardHolder = tableVisualLayoutManager != null
                            ? tableVisualLayoutManager.GetCardHolderForSeat(seatIndex)
                            : null;

                        if ( targetCardHolder == null ) {
                            tableVisualLayoutManager?.RefreshLayout();
                            targetCardHolder = tableVisualLayoutManager?.GetCardHolderForSeat(seatIndex);
                        }

                        if ( targetCardHolder == null ) {
                            Debug.LogError($"[TienLenDealingAnimator] CardHolder is null for seat {seatIndex}. Destroying visual card.");
                            Destroy(cardView.gameObject);
                            continue;
                        }

                        bool isLocal = isLocalChecker != null && isLocalChecker(seatOwner, seatIndex);

                        if ( isLocal ) {
                            const int handWaitMs = 5000;
                            int waitedMs = 0;
                            List<Card> localCards = null;

                            while ( waitedMs < handWaitMs ) {
                                // Check pendingHands cache first
                                if ( pendingHands != null && pendingHands.TryGetValue(seatOwner, out var buffered) && buffered != null && buffered.Count > round ) {
                                    ApplyHand(seatOwner, buffered, seatQueryService, localPlayerService);
                                    localCards = buffered;
                                    break;
                                }

                                // Check logic player
                                var livePlayer = seatQueryService?.GetLogicPlayer(seatOwner) ?? localPlayerService?.GetLocalLogicPlayer();
                                if ( livePlayer?.Hand?.Cards != null && livePlayer.Hand.Cards.Count > round ) {
                                    localCards = livePlayer.Hand.Cards.ToList();
                                    break;
                                }

                                await UniTask.Delay(50, cancellationToken: ct);
                                waitedMs += 50;
                            }

                            if ( localCards == null || localCards.Count <= round ) {
                                Debug.LogError($"[TienLenDealingAnimator] Hand missing card at index {round} for local player.");
                                Destroy(cardView.gameObject);
                                continue;
                            }

                            Card cardData = localCards[round];
                            Sprite cardSprite = null;
                            if ( sprites != null && sprites.TryGetValue((cardData.Suit, cardData.Rank), out Sprite sp) ) {
                                cardSprite = sp;
                            }
                            else if ( tienLenSO != null ) {
                                cardSprite = tienLenSO.GetCardSprite(cardData.Suit, cardData.Rank);
                            }

                            CardView faceCardView = cardSpawner.SpawnCard(cardData, cardSprite);
                            faceCardView.transform.position = cardView.transform.position;
                            faceCardView.transform.rotation = cardView.transform.rotation;
                            faceCardView.transform.SetParent(targetCardHolder.transform, worldPositionStays: true);

                            Destroy(cardView.gameObject);
                            targetCardHolder.AddCard(faceCardView, animate: true);
                        }
                        else {
                            cardView.transform.SetParent(targetCardHolder.transform, worldPositionStays: true);
                            targetCardHolder.AddCard(cardView, animate: true);
                        }
                    }

                    await UniTask.Delay(delayMs, cancellationToken: ct);
                }

                // 3. Sort and fan local player hand
                CardHolder localHand = tableVisualLayoutManager != null
                    ? tableVisualLayoutManager.GetLocalCardHolder()
                    : null;

                if ( localHand != null ) {
                    localHand.SortCards();
                    localHand.ArrangeCards(animate: true);
                }

                if ( uiManager != null ) {
                    uiManager.Refresh(LobbyChangeReason.TurnChanged);
                }
            }
            finally {
                while ( deckStack.Count > 0 ) {
                    CardView leftover = deckStack.Dequeue();
                    if ( leftover != null ) {
                        Destroy(leftover.gameObject);
                    }
                }
            }
        }

        private static void ApplyHand(
            PlayerRef owner,
            List<Card> hand,
            ISeatQueryService seatQuery,
            LocalPlayerService localPlayer
        ) {
            if ( hand == null || hand.Count == 0 ) return;
            TienLenPlayer player = seatQuery?.GetLogicPlayer(owner) ?? localPlayer?.GetLocalLogicPlayer();
            if ( player?.Hand == null ) return;

            player.Hand.Clear();
            player.Hand.AddCard(hand);
        }
    }
}
