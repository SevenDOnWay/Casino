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
    /// <summary>
    /// Action button bar for Tiến Lên gameplay (Play, Pass, Sort).
    /// Pure UI presenter listening to local card selection and button clicks,
    /// forwarding commands to the local network player.
    /// </summary>
    public class ActionPanel : MonoBehaviour {
        [Header("Dependencies")]
        private ILocalPlayerService localPlayerService;
        private TableVisualLayoutManager tableVisualLayoutManager;
        private CardCombinationEvaluator combinationEvaluator;
        private TienLenRuleValidator validator;

        [Header("UI Elements")]
        [Tooltip("Optional panel root. Falls back to this component's GameObject.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button playBtn;
        [SerializeField] private Button sortBtn;
        [SerializeField] private Button passBtn;

        private GameObject panelRootObject;
        private bool isThisPlayerTurn;
        private CardHolder subscribedHand;

        private CardHolder LocalHand {
            get {
                if ( subscribedHand != null ) return subscribedHand;
                return tableVisualLayoutManager.GetLocalCardHolder();
            }
        }

        [Inject]
        void Construct(
            ILocalPlayerService localPlayerService,
            TableVisualLayoutManager tableVisualLayoutManager,
            CardCombinationEvaluator combinationEvaluator,
            TienLenRuleValidator validator ) {
            this.localPlayerService = localPlayerService;
            this.tableVisualLayoutManager = tableVisualLayoutManager;
            this.combinationEvaluator = combinationEvaluator;
            this.validator = validator;
        }

        private void Awake() {
            panelRootObject = panelRoot != null ? panelRoot : gameObject;
        }

        private void Start() {
            if ( playBtn != null ) playBtn.onClick.AddListener(OnPlayBtnClick);
            if ( sortBtn != null ) sortBtn.onClick.AddListener(OnSortBtnClick);
            if ( passBtn != null ) passBtn.onClick.AddListener(OnPassBtnClick);

            SetPlayInteractable(false);
            SetPassInteractable(false);

            EnsureHandSubscribed();
        }

        private void OnDestroy() {
            if ( playBtn != null ) playBtn.onClick.RemoveListener(OnPlayBtnClick);
            if ( sortBtn != null ) sortBtn.onClick.RemoveListener(OnSortBtnClick);
            if ( passBtn != null ) passBtn.onClick.RemoveListener(OnPassBtnClick);

            UnsubscribeHand();
        }

        private void Update() {
            // Lazy subscribe if local hand becomes ready after initial start
            if ( subscribedHand == null ) {
                EnsureHandSubscribed();
            }
        }

        private void EnsureHandSubscribed() {
            CardHolder hand = LocalHand;
            if ( hand != null && hand != subscribedHand ) {
                UnsubscribeHand();
                subscribedHand = hand;
                subscribedHand.OnCardSelected += HandleCardSelected;
            }
        }

        private void UnsubscribeHand() {
            if ( subscribedHand != null ) {
                subscribedHand.OnCardSelected -= HandleCardSelected;
                subscribedHand = null;
            }
        }

        #region Public Presentation Entry Points

        public void EnterGame() {
            SetPanelVisible(true);
            EnsureHandSubscribed();
            RefreshActionState();
        }

        public void ReturnToLobby() {
            SetPanelVisible(false);
        }

        public void RenderTurnState( bool isLocalTurn ) {
            isThisPlayerTurn = isLocalTurn;
            RefreshActionState();
        }

        private void SetPanelVisible( bool visible ) {
            if ( panelRootObject == null ) return;
            if ( panelRootObject.activeSelf != visible ) {
                panelRootObject.SetActive(visible);
            }
        }

        #endregion

        #region Events

        private void HandleCardSelected( IReadOnlyList<CardView> selectedCards ) {
            RefreshActionState();
        }

        #endregion

        #region Button Handlers

        private void OnSortBtnClick() {
            LocalHand.SortCards();
            LocalHand.ArrangeCards(animate: true);
        }

        private void OnPlayBtnClick() {
            IReadOnlyList<CardView> selectedCards = LocalHand.SelectedCards;

            List<Card> cards = selectedCards
                .Select(view => view.Card)
                .ToList();

            TienLenNetWorkPlayer networkPlayer = localPlayerService?.GetLocalNetworkPlayer();

            if ( !combinationEvaluator.TryEvaluate(cards, out CardCombination combination) ) {
                Debug.Log("[ActionPanel] Play ignored: selected cards are not a valid combination.");
                return;
            }

            if ( validator != null && !validator.CanPlay(combination) ) {
                Debug.Log("[ActionPanel] Play ignored: combination is not legal against the table.");
                return;
            }

            NetworkCard[] networkCards = combination.Cards
                .Select(card => new NetworkCard(card))
                .ToArray();

            networkPlayer.RPCRequestPlayCard(networkCards);

            LocalHand.ClearSelection();
            RefreshActionState();
        }

        private void OnPassBtnClick() {
            TienLenNetWorkPlayer networkPlayer = localPlayerService?.GetLocalNetworkPlayer();
            if ( networkPlayer == null ) {
                Debug.LogWarning("[ActionPanel] Pass ignored: local network player is null.");
                return;
            }

            networkPlayer.RPCRequestPass();
            RefreshActionState();
        }

        #endregion

        #region State Evaluation

        public void RefreshActionState() {
            if ( !isThisPlayerTurn ) {
                SetPlayInteractable(false);
                SetPassInteractable(false);
                return;
            }
            SetPlayInteractable(CheckForPlayable());
            SetPassInteractable(validator?.CurrentCombination != null);
        }

        private void SetPlayInteractable( bool interactable ) {
            if ( playBtn != null ) playBtn.interactable = interactable;
        }

        private void SetPassInteractable( bool interactable ) {
            if ( passBtn != null ) passBtn.interactable = interactable;
        }

        #endregion

        private bool CheckForPlayable() {
            IReadOnlyList<CardView> selectedCards = LocalHand.SelectedCards;
            if ( selectedCards == null || selectedCards.Count == 0 ) {
                return false;
            }
            List<Card> cards = selectedCards
                .Select(view => view.Card)
                .ToList();

            if ( !combinationEvaluator.TryEvaluate(cards, out CardCombination combination) ) return false;
            if ( validator != null && !validator.CanPlay(combination) ) return false;

            return true;
        }
    }
}
