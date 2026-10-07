using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Components {
    /// <summary>
    /// Reusable uGUI Social Authentication Button Component.
    /// Manages provider branding, labels, icons/badges, and click events.
    /// </summary>
    public class SocialLoginButtonUGUI : MonoBehaviour {
        [Header("UI Elements")]
        [SerializeField] private Button button;
        [SerializeField] private Image buttonBg;
        [SerializeField] private Image buttonBorder;
        [SerializeField] private TextMeshProUGUI providerLabel;
        [SerializeField] private TextMeshProUGUI iconBadgeText;
        [SerializeField] private Image iconImage;

        [Header("Configuration")]
        [SerializeField] private SocialAuthProvider provider = SocialAuthProvider.Google;
        [SerializeField] private string customProviderName = string.Empty;

        public event Action<SocialAuthProvider, string> OnClicked;

        public SocialAuthProvider Provider {
            get => provider;
            set {
                provider = value;
                ApplyProviderDefaults();
            }
        }

        public string CustomProviderName {
            get => customProviderName;
            set {
                customProviderName = value;
                ApplyProviderDefaults();
            }
        }

        public Button Button => button;
        public Image ButtonBg => buttonBg;
        public Image ButtonBorder => buttonBorder;
        public TextMeshProUGUI ProviderLabel => providerLabel;
        public TextMeshProUGUI IconBadgeText => iconBadgeText;

        private void Awake() {
            if (button == null) {
                button = GetComponent<Button>();
            }

            if (button != null) {
                button.onClick.AddListener(HandleClicked);
            }

            ApplyProviderDefaults();
        }

        private void OnDestroy() {
            if (button != null) {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void SetProvider(SocialAuthProvider authProvider, string label = null) {
            provider = authProvider;
            if (!string.IsNullOrEmpty(label)) {
                customProviderName = label;
            }
            ApplyProviderDefaults();
        }

        public void ApplyProviderDefaults() {
            string labelText = "Google";
            string badge = "G";

            switch (provider) {
                case SocialAuthProvider.Google:
                    labelText = "Google";
                    badge = "G";
                    break;
                case SocialAuthProvider.Facebook:
                    labelText = "Facebook";
                    badge = "f";
                    break;
                case SocialAuthProvider.Apple:
                    labelText = "Apple";
                    badge = "";
                    break;
                case SocialAuthProvider.Discord:
                    labelText = "Discord";
                    badge = "D";
                    break;
                case SocialAuthProvider.Custom:
                    labelText = string.IsNullOrEmpty(customProviderName) ? "Sign In" : customProviderName;
                    badge = "★";
                    break;
            }

            if (providerLabel != null) {
                providerLabel.text = labelText;
            }

            if (iconBadgeText != null) {
                iconBadgeText.text = badge;
            }
        }

        public void ApplyThemeColors(Color bgColor, Color borderColor, Color textColor) {
            if (buttonBg != null) buttonBg.color = bgColor;
            if (buttonBorder != null) buttonBorder.color = borderColor;
            if (providerLabel != null) providerLabel.color = textColor;
            if (iconBadgeText != null) iconBadgeText.color = textColor;
        }

        private void HandleClicked() {
            string effectiveName = (provider == SocialAuthProvider.Custom && !string.IsNullOrEmpty(customProviderName))
                ? customProviderName
                : (providerLabel != null ? providerLabel.text : provider.ToString());

            OnClicked?.Invoke(provider, effectiveName);
        }
    }
}
