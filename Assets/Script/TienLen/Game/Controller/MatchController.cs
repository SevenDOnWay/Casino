using Assets.Script.Data.Models;
using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Game.Services;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using Cysharp.Threading.Tasks;
using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game.Controller {
    public class MatchController : NetworkBehaviour {
        [Header("Configuration")]
        [SerializeField] private int minPlayersToStart = 2;
        [SerializeField] private float celebrationDuration = 3.5f;

        [Header("Injected Dependencies")]
        private ISeatQueryService seatQueryService;
        private TurnManager turnManager;
        private TienLenGameController gameController;
        private TienLenMatchOutcomeReporter outcomeReporter;
        private IAnimationPlayer animationPlayer;
        private SessionController sessionController;

        public int MinPlayersToStart => minPlayersToStart;

        public int RegisteredPlayerCount =>
            seatQueryService != null ? seatQueryService.GetNetworkPlayer().Count : 0;

        public bool HasEnoughPlayersToPlay =>
            Object != null && Object.IsValid && seatQueryService != null && RegisteredPlayerCount >= minPlayersToStart;

        public bool CanStartGame() {
            if ( !HasEnoughPlayersToPlay ) return false;
            if ( gameController != null && gameController.IsGameStarted ) return false;
            return true;
        }

        [Inject]
        public void Construct(
            ISeatQueryService seatQueryService,
            TurnManager turnManager,
            TienLenGameController gameController,
            TienLenMatchOutcomeReporter outcomeReporter,
            IAnimationPlayer animationPlayer = null,
            SessionController sessionController = null ) {
            this.seatQueryService = seatQueryService;
            this.turnManager = turnManager;
            this.gameController = gameController;
            this.outcomeReporter = outcomeReporter;
            this.animationPlayer = animationPlayer;
            this.sessionController = sessionController;
        }

        #region Match Start

        public void StartGame() {
            if ( !Object.HasStateAuthority || !CanStartGame() || (gameController != null && gameController.IsGameStarted) ) return;

            var seatedPlayers = seatQueryService.GetSeatedPlayers();
            int bet = gameController != null ? gameController.CurrentBetStake : 5;

            foreach ( var p in seatedPlayers.Values ) {
                if ( p != null && p.Money < bet ) {
                    p.IsInactive = true;
                    Debug.LogWarning($"[MatchController] Player {p.Id} has insufficient funds ({p.Money} < {bet}). Marked inactive.");
                }
            }

            CleanUpInactivePlayers();

            // Re-evaluate solvent seated players after excluding underfunded seats
            seatedPlayers = seatQueryService.GetSeatedPlayers();
            int solventCount = seatedPlayers.Values.Count(p => p != null && !p.IsInactive && p.Money >= bet);
            if ( solventCount < minPlayersToStart ) {
                Debug.LogWarning("[MatchController] Not enough solvent players to start match.");
                return;
            }

            if ( gameController != null ) {
                gameController.IsGameStarted = true;
                gameController.WinnerSeat = -1;
                gameController.LastPassSeat = -1;
                gameController.IsFirstPlayOfGame = true;
            }

            CreateAndDealCards();

            // Delegate turn setup entirely to TurnManager
            turnManager?.InitializeTurns();
        }

        public void CreateAndDealCards() {
            if ( seatQueryService == null ) return;

            Deck deck = new Deck();
            deck.CreateDeck();
            deck.Shuffle();

            var seatedPlayers = seatQueryService.GetSeatedPlayers();
            var activePlayers = seatedPlayers.Values.Where(p => p != null && !p.IsInactive).ToList();
            foreach ( var p in activePlayers ) {
                p.Hand?.Clear();
            }

            for ( int i = 0; i < 13; i++ ) {
                foreach ( var player in activePlayers ) {
                    var drawnCard = deck.DrawCard();
                    player.Hand.AddCard(drawnCard);
                }
            }

            gameController?.BroadcastHands();
            animationPlayer?.PlayDealAnimation();
        }

        #endregion

        #region Match End & Outcomes

        public void EndGame( int winnerSeat ) {
            if ( !Object.HasStateAuthority || gameController == null ) return;

            gameController.WinnerSeat = winnerSeat;
            Debug.Log($"[MatchController] Player at seat {winnerSeat} wins!");

            ReportMatchOutcome(winnerSeat).Forget();
            WaitAndStartNextGame(winnerSeat).Forget();
        }

        private async UniTaskVoid ReportMatchOutcome( int winnerSeat ) {
            if ( !Object.HasStateAuthority || outcomeReporter == null || gameController == null ) return;

            bool success = await outcomeReporter.ReportMatchOutcomeAsync(winnerSeat, gameController.CurrentBetStake);
            if ( success ) {
                gameController.RPCNotifyMatchReported();
            }
        }

        private async UniTaskVoid WaitAndStartNextGame( int previousWinnerSeat ) {
            var ct = this.GetCancellationTokenOnDestroy();

            try {
                await UniTask.Delay(TimeSpan.FromSeconds(celebrationDuration), cancellationToken: ct);
            }
            catch ( OperationCanceledException ) {
                return;
            }

            if ( !Object.HasStateAuthority ) return;

            int bet = gameController != null ? gameController.CurrentBetStake : 5;
            foreach ( var p in seatQueryService.GetSeatedPlayers().Values ) {
                if ( p != null && p.Money < bet ) {
                    p.IsInactive = true;
                    Debug.LogWarning($"[MatchController] Player {p.Id} has insufficient funds for next match ({p.Money} < {bet}). Marked inactive.");
                }
            }

            CleanUpInactivePlayers();

            if ( !HasEnoughPlayersToPlay ) {
                Debug.LogWarning("[MatchController] Not enough players to continue. Returning to lobby.");
                ResetToLobby();
                return;
            }

            StartNextMatch(previousWinnerSeat);
        }

        private void ResetToLobby() {
            if ( gameController == null ) return;

            gameController.IsGameStarted = false;
            gameController.WinnerSeat = -1;
            gameController.CurrentTurnSeat = -1;
            gameController.CurrentTableCardCount = 0;
            animationPlayer?.ClearTableCenter();
        }

        #endregion

        #region Next Match & Cleanup

        private void StartNextMatch( int previousWinnerSeat ) {
            if ( !Object.HasStateAuthority || seatQueryService == null || gameController == null ) return;

            Debug.Log("[MatchController] Starting next game match...");

            gameController.CurrentTableCardCount = 0;
            gameController.LastPassSeat = -1;
            gameController.WinnerSeat = -1;
            animationPlayer?.ClearTableCenter();

            CreateAndDealCards();

            // Delegate round initialization to TurnManager starting with the previous winner
            turnManager?.InitializeTurns(previousWinnerSeat);
        }

        public void CleanUpInactivePlayers() {
            if ( seatQueryService == null || !Object.HasStateAuthority ) return;

            var seated = seatQueryService.GetSeatedPlayers();
            var netMap = seatQueryService.GetNetworkPlayerMap();
            var toRemove = new List<PlayerRef>();

            foreach ( var kvp in seated ) {
                if ( kvp.Value != null && kvp.Value.IsInactive ) {
                    if ( netMap != null && netMap.TryGetValue(kvp.Key, out var netPlayer) && netPlayer != null ) {
                        toRemove.Add(netPlayer.PlayerRef);
                    }
                }
            }

            var session = sessionController ?? FindAnyObjectByType<SessionController>();
            foreach ( var pRef in toRemove ) {
                session?.RemovePlayer(pRef);
            }
        }

        #endregion
    }
}
