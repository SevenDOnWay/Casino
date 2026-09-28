using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class ActionPanel : MonoBehaviour {
        [Header("Dependencies")]
        private LobbySessionController lobbySessionController;
        private ILocalPlayerService localPlayerService;
        private TienLenRuleValidator validator;
        private CardCombinationEvaluator combinationEvaluator;
        private TurnManager turnManager;
        private TienLenGame game;
        private TienLenGameController gameController;
        private IPlayerRegisterService playerRegisterService;

        [Header("UI Elements")]
        [Tooltip("Optional panel root. Falls back to this component's GameObject.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button playBtn;
        [SerializeField] private Button sortBtn;
        [SerializeField] private Button passBtn;

        /// <summary>
        /// Turn order is not replicated yet (TurnManager.Initialize is never
        /// called), so play/pass stay available in this build. Flip to true
        /// once the turn owner is networked.
        /// </summary>
        private const bool EnforceTurnOrder = false;

        private TienLenPlayer localPlayer;
        private GameObject panelRootObject;

        // Last turn state pushed down by UiManager. Gates play/pass;
        // the host still validates every request authoritatively.
        private bool lastPushedIsTurn;

        private TienLenPlayer LocalPlayer {
            get {
                if ( localPlayer == null ) {
                    localPlayer = ResolveLocalLogicPlayer();
                }

                return localPlayer;
            }
        }

        /// <summary>
        /// The local logic player is published by the seat manager once the
        /// seat holding our PlayerRef has been bound. Until then this returns
        /// null and the panel simply stays idle, so it is resolved lazily.
        /// </summary>
        private TienLenPlayer ResolveLocalLogicPlayer() {
            TienLenNetWorkPlayer networkPlayer = localPlayerService?.GetLocalNetworkPlayer();
            if ( networkPlayer == null ) return null;

            TienLenPlayer resolved = playerRegisterService?.GetLogicPlayer( networkPlayer.PlayerRef );
            if ( resolved == null ) return null;

            SubscribeToHand( resolved );
            return resolved;
        }

        private CardHolder LocalHand => LocalPlayer?.CardHolder;

        private bool IsLocalPlayerTurn {
            get {
                if ( turnManager == null || localPlayer == null ) return false;
                return ReferenceEquals( turnManager.CurrentPlayer, localPlayer );
            }
        }

        [Inject]
        void Construct( LobbySessionController lobbySessionController,
            ILocalPlayerService localPlayerService,
            TienLenRuleValidator validator,
            CardCombinationEvaluator combinationEvaluator,
            TurnManager turnManager,
            TienLenGame game,
            TienLenGameController gameController,
            IPlayerRegisterService playerRegisterService ) {
            this.lobbySessionController = lobbySessionController;
            this.localPlayerService = localPlayerService;
            this.validator = validator;
            this.combinationEvaluator = combinationEvaluator;
            this.turnManager = turnManager;
            this.game = game;
            this.gameController = gameController;
            this.playerRegisterService = playerRegisterService;

            SubscribeEvents();
        }

        private void Awake() {
            panelRootObject = panelRoot != null ? panelRoot : gameObject;
        }

        private void Start() {
            if ( playBtn != null ) playBtn.onClick.AddListener( OnPlayBtnClick );
            if ( sortBtn != null ) sortBtn.onClick.AddListener( OnSortBtnClick );
            if ( passBtn != null ) passBtn.onClick.AddListener( OnPassBtnClick );

            // Sorting is always allowed, even outside a turn.
            SetSortInteractable( true );
            SetPlayInteractable( false );
            SetPassInteractable( false );
        }

        private void OnDestroy() {
            if ( playBtn != null ) playBtn.onClick.RemoveListener( OnPlayBtnClick );
            if ( sortBtn != null ) sortBtn.onClick.RemoveListener( OnSortBtnClick );
            if ( passBtn != null ) passBtn.onClick.RemoveListener( OnPassBtnClick );

            UnsubscribeEvents();
        }

        #region strategy entry points

        /// <summary>Called by InGameUiStrategy once the match has started.</summary>
        public void EnterGame() {
            SetPanelVisible( true );
            RefreshActionState();
        }

        /// <summary>Called by LobbyUiStrategy while waiting in the lobby.</summary>
        public void ReturnToLobby() {
            SetPanelVisible( false );
        }

        /// <summary>
        /// Dumb entry point: UiManager pushes whether it is the local
        /// player's turn. Play/pass enablement follows; selection changes
        /// re-evaluate through <see cref="RefreshActionState"/>.
        /// </summary>
        public void RenderTurnState( bool isLocalTurn ) {
            lastPushedIsTurn = isLocalTurn;
            RefreshActionState();
        }

        private void SetPanelVisible( bool visible ) {
            if ( panelRootObject == null ) return;

            if ( panelRootObject.activeSelf != visible ) {
                panelRootObject.SetActive( visible );
            }
        }

        #endregion

        #region events

        private void SubscribeEvents() {
            if ( localPlayerService != null ) {
                localPlayerService.OnLocalPlayerSet += HandleLocalPlayerSet;
            }

            if ( turnManager != null ) {
                turnManager.OnTurnChanged += HandleTurnChanged;
            }

            // The local player may already be bound by the time we inject.
            HandleLocalPlayerSet( localPlayerService?.GetLocalLogicPlayer() );
        }

        private void UnsubscribeEvents() {
            if ( localPlayerService != null ) {
                localPlayerService.OnLocalPlayerSet -= HandleLocalPlayerSet;
            }

            if ( turnManager != null ) {
                turnManager.OnTurnChanged -= HandleTurnChanged;
            }

            if ( localPlayer?.CardHolder != null ) {
                localPlayer.CardHolder.OnCardSelected -= HandleCardSelected;
            }
        }

        private void HandleLocalPlayerSet( TienLenPlayer player ) {
            if ( player == null || ReferenceEquals( player, localPlayer ) ) return;

            if ( localPlayer?.CardHolder != null ) {
                localPlayer.CardHolder.OnCardSelected -= HandleCardSelected;
            }

            localPlayer = player;
            SubscribeToHand( player );
        }

        private void SubscribeToHand( TienLenPlayer player ) {
            if ( player?.CardHolder == null ) return;

            player.CardHolder.OnCardSelected -= HandleCardSelected;
            player.CardHolder.OnCardSelected += HandleCardSelected;
        }

        private void HandleTurnChanged() {
            RefreshActionState();
        }

        private void HandleCardSelected( IReadOnlyList<CardView> selectedCards ) {
            RefreshActionState();
        }

        #endregion

        #region button handlers

        private void OnSortBtnClick() {
            CardHolder hand = LocalHand;
            if ( hand == null ) {
                Debug.LogWarning( "[ActionPanel] Sort ignored: no local hand." );
                return;
            }

            hand.SortCards();
            hand.ArrangeCards( animate: true );
        }

        private void OnPlayBtnClick() {
            if ( EnforceTurnOrder && !IsLocalPlayerTurn ) return;

            CardHolder hand = LocalHand;
            if ( hand == null ) {
                Debug.LogWarning( "[ActionPanel] Play ignored: no local hand." );
                return;
            }

            IReadOnlyList<CardView> selectedCards = hand.SelectedCards;
            if ( selectedCards == null || selectedCards.Count == 0 ) {
                Debug.LogWarning( "[ActionPanel] Play ignored: no cards selected." );
                return;
            }

            List<Card> cards = selectedCards
                .Select( view => view.Card )
                .ToList();

            if ( combinationEvaluator == null
                || !combinationEvaluator.TryEvaluate( cards, out CardCombination combination ) ) {
                Debug.Log( "[ActionPanel] Play ignored: selected cards are not a valid combination." );
                return;
            }

            TienLenNetWorkPlayer networkPlayer = localPlayerService?.GetLocalNetworkPlayer();
            if ( networkPlayer == null ) {
                Debug.LogWarning( "[ActionPanel] Play ignored: local network player is null." );
                return;
            }

            NetworkCard[] networkCards = combination.Cards
                .Select( card => new NetworkCard( card ) )
                .ToArray();

            networkPlayer.RPCRequestPlayCard( networkCards );

            // Ownership of the hand lives on the authoritative side: the
            // controller removes the cards and animates them to the table.
            // Locally we only drop the selection.
            hand.ClearSelection();
            RefreshActionState();
        }

        private void OnPassBtnClick() {
            if ( EnforceTurnOrder && !IsLocalPlayerTurn ) return;

            TienLenNetWorkPlayer networkPlayer = localPlayerService?.GetLocalNetworkPlayer();
            if ( networkPlayer == null ) {
                Debug.LogWarning( "[ActionPanel] Pass ignored: local network player is null." );
                return;
            }

            networkPlayer.RPCRequestPass();
        }

        #endregion

        #region state

        private void RefreshActionState() {
            SetSortInteractable( true );

            bool canAct = lastPushedIsTurn;
            SetPlayInteractable( canAct && HasPlayableSelection() );
            SetPassInteractable( canAct );
        }

        /// <summary>
        /// Play is only offered when the current selection is a combination
        /// the rules allow against the table.
        /// </summary>
        private bool HasPlayableSelection() {
            CardHolder hand = LocalHand;
            if ( hand == null || combinationEvaluator == null ) return false;

            IReadOnlyList<CardView> selected = hand.SelectedCards;
            if ( selected == null || selected.Count == 0 ) return false;

            List<Card> cards = selected
                .Select( view => view.Card )
                .ToList();

            if ( !combinationEvaluator.TryEvaluate( cards, out CardCombination combination ) ) {
                return false;
            }

            return validator == null || validator.CanPlay( combination );
        }

        private void SetSortInteractable( bool interactable ) {
            if ( sortBtn == null ) return;
            sortBtn.interactable = interactable;
        }

        private void SetPlayInteractable( bool interactable ) {
            if ( playBtn == null ) return;
            playBtn.interactable = interactable;
        }

        private void SetPassInteractable( bool interactable ) {
            if ( passBtn == null ) return;
            passBtn.interactable = interactable;
        }

        #endregion
    }
}
