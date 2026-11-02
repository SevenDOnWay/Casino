using Assets.Script.Data.SO;
using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.CardFolder;
using Assets.Script.TienLen.Game.Services;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;
using Assets.Script.TienLen.UI;
using Cysharp.Threading.Tasks;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.Game.Visual {
    public class TienLenAnimationHub : NetworkBehaviour, IAnimationPlayer {
        [Header("Visual References")]
        [SerializeField] private TienLenSO tienLenSO;
        [SerializeField] private CardHolder tableCenterPosition;
        [SerializeField] private TienLenDealingAnimator dealingAnimator;
        [SerializeField] private TienLenTableAnimator tableAnimator;

        [Header("Injected Dependencies")]
        private TableVisualLayoutManager tableVisualLayoutManager;
        private CardSpawner cardSpawner;
        private UiManager uiManager;
        private ISeatQueryService seatQueryService;
        private LocalPlayerService localPlayerService;
        private TienLenRuleValidator validator;
        private CardCombinationEvaluator cardCombinationEvaluator;

        private Dictionary<(CardSuit, CardRank), Sprite> sprites = new();
        private Sprite cardBack;
        private readonly Dictionary<PlayerRef, List<Card>> pendingHands = new();

        public Dictionary<PlayerRef, List<Card>> PendingHands => pendingHands;

        [Inject]
        public void Construct(
            TableVisualLayoutManager tableVisualLayoutManager,
            CardSpawner cardSpawner,
            UiManager uiManager,
            ISeatQueryService seatQueryService,
            LocalPlayerService localPlayerService,
            TienLenRuleValidator validator = null,
            CardCombinationEvaluator cardCombinationEvaluator = null ) {
            this.tableVisualLayoutManager = tableVisualLayoutManager;
            this.cardSpawner = cardSpawner;
            this.uiManager = uiManager;
            this.seatQueryService = seatQueryService;
            this.localPlayerService = localPlayerService;
            this.validator = validator;
            this.cardCombinationEvaluator = cardCombinationEvaluator;
        }

        private void Awake() {
            InitializeSprites();
            EnsureAnimators();
        }

        public override void Spawned() {
            base.Spawned();
            EnsureAnimators();
            InitializeSprites();
        }

        private void InitializeSprites() {
            if ( tienLenSO == null ) {
                tienLenSO = Resources.Load<TienLenSO>("SO/CardSpriteAtlas");
            }
            if ( tienLenSO != null ) {
                cardBack = tienLenSO.GetCardBackSprite();
                sprites = tienLenSO.GetLookUpTable() ?? new();
            }
        }

        private void EnsureAnimators() {
            if ( dealingAnimator == null ) {
                dealingAnimator = GetComponent<TienLenDealingAnimator>() ?? gameObject.AddComponent<TienLenDealingAnimator>();
            }
            if ( tableAnimator == null ) {
                tableAnimator = GetComponent<TienLenTableAnimator>() ?? gameObject.AddComponent<TienLenTableAnimator>();
            }

            if ( tableCenterPosition == null ) {
                var holders = FindObjectsByType<CardHolder>(FindObjectsSortMode.None);
                foreach ( var holder in holders ) {
                    if ( holder != null && holder.name.Equals("Center", System.StringComparison.OrdinalIgnoreCase) ) {
                        tableCenterPosition = holder;
                        break;
                    }
                }
            }

            if ( dealingAnimator != null && tableCenterPosition != null ) {
                dealingAnimator.TableCenterPosition = tableCenterPosition;
            }

            if ( tableAnimator != null && tableCenterPosition != null ) {
                tableAnimator.TableCenterPosition = tableCenterPosition;
            }

            if ( cardSpawner == null ) {
                cardSpawner = FindAnyObjectByType<CardSpawner>();
            }
        }

        public void SetTableCenterPosition( CardHolder center ) {
            tableCenterPosition = center;
            EnsureAnimators();
        }

        public void SetTienLenSO( TienLenSO so ) {
            tienLenSO = so;
            InitializeSprites();
        }

        public void RecordPendingHand( PlayerRef owner, List<Card> hand ) {
            if ( hand != null ) {
                pendingHands[owner] = hand;
            }
        }

        #region IAnimationPlayer Implementation

        public void PlayDealAnimation() {
            if ( Object != null && Object.IsValid && Object.HasStateAuthority ) {
                RPCPlayDealAnimation();
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPCPlayDealAnimation( RpcInfo info = default ) {
            EnsureAnimators();
            if ( dealingAnimator == null ) return;

            dealingAnimator.AnimateDealingRoutineAsync(
                seatQueryService,
                localPlayerService,
                tableVisualLayoutManager,
                cardSpawner,
                tienLenSO,
                cardBack,
                uiManager,
                pendingHands,
                ( seatOwner, seatIndex ) => (seatOwner.IsValid && seatOwner == Runner.LocalPlayer)
                                       || (!seatOwner.IsValid && seatIndex == 0 && Object.HasStateAuthority),
                destroyCancellationToken,
                sprites
            ).Forget();
        }

        public void PlayCardPlayAnimation( PlayerRef playerRef, int seatIndex, NetworkCard[] cards ) {
            if ( Object != null && Object.IsValid && Object.HasStateAuthority ) {
                RPCPlayAccepted(playerRef, cards, seatIndex);
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCPlayAccepted( PlayerRef playerRef, NetworkCard[] cards, int seatIndex ) {
            if ( cards == null || cards.Length == 0 ) return;
            List<Card> playedCards = cards.Select(c => c.ToCard()).ToList();

            if ( !Object.HasStateAuthority ) {
                TienLenPlayer player = seatQueryService?.GetLogicPlayer(playerRef);
                player?.TryRemoveCards(playedCards);

                if ( cardCombinationEvaluator != null && cardCombinationEvaluator.TryEvaluate(playedCards, out CardCombination combo) ) {
                    validator?.SetCurrentCombination(combo);
                }

                var actionPanel = FindAnyObjectByType<ActionPanel>();
                actionPanel?.RefreshActionState();
            }

            HandlePlayPresentation(playerRef, seatIndex, playedCards);
        }

        private void HandlePlayPresentation( PlayerRef playerRef, int seatIndex, List<Card> playedCards ) {
            EnsureAnimators();
            if ( tableAnimator == null ) return;

            tableAnimator.ClearTableCenter();

            CardHolder hand = null;
            if ( tableVisualLayoutManager != null && seatIndex >= 0 ) {
                hand = tableVisualLayoutManager.GetCardHolderForSeat(seatIndex);
            }

            tableAnimator.AnimateCards(hand, playedCards, cardSpawner, tienLenSO, sprites);
        }

        public void ClearTableCenter() {
            if ( Object != null && Object.IsValid && Object.HasStateAuthority ) {
                RPCClearTableCenter();
            }
            else {
                EnsureAnimators();
                tableAnimator?.ClearTableCenter();
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPCClearTableCenter() {
            EnsureAnimators();
            tableAnimator?.ClearTableCenter();
            validator?.Reset();
            var actionPanel = FindAnyObjectByType<ActionPanel>();
            actionPanel?.RefreshActionState();
        }

        #endregion
    }
}
