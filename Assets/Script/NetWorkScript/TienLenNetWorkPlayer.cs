using Assets.Script.TienLen;
using Assets.Script.TienLen.Game;
using Fusion;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Assets.Script.NetWorkScript {
    public class TienLenNetWorkPlayer : NetworkBehaviour {
        [Header("Dependencies")]
        private TienLenGameController controller;
        private ILocalPlayerService localPlayerService;

        [Networked] public PlayerRef PlayerRef { get; set; }
        [Networked] public NetworkString<_16> PlayerName { get; set; }
        [Networked] public int AvatarId { get; set; }
        [Networked] public int Level { get; set; }

        [Inject]
        public void Construct( ILocalPlayerService localPlayerService, TienLenGameController controller ) {
            this.localPlayerService = localPlayerService;
            this.controller = controller;
        }

        public override void Spawned() {
            if ( controller == null || localPlayerService == null ) {
                var scope = LifetimeScope.Find<LifetimeScope>();
                if ( scope != null ) {
                    scope.Container.Inject(this);
                }
            }

            if ( Object.HasInputAuthority && localPlayerService != null ) {
                localPlayerService.SetLocalNetworkPlayer(this);

                string name = localPlayerService.GetName() ?? "Player";
                int avatar = localPlayerService.GetAvatarId();
                int lvl = localPlayerService.GetLevel();

                if ( Object.HasStateAuthority ) {
                    PlayerName = name;
                    AvatarId = avatar;
                    Level = lvl;
                }
                else {
                    RPCSetPlayerData(name, avatar, lvl);
                }

                Debug.Log($"[TienLenNetWorkPlayer] Bound local player '{PlayerName}' ({PlayerRef.PlayerId}) Avatar={AvatarId} Level={Level}.");
            }
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCSetPlayerData( string name, int avatarId, int level ) {
            PlayerName = name;
            AvatarId = avatarId;
            Level = level;
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCRequestPlayCard( NetworkCard[] cards, RpcInfo info = default ) {
            PlayerRef sender = (info.Source != PlayerRef.None) ? info.Source : Object.InputAuthority;

            if ( sender == PlayerRef.None ) {
                sender = PlayerRef;
            }

            Debug.Log(
                $"[TienLenNetWorkPlayer.RPCRequestPlayCard] info.Source={info.Source}, " +
                $"InputAuthority={Object.InputAuthority}, resolvedSender={sender}, cardCount={cards?.Length ?? 0}"
            );

            if ( controller == null ) {
                Debug.LogError("[TienLenNetWorkPlayer.RPCRequestPlayCard] Controller is null.");
                return;
            }

            controller.HandlePlayRequest(sender, cards);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCRequestPass( RpcInfo info = default ) {
            PlayerRef sender = (info.Source != PlayerRef.None) ? info.Source : Object.InputAuthority;

            if ( sender == PlayerRef.None ) {
                sender = PlayerRef;
            }

            Debug.Log(
                $"[TienLenNetWorkPlayer.RPCRequestPass] info.Source={info.Source}, " +
                $"InputAuthority={Object.InputAuthority}, resolvedSender={sender}"
            );

            if ( controller == null ) {
                Debug.LogError("[TienLenNetWorkPlayer.RPCRequestPass] Controller is null.");
                return;
            }

            controller.HandlePassRequest(sender);
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
