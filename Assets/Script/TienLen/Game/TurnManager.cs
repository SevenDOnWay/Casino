using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game.Services;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game {
    public class TurnManager : NetworkBehaviour {
        [Header("Dependencies")]
        private TienLenRuleValidator validator;
        private TienLenTurnHandler turnHandler;
        private ISeatQueryService seatQueryService;
        private IObjectResolver resolver;

        private IActionHandler ActionHandler => resolver?.Resolve<IActionHandler>();
        private TienLenGameController GameController => resolver?.Resolve<TienLenGameController>();
        private TienLenTurnHandler TurnHandler => turnHandler ??= resolver?.Resolve<TienLenTurnHandler>();

        private IReadOnlyList<TienLenPlayer> players;
        private int lastPlayIndex;
        private readonly HashSet<TienLenPlayer> passedInRound = new();

        public TienLenPlayer CurrentPlayer => players != null && players.Count > 0 && currentPlayerIndex >= 0 && currentPlayerIndex < players.Count
            ? players[currentPlayerIndex]
            : null;

        public event Action<int> OnTurnChanged;

        [Networked] public int currentPlayerIndex { get; set; }
        [Networked] int WinnerSeatNet { get; set; } = -1;
        [Networked] int lastPassSeat { get; set; } = -1;
        [Networked] int CurrentTableCardCount { get; set; } = 0;

        public TurnManager() { }

        public TurnManager( TienLenRuleValidator validator ) {
            this.validator = validator;
        }

        [Inject]
        public void Construct(
            TienLenRuleValidator validator,
            ISeatQueryService seatQueryService = null,
            IObjectResolver resolver = null ) {
            this.validator = validator;
            this.seatQueryService = seatQueryService;
            this.resolver = resolver;
        }

        private void SyncNetworkState() {
            // currentPlayerIndex is [Networked] and synced automatically across Fusion peers
        }

        public override void Spawned() {
        }

        public void Initialize( IReadOnlyList<TienLenPlayer> players ) {
            this.players = players;
            currentPlayerIndex = 0;
            lastPlayIndex = 0;
            passedInRound.Clear();
        }

        /// <summary>Override which ordered-list index starts (e.g. ♠3 holder).</summary>
        public void SetStartingPlayer( int orderIndex ) {
            if ( players == null || players.Count == 0 ) return;
            currentPlayerIndex = Mathf.Clamp(orderIndex, 0, players.Count - 1);
            lastPlayIndex = currentPlayerIndex;
            SyncNetworkState();
        }

        public void InitializeTurns( int startingSeat = -1 ) {
            if ( !Object.HasStateAuthority || seatQueryService == null ) return;

            List<TienLenPlayer> ordered = seatQueryService.GetSeatedPlayers()
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();

            if ( ordered.Count == 0 ) return;

            Initialize(ordered);

            if ( startingSeat >= 0 ) {
                var seatedPlayers = seatQueryService.GetSeatedPlayers();
                int startingIndex = -1;
                if ( seatedPlayers.TryGetValue(startingSeat, out var winnerPlayer) && winnerPlayer != null && !winnerPlayer.IsInactive ) {
                    startingIndex = ordered.IndexOf(winnerPlayer);
                }
                SetStartingPlayer(startingIndex >= 0 ? startingIndex : 0);
                if ( ActionHandler != null ) ActionHandler.IsFirstPlayOfGame = false;
                Debug.Log($"[TurnManager] Previous winner at seat {startingSeat} (order index {startingIndex}) leads next match.");
            }
            else {
                int holderIndex = TurnHandler != null ? TurnHandler.FindStartingSeatOrderIndex(ordered) : -1;
                if ( holderIndex >= 0 ) {
                    SetStartingPlayer(holderIndex);
                    if ( ActionHandler != null ) ActionHandler.IsFirstPlayOfGame = true;
                    Debug.Log($"[TurnManager] Player at order index {holderIndex} holds 3 of Spades and must lead the match.");
                }
                else {
                    SetStartingPlayer(0);
                    if ( ActionHandler != null ) ActionHandler.IsFirstPlayOfGame = false;
                    Debug.Log("[TurnManager] 3 of Spades is not held by any player. Host starts freely.");
                }
            }

            GameController?.MirrorTurnSeat(isDealingStart: true);
        }

        public bool CanPass( TienLenPlayer player ) {
            if ( player == null ) return false;
            if ( CurrentPlayer != player ) return false;
            return validator != null && validator.CurrentCombination != null;
        }

        private bool IsPlayerActive( TienLenPlayer player ) {
            if ( player == null ) return false;
            if ( player.HasWon ) return false;
            return true;
        }

        public bool CheckRoundFinished() {
            if ( players == null || players.Count <= 1 ) return true;

            int activeCount = 0;
            foreach ( var p in players ) {
                if ( p != null && IsPlayerActive(p) && !passedInRound.Contains(p) ) {
                    activeCount++;
                }
            }

            return activeCount <= 1;
        }

        public void AdvanceTurn() {
            for ( int i = 1; i <= players.Count; i++ ) {
                int nextIndex = (currentPlayerIndex + i) % players.Count;
                var candidate = players[nextIndex];

                if ( currentPlayerIndex == lastPlayIndex ) {
                    StartNewRound();
                }
                if ( candidate != null && IsPlayerActive(candidate) && !passedInRound.Contains(candidate) ) {
                    currentPlayerIndex = nextIndex;
                    SyncNetworkState();
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
                    SyncNetworkState();
                    OnTurnChanged?.Invoke(currentPlayerIndex);
                    return;
                }
            }
        }

        public void StartNewRound() {
            validator?.Reset();
            passedInRound.Clear();
        }

        public void HandleTurnTimeout() {
            var gc = GameController;
            if ( !Object.HasStateAuthority || gc == null || !gc.IsGameStarted || gc.WinnerSeat != -1 ) return;
            if ( CurrentPlayer == null || seatQueryService == null ) return;

            Debug.Log($"[TurnManager] Turn timer expired for player {CurrentPlayer.Id} (Seat {gc.CurrentTurnSeat}). Auto-acting...");
            AutoActForCurrentPlayer();
        }

        public void AutoActForCurrentPlayer() {
            var gc = GameController;
            var ah = ActionHandler;
            if ( !Object.HasStateAuthority || gc == null || !gc.IsGameStarted || gc.WinnerSeat != -1 ) return;
            if ( CurrentPlayer == null || seatQueryService == null || TurnHandler == null || ah == null ) return;

            var currentPlayer = CurrentPlayer;

            var decision = TurnHandler.DecideAutoAction(currentPlayer, ah.IsFirstPlayOfGame);
            switch ( decision.ActionType ) {
                case AutoActionResultType.Pass:
                    if ( !ah.TryExecutePass(currentPlayer, out _, out _) ) {
                        ForceAdvanceTurn();
                        gc?.MirrorTurnSeat();
                    }
                    break;

                case AutoActionResultType.PlayCards:
                    NetworkCard[] netCards = decision.CardsToPlay.Select(c => new NetworkCard(c)).ToArray();
                    if ( !ah.TryValidateAndExecutePlay(currentPlayer, netCards, out _, out _) ) {
                        ForceAdvanceTurn();
                        gc?.MirrorTurnSeat();
                    }
                    break;

                case AutoActionResultType.ForceAdvance:
                    ForceAdvanceTurn();
                    gc?.MirrorTurnSeat();
                    break;
            }

            if ( Object.HasStateAuthority && (!gc.TurnTimer.IsRunning || gc.TurnTimer.Expired(Runner)) ) {
                gc.TurnTimer = TickTimer.CreateFromSeconds(Runner, gc.TurnDuration);
            }
        }

        public bool IsPlayerTurn( TienLenPlayer player ) {
            return CurrentPlayer != null && ReferenceEquals(CurrentPlayer, player);
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

        public void AddPassPlayer( TienLenPlayer player ) {
            if ( player == null ) return;
            passedInRound.Add(player);
        }
    }
}
