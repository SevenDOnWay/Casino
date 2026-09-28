using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Fusion;
using UnityEngine;

namespace Assets.Script.TienLen.UI.Strategy {
    /// <summary>
    /// Everything a <see cref="IUiStrategy"/> is allowed to touch, resolved
    /// once by UiManager. Keeps strategies free of serialized-field lookups
    /// and of UiManager itself.
    /// </summary>
    public sealed class UiStrategyContext {
        public IUiStrategyHost Host { get; }
        public TienLenGameController GameController { get; }
        public SessionDisplayUI SessionDisplayUI { get; }
        public StartGameUI StartGameUI { get; }
        public ActionPanel ActionPanel { get; }
        public TableVisualLayoutManager TableLayoutManager { get; }

        public UiStrategyContext(
            IUiStrategyHost host,
            TienLenGameController gameController,
            SessionDisplayUI sessionDisplayUI,
            StartGameUI startGameUI,
            ActionPanel actionPanel,
            TableVisualLayoutManager tableLayoutManager ) {
            Host = host;
            GameController = gameController;
            SessionDisplayUI = sessionDisplayUI;
            StartGameUI = startGameUI;
            ActionPanel = actionPanel;
            TableLayoutManager = tableLayoutManager;
        }

        /// <summary>
        /// True once the host has started the match. Read from the replicated
        /// flag so late joiners boot straight into the in-game presentation.
        /// Guarded because the flag cannot be read before the object spawns.
        /// </summary>
        public bool IsGameRunning {
            get {
                if ( GameController == null ) return false;

                NetworkObject networkObject = GameController.Object;
                if ( networkObject == null || !networkObject.IsValid ) return false;

                return GameController.IsGameStarted;
            }
        }

        /// <summary>
        /// Shows/hides a panel without caring whether it was wired as a
        /// GameObject or as a component in the inspector.
        /// </summary>
        public void SetVisible( Object target, bool visible ) {
            GameObject targetObject = AsGameObject( target );
            if ( targetObject == null ) return;

            if ( targetObject.activeSelf != visible ) {
                targetObject.SetActive( visible );
            }
        }

        public static GameObject AsGameObject( Object target ) {
            return target switch {
                GameObject go => go,
                Component component => component != null ? component.gameObject : null,
                _ => null
            };
        }
    }
}
