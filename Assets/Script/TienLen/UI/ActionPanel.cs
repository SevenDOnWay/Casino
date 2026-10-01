using Assets.Script.NetWorkScript;
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
        private bool lastPushedIsTurn;
        private CardHolder subscribedHand;

        private CardHolder LocalHand {
            get {
                if ( subscribedHand != null ) return subscribedHand;
                if ( tableVisualLayoutManager != null ) {
                    return tableVisualLayoutManager.GetLocalCardHolder();
                }
                tableVisualLayoutManager = FindAnyObjectByType<TableVisualLayoutManager>();
                return tableVisualLayoutManager?.GetLocalCardHolder();
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

            SetSortInteractable(true);
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
            lastPushedIsTurn = isLocalTurn;
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
            CardHolder hand = LocalHand;
            if ( hand == null ) {
                Debug.LogWarning("[ActionPanel] Sort ignored: no local hand.");
                return;
            }

            hand.SortCards();
            hand.ArrangeCards(animate: true);
        }

        private void OnPlayBtnClick() {
            if ( !lastPushedIsTurn ) {
                Debug.LogWarning("[ActionPanel] Play ignored: not local player's turn.");
                return;
            }

            CardHolder hand = LocalHand;
            if ( hand == null ) {
                Debug.LogWarning("[ActionPanel] Play ignored: no local hand.");
                return;
            }

            IReadOnlyList<CardView> selectedCards = hand.SelectedCards;
            if ( selectedCards == null || selectedCards.Count == 0 ) {
                Debug.LogWarning("[ActionPanel] Play ignored: no cards selected.");
                return;
            }

            List<Card> cards = selectedCards
                .Select(view => view.Card)
                .ToList();

            if ( combinationEvaluator == null
                || !combinationEvaluator.TryEvaluate(cards, out CardCombination combination) ) {
                Debug.Log("[ActionPanel] Play ignored: selected cards are not a valid combination.");
                return;
            }

            if ( validator != null && !validator.CanPlay(combination) ) {
                Debug.Log("[ActionPanel] Play ignored: combination is not legal against the table.");
                return;
            }

            TienLenNetWorkPlayer networkPlayer = localPlayerService?.GetLocalNetworkPlayer();
            if ( networkPlayer == null ) {
                Debug.LogWarning("[ActionPanel] Play ignored: local network player is null.");
                return;
            }

            NetworkCard[] networkCards = combination.Cards
                .Select(card => new NetworkCard(card))
                .ToArray();

            networkPlayer.RPCRequestPlayCard(networkCards);

            hand.ClearSelection();
            RefreshActionState();
        }

        private void OnPassBtnClick() {
            if ( !lastPushedIsTurn ) {
                Debug.LogWarning("[ActionPanel] Pass ignored: not local player's turn.");
                return;
            }

            TienLenNetWorkPlayer networkPlayer = localPlayerService?.GetLocalNetworkPlayer();
            if ( networkPlayer == null ) {
                Debug.LogWarning("[ActionPanel] Pass ignored: local network player is null.");
                return;
            }

            networkPlayer.RPCRequestPass();
        }

        #endregion

        #region State Evaluation

        private void RefreshActionState() {
            SetSortInteractable(true);

            bool canAct = lastPushedIsTurn;
            SetPlayInteractable(canAct && HasPlayableSelection());
            SetPassInteractable(canAct);
        }

        private bool HasPlayableSelection() {
            CardHolder hand = LocalHand;
            if ( hand == null || combinationEvaluator == null ) return false;

            IReadOnlyList<CardView> selected = hand.SelectedCards;
            if ( selected == null || selected.Count == 0 ) return false;

            List<Card> cards = selected
                .Select(view => view.Card)
                .ToList();

            if ( !combinationEvaluator.TryEvaluate(cards, out CardCombination combination) ) {
                return false;
            }

            return validator == null || validator.CanPlay(combination);
        }

        private void SetSortInteractable( bool interactable ) {
            if ( sortBtn != null ) sortBtn.interactable = interactable;
        }

        private void SetPlayInteractable( bool interactable ) {
            if ( playBtn != null ) playBtn.interactable = interactable;
        }

        private void SetPassInteractable( bool interactable ) {
            if ( passBtn != null ) passBtn.interactable = interactable;
        }

        #endregion
    }
}
