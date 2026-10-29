using Assets.Script.Data.Services;
using Assets.Script.Data.SO;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu.Panel {
    /// <summary>
    /// Minimal view controller for the Player Profile panel.
    /// Handles profile preview, name editing, avatar selection dialog, and backend synchronization.
    /// </summary>
    public class PlayerProfilePanelView : MonoBehaviour {
        [Header("Dependencies")]
        [Inject] private IPlayerProfileService playerProfileService;
        [Inject] private AvatarDatabaseSO avatarDatabase;

        [Header("Preview Section")]
        [SerializeField] private Image previewAvatar;
        [SerializeField] private TextMeshProUGUI previewName;
        [SerializeField] private TextMeshProUGUI previewLevel;
        [SerializeField] private TextMeshProUGUI previewMoney;

        [Header("Name Editing")]
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button saveNameButton;

        [Header("Avatar Selection Dialog")]
        [SerializeField] private Button openAvatarPanelButton;
        [SerializeField] private GameObject avatarSelectionPanel;
        [SerializeField] private Transform avatarContainer;
        [SerializeField] private Button avatarButtonPrefab;

        [Header("Loading & Navigation")]
        [SerializeField] private GameObject loadingIndicator;

        private bool avatarsPopulated = false;

        private void OnEnable() {
            // Subscribe UI events
            if ( saveNameButton != null ) saveNameButton.onClick.AddListener(OnSaveNameClicked);
            if ( openAvatarPanelButton != null ) openAvatarPanelButton.onClick.AddListener(OpenAvatarPanel);

            // Subscribe to profile updates
            if ( playerProfileService != null ) {
                playerProfileService.OnDisplayNameChanged += OnProfileChanged;
                playerProfileService.OnAvatarChanged += OnAvatarChanged;
            }

            RefreshPreview();
        }

        private void OnDisable() {
            // Unsubscribe UI events
            if ( saveNameButton != null ) saveNameButton.onClick.RemoveListener(OnSaveNameClicked);
            if ( openAvatarPanelButton != null ) openAvatarPanelButton.onClick.RemoveListener(OpenAvatarPanel);

            // Unsubscribe from profile updates
            if ( playerProfileService != null ) {
                playerProfileService.OnDisplayNameChanged -= OnProfileChanged;
                playerProfileService.OnAvatarChanged -= OnAvatarChanged;
            }
        }

        /// <summary>
        /// Refreshes the preview elements from the currently cached player profile.
        /// </summary>
        public void RefreshPreview() {
            var profile = playerProfileService?.CachedProfile;
            if ( profile == null ) return;

            if ( previewName != null ) previewName.text = profile.displayName;
            if ( nameInputField != null ) nameInputField.text = profile.displayName;
            if ( previewLevel != null ) previewLevel.text = $"LV: {profile.level}";
            if ( previewMoney != null ) previewMoney.text = $"{profile.money} $";

            UpdateAvatarPreview(profile.avatarId);
        }

        private void UpdateAvatarPreview( int avatarId ) {
            if ( previewAvatar != null && avatarDatabase != null && avatarDatabase.TryGetAvatarSprite(avatarId, out Sprite sprite) ) {
                previewAvatar.sprite = sprite;
            }
        }

        private void OnProfileChanged( string newName ) => RefreshPreview();
        private void OnAvatarChanged( int newAvatarId ) => UpdateAvatarPreview(newAvatarId);

        private void OpenAvatarPanel() {
            if ( avatarSelectionPanel != null ) {
                avatarSelectionPanel.SetActive(true);
            }
            PopulateAvatarsIfNeeded();
        }

        private void CloseAvatarPanel() {
            if ( avatarSelectionPanel != null ) {
                avatarSelectionPanel.SetActive(false);
            }
        }

        private void ClosePanel() {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Populates generic Button instances inside avatarContainer using the AvatarDatabaseSO.
        /// </summary>
        private void PopulateAvatarsIfNeeded() {
            if ( avatarsPopulated || avatarDatabase == null || avatarContainer == null || avatarButtonPrefab == null ) {
                return;
            }

            var avatars = avatarDatabase.Avatars;
            if ( avatars == null ) return;

            for ( int i = 0; i < avatars.Count; i++ ) {
                var entry = avatars[i];
                Button btn = Instantiate(avatarButtonPrefab, avatarContainer);
                btn.gameObject.SetActive(true);

                // Configure avatar icon on the button (target Graphic or child Image)
                Image img = btn.GetComponentInChildren<Image>();
                if ( img != null && entry.avatarSprite != null ) {
                    img.sprite = entry.avatarSprite;
                }

                int selectedId = entry.avatarId;
                btn.onClick.AddListener(() => OnAvatarSelected(selectedId));
            }

            avatarsPopulated = true;
        }

        private void OnSaveNameClicked() {
            if ( nameInputField == null || playerProfileService?.CachedProfile == null ) return;

            string enteredName = nameInputField.text.Trim();
            if ( string.IsNullOrEmpty(enteredName) || enteredName == playerProfileService.CachedProfile.displayName ) {
                return;
            }

            UpdateProfileBackend(enteredName, playerProfileService.CachedProfile.avatarId).Forget();
        }

        private void OnAvatarSelected( int selectedAvatarId ) {
            CloseAvatarPanel();

            if ( playerProfileService?.CachedProfile == null ) return;
            if ( selectedAvatarId == playerProfileService.CachedProfile.avatarId ) return;

            UpdateProfileBackend(playerProfileService.CachedProfile.displayName, selectedAvatarId).Forget();
        }

        /// <summary>
        /// Sends the profile update to the backend service while toggling the loading indicator.
        /// </summary>
        private async UniTaskVoid UpdateProfileBackend( string newDisplayName, int newAvatarId ) {
            if ( playerProfileService == null ) return;

            if ( loadingIndicator != null ) loadingIndicator.SetActive(true);

            try {
                bool success = await playerProfileService.UpdateProfileAsync(newDisplayName, newAvatarId);
                if ( success ) {
                    RefreshPreview();
                }
            } finally {
                if ( loadingIndicator != null ) loadingIndicator.SetActive(false);
            }
        }
    }
}
