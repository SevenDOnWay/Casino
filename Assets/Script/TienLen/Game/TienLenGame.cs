using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Cysharp.Threading.Tasks;
using Fusion;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using VContainer;
using static Unity.Collections.Unicode;

namespace Assets.Script.TienLen.Game {
    public class TienLenGame {
        [Header("Dependencies")]
        LocalPlayerService localPlayerService;

        private readonly List<TienLenPlayer> players = new();

        private Deck deck;

        private int currentPlayerIndex;

        public TienLenGameState State { get; private set; }

        public IReadOnlyList<TienLenPlayer> Players => players;

        public TienLenPlayer CurrentPlayer => players[currentPlayerIndex];

        public event Action<TienLenPlayer> OnPlayerWon;
        public event Action OnRoundStarted;
        public event Action OnRoundEnded;

        [Inject]
        void Construct( LocalPlayerService localPlayerService ) {
            this.localPlayerService = localPlayerService;
        }

        public void AddPlayer( TienLenPlayer player ) {

            Debug.Log($"[AddPlayer] Adding localPlayer: {player.PlayerName}");

            if ( players.Count >= 4 )
                throw new InvalidOperationException(
                    "Tiến Lên supports 4 players.");

            players.Add(player);
        }

        public void RemovePlayer( PlayerRef playerRef ) {
            Debug.Log($"[RemovePlayer] Removing localPlayer with PlayerRef: {playerRef}");

            players.RemoveAll(p => p.PlayerRef == playerRef);
        }

        public void StartGame() {
            if ( players.Count < 1 ) //TODO: Change this to 2 on real game
                throw new InvalidOperationException(
                    "Tiến Lên requires at least 2 players.");

            StartRound();
        }

        public void StartRound() {
            DealHandsAuthoritative();
            //RunRoundRoutineAsync().Forget();
        }

        public void DealHandsAuthoritative() {
            var tempdeck = deck;
            tempdeck.Shuffle();

            // Deal 13 cards to each player's data hand
            for ( int i = 0; i < 13; i++ ) {
                foreach ( var player in Players ) {
                    var drawnCard = tempdeck.DrawCard();
                    player.Hand.AddCard(drawnCard);
                }
            }
        }

        private async UniTaskVoid RunRoundRoutineAsync( CancellationToken cancellationToken = default ) {
            State = TienLenGameState.Dealing;

            deck.Shuffle();

            foreach ( TienLenPlayer player in players ) {
                player.Hand.Clear();
            }

            Dictionary<TienLenPlayer, List<Card>> playerHands = new();

            //create list of card for player hand
            foreach ( TienLenPlayer player in players ) {
                playerHands[player] = new List<Card>();
            }

            //draw cards from deck and assign to player hands

            for(int i = 0;i < 13; i++ ) {
                foreach ( TienLenPlayer player in players ) {
                    Card card = deck.DrawCard();
                    if ( card == null ) {
                        Debug.LogError($"[RunRoundRoutineAsync] deck.DrawCard() returned NULL! Deck ran out of cards at round {i + 1}.");
                        return;
                    }
                    playerHands[player].Add(card);
                }
            }

            //assign cards to player hand
            foreach ( var player in players) {
                player.Hand.AddCard(playerHands[player]);
            }



            //call rpc to all players to update their hand

            //play dealcard animation in the ui for each player


            // Waits here until all cards finish animating
            await DealCardsAsync(cancellationToken);

            State = TienLenGameState.Playing;
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCSyncPrivateHand( [RpcTarget] PlayerRef targetPlayer, NetworkCard[] cards ) {
            // This executes ONLY on the targeted player's machine
            TienLenPlayer localPlayer = localPlayerService.Player;
            if ( localPlayer == null ) return;

            List<Card> myCards = cards.Select(c => c.ToCard()).ToList();
            localPlayer.Hand.AddCard(myCards);
        }

        //[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        //private void RPCTriggerDealAnimation() {
        //    // Runs on EVERY peer (Host and all Clients) to show the visual deal
        //    DealVisualsAsync().Forget();
        //}

        //private async UniTask DealVisualsAsync( CancellationToken ct ) {
        //    int delayMs = 60;
        //    TienLenPlayer localPlayer = localPlayerService.LocalPlayer;

        //    for ( int i = 0; i < 13; i++ ) {
        //        foreach ( var player in game.Players ) {
        //            ct.ThrowIfCancellationRequested();

        //            bool isLocal = (player.PlayerRef == Runner.LocalPlayer);

        //            if ( isLocal ) {
        //                // Spawn the actual face card for the local player
        //                Card cardData = player.Hand.Cards[i];
        //                CardView cardView = cardSpawner.SpawnCard(cardData, sprites[(cardData.Suit, cardData.Rank)]);
        //                player.CardHolder.AddCard(cardView, animate: true);
        //            }
        //            else {
        //                // Spawn a card back for opponents
        //                CardView cardBackView = cardSpawner.SpawnCardBack();
        //                player.CardHolder.AddCard(cardBackView, animate: true);
        //            }

        //            await UniTask.Delay(delayMs, cancellationToken: ct);
        //        }
        //    }

        //    // Arrange hands once dealing completes
        //    if ( localPlayer?.CardHolder != null ) {
        //        localPlayer.CardHolder.SortCards();
        //        localPlayer.CardHolder.ArrangeCards(animate: true);
        //    }
        //}

        //TODO: Refactor this to handle ui,
        public async UniTask DealCardsAsync( CancellationToken cancellationToken = default ) {
            int delayMs = 80; // 0.08s

            Debug.Log($"[DealCardsAsync] Starting deal. Total players: {players?.Count ?? 0}");

            if ( deck == null ) {
                Debug.LogError("[DealCardsAsync] FAILED: 'deck' reference is NULL!");
                return;
            }

            if ( players == null || players.Count == 0 ) {
                Debug.LogError("[DealCardsAsync] FAILED: 'players' list is NULL or EMPTY!");
                return;
            }

            for ( int i = 0; i < 13; i++ ) {
                //Debug.Log($"[DealCardsAsync] --- Dealing Round {i + 1}/13 ---");

                foreach ( TienLenPlayer player in players ) {
                    if ( cancellationToken.IsCancellationRequested ) {
                        Debug.LogWarning("[DealCardsAsync] Dealing cancelled via CancellationToken.");
                        return;
                    }

                    if ( player == null ) {
                        Debug.LogError("[DealCardsAsync] An entry in 'players' list is NULL!");
                        continue;
                    }

                    Card card = deck.DrawCard();
                    if ( card == null ) {
                        Debug.LogError($"[DealCardsAsync] deck.DrawCard() returned NULL! Deck ran out of cards at round {i + 1}.");
                        return;
                    }

                    // Check Player Hand
                    if ( player.Hand == null ) {
                        Debug.LogError($"[DealCardsAsync] localPlayer.Hand is NULL for '{player.PlayerName}'!");
                    }
                    else {
                        player.Hand.AddCard(card);
                    }

                    // Check Player CardHolder
                    if ( player.CardHolder == null ) {
                        Debug.LogError($"[DealCardsAsync] FAILED: localPlayer.CardHolder is NULL for '{player.PlayerName}' (ID: {player.Id})! Check if SetCardHolder was called properly.");
                    }
                    else {
                        try {
                                
                        }
                        catch ( System.Exception ex ) {
                            Debug.LogError($"[DealCardsAsync] Exception caught inside CardHolder.AddCard for '{player.PlayerName}': {ex.Message}\n{ex.StackTrace}");
                        }
                    }

                    await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
                }
            }

            Debug.Log("[DealCardsAsync] Finished dealing 13 rounds. Starting sorting & final arrangement...");

            foreach ( TienLenPlayer player in players ) {
                if ( player == null ) continue;

                Debug.Log($"[DealCardsAsync] Arranging cardsViews for '{player.PlayerName}'");

                if ( player.CardHolder != null ) {
                    player.CardHolder.SortCards();
                    player.CardHolder.ArrangeCards(true);
                }
                else {
                    Debug.LogWarning($"[DealCardsAsync] Cannot arrange CardHolder: localPlayer.CardHolder is NULL for '{player.PlayerName}'");
                }
            }

            //int unusedCardCount = deck.CardViews?.Count ?? 0;
            //Debug.Log($"[DealCardsAsync] Disabling {unusedCardCount} unused card views from the deck.");

            //if ( deck.CardViews != null ) {
            //    foreach ( CardView unusedCardView in deck.CardViews ) {
            //        if ( unusedCardView == null ) continue;

            //        unusedCardView.SetInteractable(false);
            //        unusedCardView.gameObject.SetActive(false);
            //    }
            //}

            Debug.Log("[DealCardsAsync] DealCardsAsync completed successfully.");
        }

        public void SetDeck( Deck deck ) {
            this.deck = deck;
        }
    }
}