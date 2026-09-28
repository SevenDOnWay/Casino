namespace Assets.Script.TienLen.UI.Strategy {
    /// <summary>
    /// A UI presentation policy for the lobby/table screen.
    ///
    /// UiManager owns the strategy lifecycle: it installs one strategy
    /// (calling <see cref="OnEnter"/> once) and forwards every
    /// <see cref="LobbyChangeReason"/> to <see cref="OnRefresh"/>.
    /// Strategies never subscribe to network or seat events themselves,
    /// they only react to the refresh reason they are handed.
    /// </summary>
    public interface IUiStrategy {
        /// <summary>Called once when UiManager installs this strategy.</summary>
        void OnEnter( UiStrategyContext context );

        /// <summary>Called on every lobby/table change while this strategy is installed.</summary>
        void OnRefresh( LobbyChangeReason reason, UiStrategyContext context );

        /// <summary>Called once when UiManager swaps this strategy out.</summary>
        void OnExit();
    }

    /// <summary>
    /// Lets a strategy hand control to another strategy without knowing
    /// anything about UiManager's concrete type.
    /// </summary>
    public interface IUiStrategyHost {
        void SwitchTo( IUiStrategy next );
    }
}
