using Assets.Script.TienLen;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Game.Services;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Fusion;
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Assets.Script.NetWorkScript {
    public class TienLenNetWorkPlayer : NetworkBehaviour {
        [Header("Dependencies")]
        private TienLenGameController controller;
        private LocalPlayerService localPlayerService;
        private IActionHandler actionHandler;
        private ISeatQueryService seatQueryService;

        public event Action<TienLenNetWorkPlayer> OnPlayerDataUpdated;

        [Networked] public PlayerRef PlayerRef { get; set; }
        [Networked, OnChangedRender(nameof(OnNetworkPlayerDataChanged))] public NetworkString<_32> UserId { get; set; }
        [Networked, OnChangedRender(nameof(OnNetworkPlayerDataChanged))] public NetworkString<_16> PlayerName { get; set; }
        [Networked, OnChangedRender(nameof(OnNetworkPlayerDataChanged))] public int AvatarId { get; set; }
        [Networked, OnChangedRender(nameof(OnNetworkPlayerDataChanged))] public int Level { get; set; }
        [Networked, OnChangedRender(nameof(OnNetworkPlayerDataChanged))] public long Money { get; set; }

        private void OnNetworkPlayerDataChanged() {
            OnPlayerDataUpdated?.Invoke(this);
        }

        [Inject]
        public void Construct(
            LocalPlayerService localPlayerService,
            TienLenGameController controller,
            IActionHandler actionHandler = null,
            ISeatQueryService seatQueryService = null ) {
            this.localPlayerService = localPlayerService;
            this.controller = controller;
            this.actionHandler = actionHandler;
            this.seatQueryService = seatQueryService;
        }

        public override void Spawned() {
            if ( controller == null || localPlayerService == null || actionHandler == null || seatQueryService == null ) {
                var scope = LifetimeScope.Find<LifetimeScope>(gameObject.scene);
                if ( scope != null && scope.Container != null ) {
                    scope.Container.InjectGameObject(gameObject);
                }
                else {
                    Debug.LogError($"[TienLenNetWorkPlayer] Dependencies not injected on '{name}'. Ensure DynamicNetworkObjectProvider is configured on the NetworkRunner.");
                }
            }

            if ( Object.HasInputAuthority ) {
                RegisterSelfWithServices();
            }
        }

        private void RegisterSelfWithServices() {
            if ( localPlayerService == null ) {
                Debug.LogError("[TienLenNetWorkPlayer] localPlayerService is null on Spawned().");
                return;
            }

            localPlayerService.SetLocalNetworkPlayer(this);

            var localProfile = localPlayerService.GetLocalLogicPlayer();
            if ( localProfile != null ) {
                RPCSetPlayerData(
                    localProfile.UserId ?? string.Empty,
                    localProfile.PlayerName ?? string.Empty,
                    localProfile.AvatarId,
                    localProfile.Level,
                    localProfile.Money
                );
            }
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCSetPlayerData( string userId, string name, int avatarId, int level, long money ) {
            UserId = userId;
            PlayerName = name;
            AvatarId = avatarId;
            Level = level;
            Money = money;
            OnPlayerDataUpdated?.Invoke(this);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCSendPlayCard( NetworkCard[] cards, RpcInfo info = default ) {
            if ( cards == null || cards.Length == 0 ) return;

            PlayerRef sender = (info.Source != PlayerRef.None) ? info.Source : Object.InputAuthority;
            if ( sender == PlayerRef.None ) sender = PlayerRef;

            TienLenPlayer logicPlayer = seatQueryService?.GetLogicPlayer(sender) ?? localPlayerService?.GetLocalLogicPlayer();
            if ( logicPlayer == null || actionHandler == null ) return;

            actionHandler.TryValidateAndExecutePlay(logicPlayer, cards, out _, out _);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCRequestPass( RpcInfo info = default ) {
            PlayerRef sender = (info.Source != PlayerRef.None) ? info.Source : Object.InputAuthority;
            if ( sender == PlayerRef.None ) sender = PlayerRef;

            TienLenPlayer logicPlayer = seatQueryService?.GetLogicPlayer(sender) ?? localPlayerService?.GetLocalLogicPlayer();
            if ( logicPlayer == null || actionHandler == null ) return;

            actionHandler.TryExecutePass(logicPlayer, out _, out _);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCReportHandState( NetworkCard[] cards, RpcInfo info = default ) {
            PlayerRef sender = (info.Source != PlayerRef.None) ? info.Source : Object.InputAuthority;
            if ( sender == PlayerRef.None ) {
                sender = PlayerRef;
            }

            if ( controller == null ) {
                Debug.LogError("[TienLenNetWorkPlayer.RPCReportHandState] Controller is null.");
                return;
            }

            controller.HandleReportedHand(sender, cards);
        }
    }
}
