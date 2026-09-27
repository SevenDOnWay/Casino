using UnityEngine;

namespace Assets.Script.TienLen.UI.Strategy {
    /// <summary>
    /// In-match presentation: the lobby chrome (session info + start button)
    /// is hidden and the action panel owns the bottom of the screen.
    /// </summary>
    public sealed class InGameUiStrategy : IUiStrategy {
        public void OnEnter( UiStrategyContext context ) {
            // The match is running: the start button has no meaning anymore.
            context.SetVisible( context.StartGameUI, false );

            context.ActionPanel?.EnterGame();
            context.TableLayoutManager?.RefreshLayout();
        }

        public void OnRefresh( LobbyChangeReason reason, UiStrategyContext context ) {
            if ( reason != LobbyChangeReason.GameStarted
                && reason != LobbyChangeReason.TurnChanged
                && reason != LobbyChangeReason.SeatsChanged ) {
                return;
            }

            // Keep the panel visible and its buttons in sync with the
            // current selection / turn.
            context.ActionPanel?.EnterGame();
        }

        public void OnExit() {
            Debug.Log( "[UiStrategy] Exiting InGameUiStrategy" );
        }
    }
}
