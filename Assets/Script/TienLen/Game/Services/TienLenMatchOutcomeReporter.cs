using Assets.Script.Data.Models;
using Assets.Script.Data.Services;
using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.TienLen.Game.Services {
    public class TienLenMatchOutcomeReporter {
        private ISeatQueryService seatQueryService;
        private ILocalPlayerService localPlayerService;
        private IPlayerProfileService profileService;

        public TienLenMatchOutcomeReporter(
            ISeatQueryService seatQueryService = null,
            ILocalPlayerService localPlayerService = null,
            IPlayerProfileService profileService = null
        ) {
            this.seatQueryService = seatQueryService;
            this.localPlayerService = localPlayerService;
            this.profileService = profileService;
        }

        public void SetDependencies( ISeatQueryService seatQuery, ILocalPlayerService localPlayer, IPlayerProfileService profile ) {
            this.seatQueryService = seatQuery;
            this.localPlayerService = localPlayer;
            this.profileService = profile;
        }

        public MatchResultReportRequest BuildMatchReport( int winnerSeat, int currentBet ) {
            return BuildMatchReport(winnerSeat, currentBet, seatQueryService, localPlayerService);
        }

        public static MatchResultReportRequest BuildMatchReport(
            int winnerSeat,
            int currentBet,
            ISeatQueryService seatQuery,
            ILocalPlayerService localPlayer = null
        ) {
            if ( seatQuery == null ) return null;

            var seated = seatQuery.GetSeatedPlayers();
            if ( seated == null || seated.Count == 0 ) return null;

            var netMap = seatQuery.GetNetworkPlayerMap();
            int loserCount = Mathf.Max(1, seated.Count - 1);

            var summaries = new List<MatchPlayerSummary>();

            foreach ( var kvp in seated ) {
                int seatIndex = kvp.Key;
                TienLenPlayer player = kvp.Value;
                if ( player == null ) continue;

                bool isWinner = (seatIndex == winnerSeat);
                string userId = string.Empty;
                string displayName = player.PlayerName;
                int avatarId = player.AvatarId;

                if ( netMap != null && netMap.TryGetValue(seatIndex, out var netPlayer) && netPlayer != null ) {
                    userId = (string)netPlayer.UserId;
                    if ( !string.IsNullOrEmpty((string)netPlayer.PlayerName) ) {
                        displayName = (string)netPlayer.PlayerName;
                    }
                    avatarId = netPlayer.AvatarId;
                }

                if ( string.IsNullOrEmpty(userId) && localPlayer != null ) {
                    var localLogic = localPlayer.GetLocalLogicPlayer();
                    int localSeat = localLogic != null && ReferenceEquals(localLogic, player) ? seatIndex : -1;
                    if ( localSeat == seatIndex ) {
                        userId = localPlayer.GetID();
                    }
                }

                if ( string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(player.UserId) ) {
                    userId = player.UserId;
                }

                if ( string.IsNullOrEmpty(userId) ) {
                    userId = player.Id.ToString();
                }

                long moneyEarned = isWinner ? (loserCount * currentBet) : -currentBet;
                int expEarned = isWinner ? 50 : 15;
                int rank = isWinner ? 1 : 2;

                summaries.Add(new MatchPlayerSummary {
                    userId = userId,
                    displayName = displayName,
                    avatarId = avatarId,
                    rank = rank,
                    moneyEarned = moneyEarned,
                    expEarned = expEarned
                });
            }

            return new MatchResultReportRequest {
                matchId = Guid.NewGuid().ToString("N"),
                results = summaries
            };
        }

        public async UniTask<bool> ReportMatchOutcomeAsync( int winnerSeat, int currentBet ) {
            if ( profileService == null ) return false;

            var report = BuildMatchReport(winnerSeat, currentBet);
            if ( report == null || report.results == null || report.results.Count == 0 ) return false;

            Debug.Log($"[TienLenMatchOutcomeReporter] Reporting match outcome for {report.results.Count} players. BetStake: {currentBet}");
            bool success = await profileService.RecordMatchResultAsync(report);
            if ( success ) {
                Debug.Log("[TienLenMatchOutcomeReporter] Match outcome recorded successfully in backend.");
            }
            else {
                Debug.LogWarning("[TienLenMatchOutcomeReporter] Failed to record match outcome in backend.");
            }
            return success;
        }
    }
}
