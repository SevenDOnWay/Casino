using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.UI;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Assets.Script.TienLen.Game {
    /// <summary>
    /// Authoritative lobby controller. Handles player joins, leaves,
    /// network player object spawning, seat registration, and player kicking/exit lifecycle.
    /// </summary>
    public class SessionController : NetworkBehaviour, INetworkRunnerCallbacks {
        [Header("Scene Dependencies (Serialized / Injected)")]
        private SeatManager seatManager;
        private TienLenGameController gameController;

        private IPlayerRegisterService playerRegisterService;
        private ISeatQueryService seatQueryService;
        private LocalPlayerService localPlayerService;
        private UiManager uiManager;

        [Inject]
        public void Construct(
            SeatManager seatManager,
            TienLenGameController gameController,
            IPlayerRegisterService playerRegisterService = null,
            ISeatQueryService seatQueryService = null,
            LocalPlayerService localPlayerService = null,
            UiManager uiManager = null ) {
            this.seatManager = seatManager;
            this.gameController = gameController;
            this.playerRegisterService = playerRegisterService ?? seatManager;
            this.seatQueryService = seatQueryService ?? seatManager;
            this.localPlayerService = localPlayerService;
            this.uiManager = uiManager;
        }

        public IPlayerRegisterService RegisterService => playerRegisterService ?? seatManager;
        public ISeatQueryService SeatQuery => seatQueryService ?? seatManager;
        public TienLenGameController GameController => gameController;

        private UiManager UiMgr => uiManager != null ? uiManager : (uiManager = FindAnyObjectByType<UiManager>());

        [Header("Prefab")]
        [SerializeField] private GameObject networkPlayerPrefab;

        public override void Spawned() {
            Runner.AddCallbacks(this);

            RestoreQueuedDisconnects();

            if ( Object.HasStateAuthority && Runner != null && Runner.LocalPlayer.IsValid ) {
                if ( CheckPlayerCanJoin(Runner.LocalPlayer) ) {
                    OnPlayerJoined(Runner, Runner.LocalPlayer);
                }
            }
        }

        public override void Despawned( NetworkRunner runner, bool hasState ) {
            if ( runner != null ) {
                runner.RemoveCallbacks(this);
            }
        }

        private void OnDestroy() {
            if ( Runner != null ) {
                Runner.RemoveCallbacks(this);
            }
        }

        public void OnPlayerJoined( NetworkRunner runner, PlayerRef player ) {
            if ( !Object.HasStateAuthority ) return;

            Debug.Log($"[Lobby] Player joined: {player.PlayerId}");

            if ( runner.IsResume ) {
                if ( SeatQuery != null && SeatQuery.TryGetSeat(player, out _) ) {
                    Debug.Log($"[Lobby] Player {player.PlayerId} already has a restored seat from snapshot.");
                    return;
                }

                if ( SeatQuery != null ) {
                    var networkPlayerMap = SeatQuery.GetNetworkPlayerMap();
                    foreach ( var kvp in networkPlayerMap ) {
                        if ( kvp.Value != null && kvp.Value.PlayerRef == player ) {
                            Debug.Log($"[Lobby] Found matching network player for {player.PlayerId} at seat {kvp.Key}.");
                            return;
                        }
                    }
                }
            }

            if ( !CheckPlayerCanJoin(player) ) return;

            TienLenNetWorkPlayer networkPlayer = SpawnNetworkPlayer(player);
            if ( networkPlayer != null ) {
                RegisterService?.RegisterPlayer(networkPlayer);
                Debug.Log($"[Lobby] Spawned and registered network player '{networkPlayer.PlayerName}' for {player.PlayerId}");
            }
        }

        public void OnPlayerLeft( NetworkRunner runner, PlayerRef player ) {
            queuedDisconnectPlayers.Remove(player);
            if ( Object != null && Object.IsValid && Object.HasStateAuthority ) {
                if ( QueuedDisconnectsNet.ContainsKey(player) ) {
                    QueuedDisconnectsNet.Remove(player);
                }
            }
            if ( !Object.HasStateAuthority ) return;

            if ( GameController != null && GameController.IsGameStarted && GameController.WinnerSeat == -1 ) {
                // Mid-game: retain player in inactive state so match completes honestly
                GameController.HandlePlayerLeftMidGame(player);
            }
            else {
                RemovePlayer(player);
            }
        }

        private TienLenNetWorkPlayer SpawnNetworkPlayer( PlayerRef player ) {
            TienLenNetWorkPlayer networkPlayer = null;

            var networkPlayerObject = Runner.Spawn(
                networkPlayerPrefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: player,
                onBeforeSpawned: ( runner, obj ) => {
                    networkPlayer = obj.GetComponent<TienLenNetWorkPlayer>();
                    if ( networkPlayer != null ) {
                        networkPlayer.PlayerRef = player;
                        if ( player == runner.LocalPlayer && localPlayerService != null ) {
                            networkPlayer.UserId = localPlayerService.GetID() ?? string.Empty;
                            networkPlayer.PlayerName = localPlayerService.GetName() ?? $"Player {player.PlayerId}";
                            networkPlayer.AvatarId = localPlayerService.GetAvatarId();
                            networkPlayer.Level = localPlayerService.GetLevel();
                            long money = localPlayerService.GetMoney();
                            networkPlayer.Money = money > 0 ? money : 10000;
                        }
                        else {
                            networkPlayer.PlayerName = $"Player {player.PlayerId}";
                            networkPlayer.Money = 10000;
                        }
                    }
                }
            );

            if ( networkPlayer == null && networkPlayerObject != null ) {
                networkPlayer = networkPlayerObject.GetComponent<TienLenNetWorkPlayer>();
                if ( networkPlayer != null ) {
                    networkPlayer.PlayerRef = player;
                    if ( player == Runner.LocalPlayer && localPlayerService != null ) {
                        networkPlayer.UserId = localPlayerService.GetID() ?? string.Empty;
                        networkPlayer.PlayerName = localPlayerService.GetName() ?? $"Player {player.PlayerId}";
                        networkPlayer.AvatarId = localPlayerService.GetAvatarId();
                        networkPlayer.Level = localPlayerService.GetLevel();
                        long money = localPlayerService.GetMoney();
                        networkPlayer.Money = money > 0 ? money : 10000;
                    }
                    else {
                        networkPlayer.PlayerName = $"Player {player.PlayerId}";
                        networkPlayer.Money = 10000;
                    }
                }
            }

            return networkPlayer;
        }

        private bool CheckPlayerCanJoin( PlayerRef player ) {
            if ( SeatQuery != null && SeatQuery.TryGetSeat(player, out _) ) {
                Debug.LogWarning($"[Lobby] Player {player.PlayerId} already has a registered seat. Skipping spawn.");
                return false;
            }

            return true;
        }

        public void RemovePlayer( PlayerRef player ) {
            if ( !Object.HasStateAuthority ) return;
            if ( Runner == null ) return;

            Debug.Log($"[Lobby] Player left: {player.PlayerId}");

            NetworkObject netObj = null;
            if ( SeatQuery != null ) {
                foreach ( var kvp in SeatQuery.GetNetworkPlayerMap() ) {
                    if ( kvp.Value != null && kvp.Value.PlayerRef == player ) {
                        netObj = kvp.Value.Object;
                        break;
                    }
                }
            }

            RegisterService?.UnregisterPlayer(player);

            if ( netObj != null ) {
                Runner.Despawn(netObj);
                Debug.Log($"[Lobby] Despawned network player for {player.PlayerId}");
            }
        }

        #region Disconnect Queue & Kicking

        [Networked, Capacity(4), OnChangedRender(nameof(OnQueuedDisconnectsChanged))]
        public NetworkDictionary<PlayerRef, NetworkBool> QueuedDisconnectsNet => default;

        private void OnQueuedDisconnectsChanged() {
            RestoreQueuedDisconnects();
        }

        private readonly HashSet<PlayerRef> queuedDisconnectPlayers = new();

        public bool IsLocalDisconnectQueued { get; private set; }

        public bool HasQueuedDisconnects =>
            queuedDisconnectPlayers.Count > 0 ||
            IsLocalDisconnectQueued ||
            (Object != null && Object.IsValid && QueuedDisconnectsNet.Count > 0);

        public bool IsPlayerDisconnectQueued( PlayerRef player ) {
            if ( Runner != null && Runner.LocalPlayer == player && IsLocalDisconnectQueued ) return true;
            if ( queuedDisconnectPlayers.Contains(player) ) return true;
            if ( Object != null && Object.IsValid && QueuedDisconnectsNet.ContainsKey(player) ) return true;
            return false;
        }

        public IReadOnlyCollection<PlayerRef> GetQueuedDisconnectPlayers() {
            if ( Object != null && Object.IsValid ) {
                foreach ( var kvp in QueuedDisconnectsNet ) {
                    if ( kvp.Value ) queuedDisconnectPlayers.Add(kvp.Key);
                }
            }
            return queuedDisconnectPlayers;
        }

        public void RestoreQueuedDisconnects() {
            if ( Object != null && Object.IsValid ) {
                queuedDisconnectPlayers.Clear();
                foreach ( var kvp in QueuedDisconnectsNet ) {
                    if ( kvp.Value ) {
                        queuedDisconnectPlayers.Add(kvp.Key);
                    }
                }

                if ( Runner != null && Runner.LocalPlayer.IsValid ) {
                    IsLocalDisconnectQueued = queuedDisconnectPlayers.Contains(Runner.LocalPlayer);
                }
            }
        }

        public void RequestLeaveToLobby() {
            bool isGameInProgress = GameController != null && GameController.IsGameStarted && GameController.WinnerSeat == -1;
            if ( !isGameInProgress ) {
                LeaveLocalToLobby();
            }
            else {
                if ( Runner != null && Runner.LocalPlayer.IsValid ) {
                    QueuePlayerDisconnect(Runner.LocalPlayer);
                }
                UiMgr?.ShowAnnouncement("Left at the end of game");
            }
        }

        public void QueuePlayerDisconnect( PlayerRef player ) {
            if ( Runner != null && Runner.LocalPlayer == player ) {
                IsLocalDisconnectQueued = true;
            }

            if ( Object != null && Object.IsValid ) {
                if ( Object.HasStateAuthority ) {
                    queuedDisconnectPlayers.Add(player);
                    if ( !QueuedDisconnectsNet.ContainsKey(player) ) {
                        QueuedDisconnectsNet.Add(player, true);
                    }
                }
                else {
                    RPCQueuePlayerDisconnect(player);
                }
            }
            else {
                queuedDisconnectPlayers.Add(player);
            }
        }

        [Rpc(sources: RpcSources.All, targets: RpcTargets.StateAuthority)]
        public void RPCQueuePlayerDisconnect( PlayerRef player, RpcInfo info = default ) {
            PlayerRef sender = (info.Source != PlayerRef.None) ? info.Source : player;
            if ( info.Source != PlayerRef.None && info.Source != player ) {
                Debug.LogWarning($"[SessionController] Unauthorized disconnect request: client {info.Source.PlayerId} attempted to queue player {player.PlayerId}. Rejected.");
                return;
            }

            Debug.Log($"[SessionController] Player {sender.PlayerId} queued disconnect at end of game.");
            queuedDisconnectPlayers.Add(sender);

            if ( Object != null && Object.IsValid && Object.HasStateAuthority ) {
                if ( !QueuedDisconnectsNet.ContainsKey(sender) ) {
                    QueuedDisconnectsNet.Add(sender, true);
                }
            }
        }

        public void KickQueuedPlayers() {
            if ( Object == null || !Object.HasStateAuthority ) return;

            RestoreQueuedDisconnects();

            if ( queuedDisconnectPlayers.Count > 0 ) {
                var toKick = new List<PlayerRef>(queuedDisconnectPlayers);
                queuedDisconnectPlayers.Clear();
                if ( Object.IsValid ) {
                    QueuedDisconnectsNet.Clear();
                }

                foreach ( var pRef in toKick ) {
                    if ( Runner != null && pRef == Runner.LocalPlayer ) continue;
                    Debug.Log($"[SessionController] Kicking queued player {pRef.PlayerId}.");
                    RPCKickPlayer(pRef);
                    RemovePlayer(pRef);
                }
            }

            if ( IsLocalDisconnectQueued ) {
                Debug.Log("[SessionController] Local host queued disconnect. Leaving to lobby.");
                LeaveLocalToLobby();
            }
        }

        public void KickPlayer( PlayerRef player ) {
            if ( Runner != null && player == Runner.LocalPlayer ) {
                LeaveLocalToLobby();
                return;
            }

            if ( Object == null || !Object.HasStateAuthority ) return;

            queuedDisconnectPlayers.Remove(player);
            if ( Object.IsValid && QueuedDisconnectsNet.ContainsKey(player) ) {
                QueuedDisconnectsNet.Remove(player);
            }
            RPCKickPlayer(player);
            RemovePlayer(player);
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        public void RPCKickPlayer( PlayerRef targetPlayer ) {
            if ( Runner != null && Runner.LocalPlayer == targetPlayer ) {
                Debug.Log($"[SessionController] Received kick instruction for local player.");
                LeaveLocalToLobby();
            }
        }

        public void LeaveLocalToLobby() {
            IsLocalDisconnectQueued = false;
            queuedDisconnectPlayers.Clear();
            if ( Object != null && Object.IsValid && Object.HasStateAuthority ) {
                QueuedDisconnectsNet.Clear();
            }

            if ( Runner != null && Runner.IsRunning ) {
                Runner.Shutdown();
            }
            if ( Application.isPlaying ) {
                SceneManager.LoadScene("MainMenuScene");
            }
        }

        #endregion

        #region INetworkRunnerCallbacks

        public void OnObjectExitAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }
        public void OnObjectEnterAOI( NetworkRunner runner, NetworkObject obj, PlayerRef player ) { }
        public void OnShutdown( NetworkRunner runner, ShutdownReason shutdownReason ) { }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer( NetworkRunner runner, NetDisconnectReason reason ) { }
        public void OnConnectRequest( NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token ) { }
        public void OnConnectFailed( NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason ) { }
        public void OnReliableDataReceived( NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data ) { }
        public void OnReliableDataProgress( NetworkRunner runner, PlayerRef player, ReliableKey key, float progress ) { }
        public void OnInput( NetworkRunner runner, NetworkInput input ) { }
        public void OnInputMissing( NetworkRunner runner, PlayerRef player, NetworkInput input ) { }
        void INetworkRunnerCallbacks.OnConnectedToServer( NetworkRunner runner ) { }
        public void OnSessionListUpdated( NetworkRunner runner, List<SessionInfo> sessionList ) { }
        public void OnCustomAuthenticationResponse( NetworkRunner runner, Dictionary<string, object> data ) { }
        public void OnHostMigration( NetworkRunner runner, HostMigrationToken hostMigrationToken ) { }
        public void OnSceneLoadDone( NetworkRunner runner ) { }
        public void OnSceneLoadStart( NetworkRunner runner ) { }

        #endregion
    }
}
