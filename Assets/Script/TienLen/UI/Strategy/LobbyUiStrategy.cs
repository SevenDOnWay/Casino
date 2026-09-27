using UnityEngine;

namespace Assets.Script.TienLen.UI.Strategy {
    /// <summary>
    /// Pre-match presentation: session info and the start button are visible,
    /// the action panel is hidden. Hands over to <see cref="InGameUiStrategy"/>
    /// as soon as the host starts the match.
    /// </summary>
    public sealed class LobbyUiStrategy : IUiStrategy {
        public void OnEnter( UiStrategyContext context ) {
            context.ActionPanel?.ReturnToLobby();

            context.SetVisible( context.SessionDisplayUI, true );
            context.SetVisible( context.StartGameUI, true );

            _ = context.TableLayoutManager?.RefreshLayoutAsync();
        }

        public void OnRefresh( LobbyChangeReason reason, UiStrategyContext context ) {
            // The host started the match on some peer: everyone switches over.
            if ( reason == LobbyChangeReason.GameStarted || context.IsGameRunning ) {
                context.Host.SwitchTo( new InGameUiStrategy() );
                return;
            }

            context.StartGameUI?.RefreshStartButton();
            _ = context.TableLayoutManager?.RefreshLayoutAsync();
        }

        public void OnExit() {
            Debug.Log( "[UiStrategy] Exiting LobbyUiStrategy" );
        }
    }
}