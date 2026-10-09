using System;
using Assets.Script.Data.Models;
using Assets.Script.Data.Repositories;
using Assets.Script.Data.Services;
using Assets.Script.Data.SO;
using Assets.Script.NetWorkScript;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu {
    /// <summary>
    /// uGUI controller managing the Minimal Arena Main Menu &amp; Lobby interface.
    /// This is the uGUI replacement for the previous UI Toolkit <c>MainMenuController</c>.
    ///
    /// Layout
    ///   Top-Left corner : player info (avatar, name, level, money)
    ///   Center          : play options (Quick Join, Host Table, Search Table)
    ///   Profile panel  : change display name &amp; avatar
    ///   Host panel     : table name, game type, stake (how much), player count, public/private
    ///
    /// All serialized references are optional: when they are not wired up in the scene the
    /// layout is generated at runtime by <see cref="MainMenuUILayoutBuilder"/>.
    /// </summary>
    public class MainMenuUIController : MonoBehaviour {
        // =====================================================================
        // Inspector References
        // =====================================================================
        [Header("Configuration")]
        [SerializeField] private MainMenuConfigSO config;
        [SerializeField] private AvatarDatabaseSO avatarDatabase;
        [SerializeField] private bool useMockIfUninjected = true;
        [SerializeField] private string defaultApiUrl = "http://localhost:5000/api";

        [Header("Optional Network Integration")]
        [SerializeField] private TienLenNetworkController networkController;

        // --- Player Info Panel (top-left corner) ---
        [Header("Player Info Panel (corner)")]
        [SerializeField] private GameObject playerInfoPanel;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text avatarInitialsText;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text playerLevelText;
        [SerializeField] private TMP_Text playerMoneyText;
        [SerializeField] private Button btnOpenProfile;

        // --- Center Play Options ---
        [Header("Center Play Options")]
        [SerializeField] private GameObject playOptionsPanel;
        [SerializeField] private Button btnQuickJoin;
        [SerializeField] private Button btnHostTable;
        [SerializeField] private Button btnSearchTable;

        // --- Profile Panel ---
        [Header("Profile Panel")]
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private TMP_InputField inputDisplayName;
        [SerializeField] private Button btnAvatarPrev;
        [SerializeField] private Button btnAvatarNext;
        [SerializeField] private Image profileAvatarPreview;
        [SerializeField] private TMP_Text profileAvatarNameText;
        [SerializeField] private Button btnSaveProfile;
        [SerializeField] private Button btnCancelProfile;

        // --- Host Table Panel ---
        [Header("Host Table Panel")]
        [SerializeField] private GameObject hostPanel;
        [SerializeField] private TMP_InputField inputTableName;
        [SerializeField] private TMP_Text stakeDisplayText;
        [SerializeField] private Button btnGameTypeCasual;
        [SerializeField] private Button btnGameTypeRanked;
        [SerializeField] private Button btnGameTypeFast;
        [SerializeField] private TMP_Text gameTypeText;
        [SerializeField] private Button btnStake1k;
        [SerializeField] private Button btnStake5k;
        [SerializeField] private Button btnStake20k;
        [SerializeField] private Button btnStake100k;
        [SerializeField] private Button btnPlayers2;
        [SerializeField] private Button btnPlayers3;
        [SerializeField] private Button btnPlayers4;
        [SerializeField] private ToggleSwitch togglePrivateRoom;
        [SerializeField] private Button btnConfirmHost;
        [SerializeField] private Button btnCancelHost;

        // --- Search Table Panel ---
        [Header("Search Table Panel")]
        [SerializeField] private GameObject searchPanel;
        [SerializeField] private TMP_InputField inputRoomCode;
        [SerializeField] private Button btnJoinByCode;
        [SerializeField] private Button btnCloseSearch;

        // --- Selection visuals ---
        [Header("Selection Visuals")]
        [SerializeField] private Color selectedColor = new Color(0.204f, 0.827f, 0.600f, 1f);
        [SerializeField] private Color normalColor = new Color(0.22f, 0.24f, 0.28f, 1f);

        // =====================================================================
        // Injected Services
        // =====================================================================
        [Inject] private IPlayerProfileService profileService;
        [Inject] private IAuthService authService;
        [Inject] private ILocalPlayerService localPlayerService;

        // =====================================================================
        // State
        // =====================================================================
        private static readonly string[] GameTypeNames = { "Casual", "Ranked", "Fast Play" };

        private int selectedPlayerCount = 4;
        private long selectedStake = 1000;
        private int selectedGameType = 0;
        private bool isPrivateRoom = false;
        private int previewAvatarId = 0;

        // =====================================================================
        // Public Events
        // =====================================================================
        public event Action OnQuickJoinRequested;
        public event Action<string, int, long, bool, string, bool> OnHostTableRequested;
        public event Action<string> OnJoinTableRequested;

        // =====================================================================
        // Unity Lifecycle
        // =====================================================================
        private void Awake() {
            if (networkController == null) {
                networkController = FindFirstObjectByType<TienLenNetworkController>();
            }

            RootLifeTimeScope.Ensure()?.InjectGameObject(gameObject);
            EnsureServices();

            // Generate the uGUI layout when nothing was wired in the scene/prefab.
            if (btnQuickJoin == null || btnHostTable == null || btnSearchTable == null) {
                BuildLayoutIfNeeded();
            }
        }

        private void BuildLayoutIfNeeded() {
            var existing = GetComponentInChildren<MainMenuUIRootMarker>(true);
            if (existing != null) return;

            var refs = MainMenuUILayoutBuilder.Build(this);
            if (refs != null) {
                ApplyLayout(refs);
            }
        }

        /// <summary>Wires a generated layout into this controller's serialized references.</summary>
        public void ApplyLayout(MainMenuUILayoutBuilder.Refs refs) {
            if (refs == null) return;

            playerInfoPanel = refs.playerInfoPanel;
            avatarImage = refs.avatarImage;
            avatarInitialsText = refs.avatarInitialsText;
            playerNameText = refs.playerNameText;
            playerLevelText = refs.playerLevelText;
            playerMoneyText = refs.playerMoneyText;
            btnOpenProfile = refs.btnOpenProfile;

            playOptionsPanel = refs.playOptionsPanel;
            btnQuickJoin = refs.btnQuickJoin;
            btnHostTable = refs.btnHostTable;
            btnSearchTable = refs.btnSearchTable;

            profilePanel = refs.profilePanel;
            inputDisplayName = refs.inputDisplayName;
            btnAvatarPrev = refs.btnAvatarPrev;
            btnAvatarNext = refs.btnAvatarNext;
            profileAvatarPreview = refs.profileAvatarPreview;
            profileAvatarNameText = refs.profileAvatarNameText;
            btnSaveProfile = refs.btnSaveProfile;
            btnCancelProfile = refs.btnCancelProfile;

            hostPanel = refs.hostPanel;
            inputTableName = refs.inputTableName;
            stakeDisplayText = refs.stakeDisplayText;
            btnGameTypeCasual = refs.btnGameTypeCasual;
            btnGameTypeRanked = refs.btnGameTypeRanked;
            btnGameTypeFast = refs.btnGameTypeFast;
            gameTypeText = refs.gameTypeText;
            btnStake1k = refs.btnStake1k;
            btnStake5k = refs.btnStake5k;
            btnStake20k = refs.btnStake20k;
            btnStake100k = refs.btnStake100k;
            btnPlayers2 = refs.btnPlayers2;
            btnPlayers3 = refs.btnPlayers3;
            btnPlayers4 = refs.btnPlayers4;
            togglePrivateRoom = refs.togglePrivateRoom;
            btnConfirmHost = refs.btnConfirmHost;
            btnCancelHost = refs.btnCancelHost;

            searchPanel = refs.searchPanel;
            inputRoomCode = refs.inputRoomCode;
            btnJoinByCode = refs.btnJoinByCode;
            btnCloseSearch = refs.btnCloseSearch;
        }

        private void OnEnable() {
            BindButtons();
            SubscribeToProfileChanges();
        }

        private void OnDisable() {
            UnsubscribeFromProfileChanges();
        }

        private async void Start() {
            CloseAllPanels();
            ApplyDefaults();

            if (profileService != null) {
                try {
                    await profileService.RefreshProfileAsync();
                    localPlayerService?.SetProfile(profileService.CachedProfile);
                    UpdatePlayerInfoDisplay(profileService.CachedProfile);
                } catch (Exception ex) {
                    Debug.LogWarning($"[MainMenuUI] Failed to refresh profile: {ex.Message}");
                }
            }
        }

        // =====================================================================
        // Service bootstrap (same rules as the original controller)
        // =====================================================================
        private void EnsureServices() {
            var root = RootLifeTimeScope.Ensure();
            if (profileService == null && root != null &&
                root.TryResolve(out IPlayerProfileService resolvedProfile)) {
                profileService = resolvedProfile;
                root.TryResolve(out IAuthService resolvedAuth);
                root.TryResolve(out ILocalPlayerService resolvedLocal);
                authService ??= resolvedAuth;
                localPlayerService ??= resolvedLocal;
            }

            if (profileService == null) {
                IPlayerRepository repo = useMockIfUninjected
                    ? new MockPlayerRepository()
                    : new RestPlayerRepository(defaultApiUrl);

                authService ??= new AuthService(repo);
                profileService = new PlayerProfileService(repo, authService);
            }

            if (localPlayerService == null) {
                localPlayerService = new LocalPlayerService(profileService);
            }
        }

        // =====================================================================
        // Button binding
        // =====================================================================
        private void BindButtons() {
            SafeBind(btnQuickJoin, HandleQuickJoin);
            SafeBind(btnHostTable, OpenHostPanel);
            SafeBind(btnSearchTable, OpenSearchPanel);
            SafeBind(btnOpenProfile, OpenProfilePanel);

            // Profile panel
            SafeBind(btnAvatarPrev, HandleAvatarPrev);
            SafeBind(btnAvatarNext, HandleAvatarNext);
            SafeBind(btnSaveProfile, HandleSaveProfile);
            SafeBind(btnCancelProfile, CloseProfilePanel);

            // Host panel
            SafeBind(btnGameTypeCasual, () => SetGameTypeSelection(0));
            SafeBind(btnGameTypeRanked, () => SetGameTypeSelection(1));
            SafeBind(btnGameTypeFast, () => SetGameTypeSelection(2));
            SafeBind(btnStake1k, () => SetStakeSelection(1000));
            SafeBind(btnStake5k, () => SetStakeSelection(5000));
            SafeBind(btnStake20k, () => SetStakeSelection(20000));
            SafeBind(btnStake100k, () => SetStakeSelection(100000));
            SafeBind(btnPlayers2, () => SetPlayerCountSelection(2));
            SafeBind(btnPlayers3, () => SetPlayerCountSelection(3));
            SafeBind(btnPlayers4, () => SetPlayerCountSelection(4));
            SafeBind(btnConfirmHost, HandleConfirmHost);
            SafeBind(btnCancelHost, CloseHostPanel);

            if (togglePrivateRoom != null) {
                togglePrivateRoom.OnValueChanged -= HandlePrivateRoomChanged;
                togglePrivateRoom.OnValueChanged += HandlePrivateRoomChanged;
            }

            // Search panel
            SafeBind(btnJoinByCode, HandleJoinByCode);
            SafeBind(btnCloseSearch, CloseSearchPanel);
        }

        private static void SafeBind(Button btn, Action action) {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => action());
        }

        // =====================================================================
        // Profile change subscriptions
        // =====================================================================
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

        // =====================================================================
        // Display updates
        // =====================================================================
        private void UpdatePlayerInfoDisplay(PlayerProfileData profile) {
            if (profile == null) return;

            if (playerNameText != null && !string.IsNullOrEmpty(profile.displayName)) {
                playerNameText.text = profile.displayName;

                if (avatarInitialsText != null) {
                    avatarInitialsText.text = profile.displayName.Length > 2
                        ? profile.displayName.Substring(0, 2).ToUpperInvariant()
                        : profile.displayName.ToUpperInvariant();
                }
            }

            if (playerLevelText != null) {
                playerLevelText.text = $"LV {Mathf.Max(1, profile.level)}";
            }

            if (playerMoneyText != null) {
                playerMoneyText.text = $"{profile.money:N0} CR";
            }

            ApplyAvatar(avatarImage, profile.avatarId);
        }

        private void ApplyAvatar(Image target, int avatarId) {
            if (target == null) return;

            if (avatarDatabase != null &&
                avatarDatabase.TryGetAvatarSprite(avatarId, out Sprite sprite) && sprite != null) {
                target.sprite = sprite;
                target.color = Color.white;
            } else {
                target.sprite = null;
                target.color = new Color(0.16f, 0.17f, 0.20f, 1f);
            }
        }

        private void HandleMoneyChanged(long newMoney) {
            if (playerMoneyText != null) playerMoneyText.text = $"{newMoney:N0} CR";
        }

        private void HandleLevelOrExpChanged(int level, int exp) {
            if (playerLevelText != null) playerLevelText.text = $"LV {level}";
        }

        private void HandleDisplayNameChanged(string newName) {
            if (playerNameText != null) playerNameText.text = newName;
            if (avatarInitialsText != null && !string.IsNullOrEmpty(newName)) {
                avatarInitialsText.text = newName.Length > 2
                    ? newName.Substring(0, 2).ToUpperInvariant()
                    : newName.ToUpperInvariant();
            }
        }

        private void HandleAvatarChanged(int newAvatarId) {
            ApplyAvatar(avatarImage, newAvatarId);
            if (previewAvatarId != newAvatarId && profileService?.CachedProfile != null &&
                newAvatarId == profileService.CachedProfile.avatarId) {
                previewAvatarId = newAvatarId;
            }
        }

        // =====================================================================
        // Defaults
        // =====================================================================
        private void ApplyDefaults() {
            SetPlayerCountSelection(config != null ? config.DefaultPlayerCount : 4);
            SetStakeSelection(config != null ? config.DefaultStake : 1000);
            SetGameTypeSelection(0);
            SetPrivateRoom(false);

            if (inputTableName != null) {
                inputTableName.text = config != null ? config.DefaultRoomName : "ArenaTable";
            }

            if (playerNameText != null) playerNameText.text = "PLAYER_01";
            if (playerLevelText != null) playerLevelText.text = "LV 1";
            if (playerMoneyText != null) playerMoneyText.text = "0 CR";
            if (avatarInitialsText != null) avatarInitialsText.text = "P1";
        }

        // =====================================================================
        // Panel management
        // =====================================================================
        private void SetActive(GameObject panel, bool active) {
            if (panel != null) panel.SetActive(active);
        }

        private void CloseAllPanels() {
            SetActive(profilePanel, false);
            SetActive(hostPanel, false);
            SetActive(searchPanel, false);
        }

        private void OpenHostPanel() {
            CloseAllPanels();
            SetActive(hostPanel, true);
        }

        private void CloseHostPanel() {
            SetActive(hostPanel, false);
        }

        private void OpenSearchPanel() {
            CloseAllPanels();
            SetActive(searchPanel, true);
        }

        private void CloseSearchPanel() {
            SetActive(searchPanel, false);
        }

        private void OpenProfilePanel() {
            CloseAllPanels();
            SetActive(profilePanel, true);

            var profile = profileService?.CachedProfile;
            if (profile != null) {
                if (inputDisplayName != null) inputDisplayName.text = profile.displayName ?? string.Empty;
                previewAvatarId = profile.avatarId;
            } else {
                if (inputDisplayName != null) inputDisplayName.text = string.Empty;
            }

            UpdateAvatarPreview();
        }

        private void CloseProfilePanel() {
            SetActive(profilePanel, false);
        }

        // =====================================================================
        // Play actions
        // =====================================================================
        private void HandleQuickJoin() {
            Debug.Log("[MainMenuUI] Quick Join requested. Matching with an active table...");
            OnQuickJoinRequested?.Invoke();

            if (networkController != null) {
                networkController.JoinRoom("QuickMatch");
            } else {
                LoadGameplayScene();
            }
        }

        private void HandleConfirmHost() {
            string roomName = inputTableName != null && !string.IsNullOrEmpty(inputTableName.text)
                ? inputTableName.text
                : (config != null ? config.DefaultRoomName : "ArenaTable");

            string gameType = GameTypeNames[Mathf.Clamp(selectedGameType, 0, GameTypeNames.Length - 1)];

            Debug.Log($"[MainMenuUI] Hosting '{roomName}' | Game: {gameType} | Players: {selectedPlayerCount} | " +
                      $"Stake: {selectedStake} CR | Private: {isPrivateRoom}");

            CloseHostPanel();
            OnHostTableRequested?.Invoke(roomName, selectedPlayerCount, selectedStake, isPrivateRoom, gameType, true);

            if (networkController != null) {
                networkController.HostRoom(roomName, selectedPlayerCount);
            } else {
                LoadGameplayScene();
            }
        }

        private void HandleJoinByCode() {
            string code = inputRoomCode != null ? inputRoomCode.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(code)) {
                Debug.LogWarning("[MainMenuUI] Please enter a valid room code.");
                return;
            }

            Debug.Log($"[MainMenuUI] Joining room '{code}'...");
            CloseSearchPanel();
            OnJoinTableRequested?.Invoke(code);

            if (networkController != null) {
                networkController.JoinRoom(code);
            } else {
                LoadGameplayScene();
            }
        }

        // =====================================================================
        // Selection helpers
        // =====================================================================
        private void SetPlayerCountSelection(int count) {
            selectedPlayerCount = count;
            Highlight(btnPlayers2, count == 2);
            Highlight(btnPlayers3, count == 3);
            Highlight(btnPlayers4, count == 4);
        }

        private void SetStakeSelection(long stake) {
            selectedStake = stake;
            Highlight(btnStake1k, stake == 1000);
            Highlight(btnStake5k, stake == 5000);
            Highlight(btnStake20k, stake == 20000);
            Highlight(btnStake100k, stake == 100000);

            if (stakeDisplayText != null) stakeDisplayText.text = $"{stake:N0} CR";
        }

        private void SetGameTypeSelection(int index) {
            selectedGameType = Mathf.Clamp(index, 0, GameTypeNames.Length - 1);
            Highlight(btnGameTypeCasual, selectedGameType == 0);
            Highlight(btnGameTypeRanked, selectedGameType == 1);
            Highlight(btnGameTypeFast, selectedGameType == 2);

            if (gameTypeText != null) gameTypeText.text = GameTypeNames[selectedGameType];
        }

        private void SetPrivateRoom(bool isPrivate) {
            isPrivateRoom = isPrivate;
            if (togglePrivateRoom != null && togglePrivateRoom.IsOn != isPrivate) {
                togglePrivateRoom.SetValue(isPrivate, false);
            }
        }

        private void HandlePrivateRoomChanged(bool isPrivate) {
            isPrivateRoom = isPrivate;
        }

        private void Highlight(Button btn, bool selected) {
            if (btn == null) return;
            var colors = btn.colors;
            colors.normalColor = selected ? selectedColor : normalColor;
            colors.highlightedColor = selected ? selectedColor : new Color(0.32f, 0.35f, 0.40f, 1f);
            btn.colors = colors;
        }

        // =====================================================================
        // Profile editing
        // =====================================================================
        private void HandleAvatarPrev() {
            if (avatarDatabase == null || avatarDatabase.TotalAvatars == 0) return;
            previewAvatarId = (previewAvatarId - 1 + avatarDatabase.TotalAvatars) % avatarDatabase.TotalAvatars;
            UpdateAvatarPreview();
        }

        private void HandleAvatarNext() {
            if (avatarDatabase == null || avatarDatabase.TotalAvatars == 0) return;
            previewAvatarId = (previewAvatarId + 1) % avatarDatabase.TotalAvatars;
            UpdateAvatarPreview();
        }

        private void UpdateAvatarPreview() {
            ApplyAvatar(profileAvatarPreview, previewAvatarId);

            if (profileAvatarNameText != null) {
                profileAvatarNameText.text = avatarDatabase != null
                    ? $"AVATAR #{previewAvatarId}"
                    : $"AVATAR #{previewAvatarId}";
            }
        }

        private async void HandleSaveProfile() {
            if (profileService == null) {
                Debug.LogWarning("[MainMenuUI] No profile service available to save.");
                CloseProfilePanel();
                return;
            }

            string newName = inputDisplayName != null ? inputDisplayName.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(newName)) {
                Debug.LogWarning("[MainMenuUI] Display name cannot be empty.");
                return;
            }

            bool success = false;
            try {
                success = await profileService.UpdateProfileAsync(newName, previewAvatarId);
            } catch (Exception ex) {
                Debug.LogWarning($"[MainMenuUI] Profile update threw: {ex.Message}");
            }

            if (success) {
                Debug.Log($"[MainMenuUI] Profile updated: '{newName}', avatar #{previewAvatarId}");
                try {
                    await profileService.RefreshProfileAsync();
                } catch (Exception ex) {
                    Debug.LogWarning($"[MainMenuUI] Profile refresh threw: {ex.Message}");
                }

                localPlayerService?.SetProfile(profileService.CachedProfile);
                UpdatePlayerInfoDisplay(profileService.CachedProfile);
            } else {
                Debug.LogWarning("[MainMenuUI] Failed to update profile on the server; local preview only.");
                if (playerNameText != null) playerNameText.text = newName;
            }

            CloseProfilePanel();
        }

        // =====================================================================
        // Scene loading
        // =====================================================================
        private void LoadGameplayScene() {
            string targetScene = config != null ? config.GameplaySceneName : "GameScene";
            if (!string.IsNullOrEmpty(targetScene) && Application.CanStreamedLevelBeLoaded(targetScene)) {
                SceneManager.LoadScene(targetScene);
            } else {
                Debug.LogWarning($"[MainMenuUI] Scene '{targetScene}' cannot be loaded or is missing from Build Settings.");
            }
        }
    }
}
