using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Script.Data.Models;
using Assets.Script.Data.Repositories;
using Assets.Script.Data.Services;
using Assets.Script.Data.SO;
using Assets.Script.NetWorkScript;
using Assets.Script.UI.Components;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VContainer;

namespace Assets.Script.UI {
    /// <summary>
    /// UI Toolkit controller managing the Minimal Arena Main Menu & Lobby interface.
    /// Supports profile data binding, network matchmaking, modals (Host, Search, Settings, Rules, Leaderboard),
    /// responsive adaptations, and standalone/VContainer injection workflows.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour {
        [Header("Configuration")]
        [SerializeField] private MainMenuConfigSO config;
        [SerializeField] private AvatarDatabaseSO avatarDatabase;
        [SerializeField] private bool useMockIfUninjected = true;
        [SerializeField] private string defaultApiUrl = "http://localhost:5000/api";

        [Header("Optional Network Integration")]
        [SerializeField] private TienLenNetworkController networkController;

        [Inject]
        private IPlayerProfileService profileService;

        [Inject]
        private IAuthService authService;

        [Inject]
        private ILocalPlayerService localPlayerService;

        // Visual Tree References
        private UIDocument uiDocument;
        private VisualElement root;

        // Header Elements
        private VisualElement profileCard;
        private Label playerNameLabel;
        private Label playerLvlTag;
        private Label currencyCrAmount;
        private Label currencyTkAmount;
        private Label avatarPlaceholderText;
        private VisualElement avatarImage;
        private Label latencyText;
        private Button btnNotifications;
        private Button btnSettings;

        // Center Action Elements
        private VisualElement btnQuickJoin;
        private VisualElement btnHostTable;
        private VisualElement btnSearchTables;
        private VisualElement btnSoloPractice;

        // Footer Elements
        private Label footerServerLabel;
        private Label footerVersionLabel;
        private Button btnRules;
        private Button btnLeaderboard;
        private Button btnSupport;

        // Modals
        private VisualElement modalHostTable;
        private VisualElement modalSearchTables;
        private VisualElement modalSettings;
        private VisualElement modalLeaderboard;
        private VisualElement modalRules;
        private VisualElement modalNotifications;

        // Host Table Modal Elements
        private TextField inputHostTableName;
        private Button btnPlayers2;
        private Button btnPlayers3;
        private Button btnPlayers4;
        private Button btnStake1k;
        private Button btnStake5k;
        private Button btnStake20k;
        private Button btnStake100k;
        private Toggle togglePrivateRoom;
        private Button btnCancelHost;
        private Button btnConfirmHost;
        private Button btnCloseHostModal;
        private int selectedPlayerCount = 4;
        private long selectedStake = 1000;

        // Search Table Modal Elements
        private TextField inputRoomCode;
        private Button btnJoinCode;
        private Button btnRefreshRooms;
        private Button btnCloseSearch;
        private Button btnCloseSearchModal;
        private ScrollView roomListScroll;

        // Settings Modal Elements
        private Slider sliderMasterVolume;
        private Slider sliderSfxVolume;
        private Slider sliderBgmVolume;
        private Button btnRegionNa;
        private Button btnRegionEu;
        private Button btnRegionAsia;
        private Button btnLogout;
        private Button btnSaveSettings;
        private Button btnCloseSettingsModal;
        private string selectedRegion = "NORTH-AMERICA";

        // Other Modal Close Elements
        private Button btnCloseLeaderboard;
        private Button btnCloseLeaderboardModal;
        private Button btnCloseRules;
        private Button btnCloseRulesModal;
        private Button btnCloseNotifications;
        private Button btnCloseNotificationsModal;
        private Button btnClaimBonus;

        // Coroutines & State
        private Coroutine pingSimulationCoroutine;
        private bool hasClaimedDailyBonus = false;

        // Public Events for Decoupled Extensibility
        public event Action OnQuickJoinRequested;
        public event Action<string, int, long, bool> OnHostTableRequested;
        public event Action<string> OnJoinTableRequested;
        public event Action OnSoloPracticeRequested;
        public event Action OnLogoutRequested;
        public event Action<string> OnRegionChanged;

        private void Awake() {
            uiDocument = GetComponent<UIDocument>();
            if (networkController == null) {
                networkController = FindFirstObjectByType<TienLenNetworkController>();
            }

            // MainMenuScene has no LifetimeScope of its own, so pull the persistent
            // services out of the application root container before falling back.
            RootLifeTimeScope.Ensure()?.InjectGameObject(gameObject);

            EnsureServices();
        }

        private void EnsureServices() {
            var root = RootLifeTimeScope.Ensure();
            if ( profileService == null && root != null &&
                 root.TryResolve(out IPlayerProfileService resolvedProfile) ) {
                profileService = resolvedProfile;
                root.TryResolve(out IAuthService resolvedAuthService);
                root.TryResolve(out ILocalPlayerService resolvedLocalPlayerService);
                authService ??= resolvedAuthService;
                localPlayerService ??= resolvedLocalPlayerService;
            }

            if ( profileService == null ) {
                IPlayerRepository repo = useMockIfUninjected
                    ? new MockPlayerRepository()
                    : new RestPlayerRepository(defaultApiUrl);

                authService ??= new AuthService(repo);
                profileService = new PlayerProfileService(repo, authService);
            }

            if ( localPlayerService == null ) {
                localPlayerService = new LocalPlayerService(profileService);
            }
        }

        private void OnEnable() {
            if (uiDocument == null) return;
            root = uiDocument.rootVisualElement;
            if (root == null) return;

            QueryElements();
            BindEvents();
            ApplyInitialData();
            SubscribeToProfileChanges();

            if (config != null && config.SimulatePing) {
                pingSimulationCoroutine = StartCoroutine(SimulatePingRoutine());
            }
        }

        private void OnDisable() {
            UnbindEvents();
            UnsubscribeFromProfileChanges();

            if (pingSimulationCoroutine != null) {
                StopCoroutine(pingSimulationCoroutine);
                pingSimulationCoroutine = null;
            }
        }

        private async void Start() {
            if (profileService != null) {
                try {
                    await profileService.RefreshProfileAsync();
                    localPlayerService?.SetProfile(profileService.CachedProfile);
                    UpdateProfileDisplay(profileService.CachedProfile);
                } catch (Exception ex) {
                    Debug.LogWarning($"[MainMenuController] Failed to refresh profile: {ex.Message}");
                }
            }
        }

        private void QueryElements() {
            // Header
            profileCard = root.Q<VisualElement>("profile-card");
            playerNameLabel = root.Q<Label>("player-name-label");
            playerLvlTag = root.Q<Label>("player-lvl-tag");
            currencyCrAmount = root.Q<Label>("currency-cr-amount");
            currencyTkAmount = root.Q<Label>("currency-tk-amount");
            avatarPlaceholderText = root.Q<Label>("avatar-placeholder-text");
            avatarImage = root.Q<VisualElement>("avatar-image");
            latencyText = root.Q<Label>("latency-text");
            btnNotifications = root.Q<Button>("btn-notifications");
            btnSettings = root.Q<Button>("btn-settings");

            // Actions Stack
            btnQuickJoin = root.Q<VisualElement>("btn-quick-join");
            btnHostTable = root.Q<VisualElement>("btn-host-table");
            btnSearchTables = root.Q<VisualElement>("btn-search-tables");
            btnSoloPractice = root.Q<VisualElement>("btn-solo-practice");

            // Footer
            footerServerLabel = root.Q<Label>("footer-server-label");
            footerVersionLabel = root.Q<Label>("footer-version-label");
            btnRules = root.Q<Button>("btn-rules");
            btnLeaderboard = root.Q<Button>("btn-leaderboard");
            btnSupport = root.Q<Button>("btn-support");

            // Modals
            modalHostTable = root.Q<VisualElement>("modal-host-table");
            modalSearchTables = root.Q<VisualElement>("modal-search-tables");
            modalSettings = root.Q<VisualElement>("modal-settings");
            modalLeaderboard = root.Q<VisualElement>("modal-leaderboard");
            modalRules = root.Q<VisualElement>("modal-rules");
            modalNotifications = root.Q<VisualElement>("modal-notifications");

            // Host Table Modal Sub-elements
            inputHostTableName = root.Q<TextField>("input-host-table-name");
            btnPlayers2 = root.Q<Button>("btn-players-2");
            btnPlayers3 = root.Q<Button>("btn-players-3");
            btnPlayers4 = root.Q<Button>("btn-players-4");
            btnStake1k = root.Q<Button>("btn-stake-1k");
            btnStake5k = root.Q<Button>("btn-stake-5k");
            btnStake20k = root.Q<Button>("btn-stake-20k");
            btnStake100k = root.Q<Button>("btn-stake-100k");
            togglePrivateRoom = root.Q<Toggle>("toggle-private-room");
            btnCancelHost = root.Q<Button>("btn-cancel-host");
            btnConfirmHost = root.Q<Button>("btn-confirm-host");
            btnCloseHostModal = root.Q<Button>("btn-close-host-modal");

            // Search Table Modal Sub-elements
            inputRoomCode = root.Q<TextField>("input-room-code");
            btnJoinCode = root.Q<Button>("btn-join-code");
            btnRefreshRooms = root.Q<Button>("btn-refresh-rooms");
            btnCloseSearch = root.Q<Button>("btn-close-search");
            btnCloseSearchModal = root.Q<Button>("btn-close-search-modal");
            roomListScroll = root.Q<ScrollView>("room-list-scroll");

            // Settings Modal Sub-elements
            sliderMasterVolume = root.Q<Slider>("slider-master-volume");
            sliderSfxVolume = root.Q<Slider>("slider-sfx-volume");
            sliderBgmVolume = root.Q<Slider>("slider-bgm-volume");
            btnRegionNa = root.Q<Button>("btn-region-na");
            btnRegionEu = root.Q<Button>("btn-region-eu");
            btnRegionAsia = root.Q<Button>("btn-region-asia");
            btnLogout = root.Q<Button>("btn-logout");
            btnSaveSettings = root.Q<Button>("btn-save-settings");
            btnCloseSettingsModal = root.Q<Button>("btn-close-settings-modal");

            // Other Modals Sub-elements
            btnCloseLeaderboard = root.Q<Button>("btn-close-leaderboard");
            btnCloseLeaderboardModal = root.Q<Button>("btn-close-leaderboard-modal");
            btnCloseRules = root.Q<Button>("btn-close-rules");
            btnCloseRulesModal = root.Q<Button>("btn-close-rules-modal");
            btnCloseNotifications = root.Q<Button>("btn-close-notifications");
            btnCloseNotificationsModal = root.Q<Button>("btn-close-notifications-modal");
            btnClaimBonus = root.Q<Button>("btn-claim-bonus");
        }

        private void BindEvents() {
            // Header Clicks
            if (profileCard != null) profileCard.RegisterCallback<ClickEvent>(evt => OpenModal(modalLeaderboard));
            if (btnNotifications != null) btnNotifications.clicked += () => OpenModal(modalNotifications);
            if (btnSettings != null) btnSettings.clicked += () => OpenModal(modalSettings);

            // Action Buttons
            BindActionButton(btnQuickJoin, HandleQuickJoin);
            BindActionButton(btnHostTable, () => OpenModal(modalHostTable));
            BindActionButton(btnSearchTables, () => OpenModal(modalSearchTables));
            BindActionButton(btnSoloPractice, HandleSoloPractice);

            // Footer Links
            if (btnRules != null) btnRules.clicked += () => OpenModal(modalRules);
            if (btnLeaderboard != null) btnLeaderboard.clicked += () => OpenModal(modalLeaderboard);
            if (btnSupport != null) btnSupport.clicked += HandleSupportLink;

            // Host Table Modal Events
            if (btnPlayers2 != null) btnPlayers2.clicked += () => SetPlayerCountSelection(2);
            if (btnPlayers3 != null) btnPlayers3.clicked += () => SetPlayerCountSelection(3);
            if (btnPlayers4 != null) btnPlayers4.clicked += () => SetPlayerCountSelection(4);

            if (btnStake1k != null) btnStake1k.clicked += () => SetStakeSelection(1000);
            if (btnStake5k != null) btnStake5k.clicked += () => SetStakeSelection(5000);
            if (btnStake20k != null) btnStake20k.clicked += () => SetStakeSelection(20000);
            if (btnStake100k != null) btnStake100k.clicked += () => SetStakeSelection(100000);

            if (btnCancelHost != null) btnCancelHost.clicked += () => CloseModal(modalHostTable);
            if (btnCloseHostModal != null) btnCloseHostModal.clicked += () => CloseModal(modalHostTable);
            if (btnConfirmHost != null) btnConfirmHost.clicked += HandleConfirmHostTable;

            // Search Table Modal Events
            if (btnJoinCode != null) btnJoinCode.clicked += HandleJoinByCode;
            if (btnRefreshRooms != null) btnRefreshRooms.clicked += HandleRefreshRoomList;
            if (btnCloseSearch != null) btnCloseSearch.clicked += () => CloseModal(modalSearchTables);
            if (btnCloseSearchModal != null) btnCloseSearchModal.clicked += () => CloseModal(modalSearchTables);
            BindDynamicRoomListButtons();

            // Settings Modal Events
            if (btnRegionNa != null) btnRegionNa.clicked += () => SetRegionSelection("NORTH-AMERICA");
            if (btnRegionEu != null) btnRegionEu.clicked += () => SetRegionSelection("EUROPE");
            if (btnRegionAsia != null) btnRegionAsia.clicked += () => SetRegionSelection("ASIA-PACIFIC");
            if (btnLogout != null) btnLogout.clicked += HandleLogout;
            if (btnSaveSettings != null) btnSaveSettings.clicked += () => CloseModal(modalSettings);
            if (btnCloseSettingsModal != null) btnCloseSettingsModal.clicked += () => CloseModal(modalSettings);

            // Other Modals Events
            if (btnCloseLeaderboard != null) btnCloseLeaderboard.clicked += () => CloseModal(modalLeaderboard);
            if (btnCloseLeaderboardModal != null) btnCloseLeaderboardModal.clicked += () => CloseModal(modalLeaderboard);
            if (btnCloseRules != null) btnCloseRules.clicked += () => CloseModal(modalRules);
            if (btnCloseRulesModal != null) btnCloseRulesModal.clicked += () => CloseModal(modalRules);
            if (btnCloseNotifications != null) btnCloseNotifications.clicked += () => CloseModal(modalNotifications);
            if (btnCloseNotificationsModal != null) btnCloseNotificationsModal.clicked += () => CloseModal(modalNotifications);
            if (btnClaimBonus != null) btnClaimBonus.clicked += HandleClaimDailyBonus;

            // Backdrop click to dismiss
            BindBackdropDismiss(modalHostTable);
            BindBackdropDismiss(modalSearchTables);
            BindBackdropDismiss(modalSettings);
            BindBackdropDismiss(modalLeaderboard);
            BindBackdropDismiss(modalRules);
            BindBackdropDismiss(modalNotifications);
        }

        private void UnbindEvents() {
            // Handlers automatically cleaned up on disable/destruction
        }

        private void BindActionButton(VisualElement element, Action callback) {
            if (element is Button btn) {
                btn.clicked += callback;
            } else if (element != null) {
                element.RegisterCallback<ClickEvent>(evt => callback?.Invoke());
            }
        }

        private void BindBackdropDismiss(VisualElement modalScrim) {
            if (modalScrim == null) return;
            modalScrim.RegisterCallback<ClickEvent>(evt => {
                if (evt.target == modalScrim) {
                    CloseModal(modalScrim);
                }
            });
        }

        private void BindDynamicRoomListButtons() {
            if (roomListScroll == null) return;
            var joinButtons = roomListScroll.Query<Button>(className: "room-item-join-btn").ToList();
            for (int i = 0; i < joinButtons.Count; i++) {
                int index = i;
                joinButtons[i].clicked += () => {
                    string sampleCode = $"ROOM_0{index + 1}";
                    HandleJoinTable(sampleCode);
                };
            }
        }

        private void ApplyInitialData() {
            string region = config != null ? config.ServerRegion : "NORTH-AMERICA";
            string version = config != null ? config.VersionString : "v2.1.0";

            if (footerServerLabel != null) footerServerLabel.text = $"SERVER: {region}";
            if (footerVersionLabel != null) footerVersionLabel.text = version;
            if (latencyText != null) latencyText.text = "24ms";

            SetPlayerCountSelection(config != null ? config.DefaultPlayerCount : 4);
            SetStakeSelection(config != null ? config.DefaultStake : 1000);
            SetRegionSelection(region);

            // Populate fallback profile visuals
            if (playerNameLabel != null) playerNameLabel.text = "PLAYER_01";
            if (playerLvlTag != null) playerLvlTag.text = "LVL 42";
            if (currencyCrAmount != null) currencyCrAmount.text = "148,250";
            if (currencyTkAmount != null) currencyTkAmount.text = "1,890";
            if (avatarPlaceholderText != null) avatarPlaceholderText.text = "P1";
        }

        private void SubscribeToProfileChanges() {
            if (profileService == null) return;
            profileService.OnMoneyChanged += HandleMoneyChanged;
            profileService.OnLevelOrExpChanged += HandleLevelOrExpChanged;
            profileService.OnDisplayNameChanged += HandleDisplayNameChanged;
            profileService.OnAvatarChanged += HandleAvatarChanged;
        }

        private void UnsubscribeFromProfileChanges() {
            if (profileService == null) return;
            profileService.OnMoneyChanged -= HandleMoneyChanged;
            profileService.OnLevelOrExpChanged -= HandleLevelOrExpChanged;
            profileService.OnDisplayNameChanged -= HandleDisplayNameChanged;
            profileService.OnAvatarChanged -= HandleAvatarChanged;
        }

        private void HandleAvatarChanged(int newAvatarId) {
            if (profileService?.CachedProfile != null) {
                UpdateProfileDisplay(profileService.CachedProfile);
            }
        }

        private void UpdateProfileDisplay(PlayerProfileData profile) {
            if (profile == null) return;

            if (playerNameLabel != null && !string.IsNullOrEmpty(profile.displayName)) {
                playerNameLabel.text = profile.displayName;
                if (avatarPlaceholderText != null) {
                    avatarPlaceholderText.text = profile.displayName.Length > 2
                        ? profile.displayName.Substring(0, 2).ToUpper()
                        : profile.displayName.ToUpper();
                }
            }

            if (playerLvlTag != null) {
                playerLvlTag.text = $"LVL {Mathf.Max(1, profile.level)}";
            }

            if (currencyCrAmount != null) {
                currencyCrAmount.text = profile.money.ToString("N0");
            }

            if (currencyTkAmount != null) {
                // Tokens / Gems: 1 TK per 100 CR or custom
                long tokens = Mathf.Max(100, (int)(profile.money / 100));
                currencyTkAmount.text = tokens.ToString("N0");
            }

            if (avatarDatabase != null && avatarImage != null) {
                if (avatarDatabase.TryGetAvatarSprite(profile.avatarId, out Sprite sprite) && sprite != null) {
                    avatarImage.style.backgroundImage = new StyleBackground(sprite);
                    if (avatarPlaceholderText != null) avatarPlaceholderText.style.display = DisplayStyle.None;
                } else {
                    avatarImage.style.backgroundImage = StyleKeyword.Null;
                    if (avatarPlaceholderText != null) avatarPlaceholderText.style.display = DisplayStyle.Flex;
                }
            }
        }

        private void HandleMoneyChanged(long newMoney) {
            if (currencyCrAmount != null) {
                currencyCrAmount.text = newMoney.ToString("N0");
            }
        }

        private void HandleLevelOrExpChanged(int level, int exp) {
            if (playerLvlTag != null) {
                playerLvlTag.text = $"LVL {level}";
            }
        }

        private void HandleDisplayNameChanged(string newName) {
            if (playerNameLabel != null) {
                playerNameLabel.text = newName;
            }
        }

        // =====================================================================
        // Modal System
        // =====================================================================
        public void OpenModal(VisualElement modal) {
            if (modal == null) return;
            modal.RemoveFromClassList("hidden");
        }

        public void CloseModal(VisualElement modal) {
            if (modal == null) return;
            modal.AddToClassList("hidden");
        }

        public void CloseAllModals() {
            CloseModal(modalHostTable);
            CloseModal(modalSearchTables);
            CloseModal(modalSettings);
            CloseModal(modalLeaderboard);
            CloseModal(modalRules);
            CloseModal(modalNotifications);
        }

        // =====================================================================
        // Action Handlers
        // =====================================================================
        private void HandleQuickJoin() {
            //Debug.Log("[MainMenuController] Quick Join requested. Matching with active table...");
            //OnQuickJoinRequested?.Invoke();

            //if (networkController != null) {
            //    networkController.JoinRoom("QuickMatch");
            //} else {
            //    LoadGameplayScene();
            //}
        }

        private void HandleConfirmHostTable() {
            string roomName = inputHostTableName != null && !string.IsNullOrEmpty(inputHostTableName.value)
                ? inputHostTableName.value
                : (config != null ? config.DefaultRoomName : "ArenaTable");

            bool isPrivate = togglePrivateRoom != null && togglePrivateRoom.value;

            Debug.Log($"[MainMenuController] Hosting table '{roomName}' with {selectedPlayerCount} players, Stake: {selectedStake} CR, Private: {isPrivate}");
            CloseModal(modalHostTable);
            OnHostTableRequested?.Invoke(roomName, selectedPlayerCount, selectedStake, isPrivate);

            if (networkController != null) {
                networkController.HostRoom(roomName, selectedPlayerCount);
            } else {
                LoadGameplayScene();
            }
        }

        private void HandleJoinByCode() {
            string roomCode = inputRoomCode != null ? inputRoomCode.value.Trim() : string.Empty;
            if (string.IsNullOrEmpty(roomCode)) {
                Debug.LogWarning("[MainMenuController] Please enter a valid room code.");
                return;
            }

            HandleJoinTable(roomCode);
        }

        private void HandleJoinTable(string roomCode) {
            //Debug.Log($"[MainMenuController] Joining room '{roomCode}'...");
            //CloseModal(modalSearchTables);
            //OnJoinTableRequested?.Invoke(roomCode);

            //if (networkController != null) {
            //    networkController.JoinRoom(roomCode);
            //} else {
            //    LoadGameplayScene();
            //}
        }

        private void HandleSoloPractice() {
            Debug.Log("[MainMenuController] Solo Practice requested. Launching offline bot match...");
            OnSoloPracticeRequested?.Invoke();
            LoadGameplayScene();
        }

        private void HandleRefreshRoomList() {
            Debug.Log("[MainMenuController] Refreshing active rooms list...");
            // Can be tied to Photon Matchmaking Lobby / Session List callbacks
        }

        private void HandleClaimDailyBonus() {
            if (hasClaimedDailyBonus) return;
            hasClaimedDailyBonus = true;

            if (btnClaimBonus != null) {
                btnClaimBonus.text = "Claimed ✓";
                btnClaimBonus.SetEnabled(false);
            }

            if (profileService != null) {
                var cached = profileService.CachedProfile;
                if (cached != null) {
                    cached.money += 5000;
                    UpdateProfileDisplay(cached);
                }
            }

            Debug.Log("[MainMenuController] Claimed +5,000 CR daily bonus!");
        }

        private void HandleLogout() {
            Debug.Log("[MainMenuController] Logging out user...");
            authService?.Logout();
            OnLogoutRequested?.Invoke();

            string signInScene = config != null ? config.SignInSceneName : "SignInScene";
            if (!string.IsNullOrEmpty(signInScene) && Application.CanStreamedLevelBeLoaded(signInScene)) {
                SceneManager.LoadScene(signInScene);
            }
        }

        private void HandleSupportLink() {
            Debug.Log("[MainMenuController] Opening support portal...");
            Application.OpenURL("https://github.com");
        }

        private void LoadGameplayScene() {
            string targetScene = config != null ? config.GameplaySceneName : "GameScene";
            if (!string.IsNullOrEmpty(targetScene) && Application.CanStreamedLevelBeLoaded(targetScene)) {
                SceneManager.LoadScene(targetScene);
            } else {
                Debug.LogWarning($"[MainMenuController] Scene '{targetScene}' cannot be loaded or is missing from Build Settings.");
            }
        }

        // =====================================================================
        // Selection Helpers (Player count, stakes, regions)
        // =====================================================================
        private void SetPlayerCountSelection(int count) {
            selectedPlayerCount = count;
            btnPlayers2?.EnableInClassList("option-chip-btn--selected", count == 2);
            btnPlayers3?.EnableInClassList("option-chip-btn--selected", count == 3);
            btnPlayers4?.EnableInClassList("option-chip-btn--selected", count == 4);
        }

        private void SetStakeSelection(long stake) {
            selectedStake = stake;
            btnStake1k?.EnableInClassList("option-chip-btn--selected", stake == 1000);
            btnStake5k?.EnableInClassList("option-chip-btn--selected", stake == 5000);
            btnStake20k?.EnableInClassList("option-chip-btn--selected", stake == 20000);
            btnStake100k?.EnableInClassList("option-chip-btn--selected", stake == 100000);
        }

        private void SetRegionSelection(string region) {
            selectedRegion = region;
            btnRegionNa?.EnableInClassList("option-chip-btn--selected", region.StartsWith("NORTH", StringComparison.OrdinalIgnoreCase));
            btnRegionEu?.EnableInClassList("option-chip-btn--selected", region.StartsWith("EUR", StringComparison.OrdinalIgnoreCase));
            btnRegionAsia?.EnableInClassList("option-chip-btn--selected", region.StartsWith("ASIA", StringComparison.OrdinalIgnoreCase));

            if (footerServerLabel != null) {
                footerServerLabel.text = $"SERVER: {region}";
            }

            OnRegionChanged?.Invoke(region);
        }

        // =====================================================================
        // Latency Simulator
        // =====================================================================
        private IEnumerator SimulatePingRoutine() {
            var wait = new WaitForSeconds(3.5f);
            var rand = new System.Random();

            int minPing = config != null ? config.MinSimulatedPing : 18;
            int maxPing = config != null ? config.MaxSimulatedPing : 34;

            while (true) {
                yield return wait;
                int currentPing = rand.Next(minPing, maxPing + 1);
                if (latencyText != null) {
                    latencyText.text = $"{currentPing}ms";
                }
            }
        }
    }
}
