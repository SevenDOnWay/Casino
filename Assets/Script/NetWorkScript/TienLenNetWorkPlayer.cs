using Assets.Script.TienLen;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.UI;
using Fusion;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VContainer;

namespace Assets.Script.NetWorkScript {
    public class TienLenNetWorkPlayer : NetworkBehaviour {
        [Header("Dependencies")]
        private TienLenGameController Controller { get; set; }
        private TableVisualLayoutManager TableVisualLayoutManager { get; set; }

        [Networked] public PlayerRef PlayerRef { get; set; }
        [Networked] public NetworkString<_16> PlayerName { get; set; }

        public override void Spawned() {
            Initialize();
        }


        //TODO: Consider using dependency injection to ensure these are always set, rather than relying on FindFirstObjectByType.
        //lazy for now, but this could lead to null reference exceptions if the objects aren't present in the scene.
        private void Initialize() {
            Controller = Controller ?? FindFirstObjectByType<TienLenGameController>();

            if ( Controller == null ) {
                Debug.LogError("[TienLenNetWorkPlayer.Initialize] TienLenGameController not found in scene.");
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
        public void RPCRequestPass( RpcInfo info = default ) {
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