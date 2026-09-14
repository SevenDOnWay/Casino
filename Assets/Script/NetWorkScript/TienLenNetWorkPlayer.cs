using Assets.Script.TienLen;
using Assets.Script.TienLen.Game;
using Fusion;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VContainer;

namespace Assets.Script.NetWorkScript {
    public class TienLenNetWorkPlayer : NetworkBehaviour {

        [Networked] public PlayerRef PlayerRef { get; set; }
        [Networked] public int PlayerId { get; set; }
        [Networked] public NetworkString<_16> PlayerName { get; set; }

        public TienLenGameController Controller { get; set; }

        public override void Spawned() {
            Debug.Log($"[TienLenNetWorkPlayer] Spawned - PlayerId: {PlayerId}, PlayerName: {PlayerName}, PlayerRef: {PlayerRef}");

            TienLenGameController controller = FindFirstObjectByType<TienLenGameController>();
            if ( controller != null ) {
                controller.RegisterPlayer(this);
            }
        }


        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCRequestPlayCard( NetworkCard[] cards, RpcInfo info = default ) {
            PlayerRef sender = (info.Source != PlayerRef.None) ? info.Source : Object.InputAuthority;

            // Fallback if InputAuthority wasn't assigned:
            if ( sender == PlayerRef.None ) {
                sender = PlayerRef;
            }

            Debug.Log(
                $"[TienLenNetWorkPlayer.RPCRequestPlayCard] info.Source={info.Source}, " +
                $"InputAuthority={Object.InputAuthority}, resolvedSender={sender}, cardCount={cards?.Length ?? 0}"
            );

            if ( Controller == null ) {
                Debug.LogError("[TienLenNetWorkPlayer.RPCRequestPlayCard] Controller is null.");
                return;
            }

            Controller.HandlePlayRequest(sender, cards);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPCRequestPass(RpcInfo info = default){
            PlayerRef sender = (info.Source != PlayerRef.None) ? info.Source : Object.InputAuthority;

            // Fallback if InputAuthority wasn't assigned:
            if ( sender == PlayerRef.None ) {
                sender = PlayerRef;
            }

            Debug.Log(
                $"[TienLenNetWorkPlayer.RPCRequestPass] info.Source={info.Source}, " +
                $"InputAuthority={Object.InputAuthority}, resolvedSender={sender}"
            );

            if ( Controller == null ) {
                Debug.LogError("[TienLenNetWorkPlayer.RPCRequestPass] Controller is null.");
                return;
            }

            Controller.HandlePassRequest(sender);

        }

    }
}