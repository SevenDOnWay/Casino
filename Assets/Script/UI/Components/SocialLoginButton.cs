using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Script.UI.Components {
    public enum SocialAuthProvider {
        Google,
        Facebook,
        Apple,
        Discord,
        Custom
    }

    /// <summary>
    /// Reusable UI Toolkit Social Authentication Button Component.
    /// Can be used as a custom UXML element or instantiated programmatically.
    /// </summary>
    [UxmlElement]
    public partial class SocialLoginButton : Button {
        private VisualElement iconContainer;
        private Label iconBadgeLabel;
        private Label labelElement;

        private SocialAuthProvider provider = SocialAuthProvider.Google;
        private string customProviderName = string.Empty;
        private string providerLabel = "Google";
        private string iconBadge = "G";

        public event Action<SocialAuthProvider, string> OnSocialButtonClicked;

        [UxmlAttribute]
        public SocialAuthProvider Provider {
            get => provider;
            set {
                provider = value;
                ApplyProviderDefaults();
                UpdateVisuals();
            }
        }

        [UxmlAttribute]
        public string CustomProviderName {
            get => customProviderName;
            set {
                customProviderName = value;
                UpdateVisuals();
            }
        }

        [UxmlAttribute]
        public string ProviderLabel {
            get => providerLabel;
            set {
                providerLabel = value;
                UpdateVisuals();
            }
        }

        [UxmlAttribute]
        public string IconBadge {
            get => iconBadge;
            set {
                iconBadge = value;
                UpdateVisuals();
            }
        }

        public SocialLoginButton() {
            AddToClassList("social-btn");
            BuildLayout();
            ApplyProviderDefaults();
            UpdateVisuals();

            clicked += HandleClicked;
        }

        public SocialLoginButton(SocialAuthProvider authProvider, string label = null) : this() {
            Provider = authProvider;
            if (!string.IsNullOrEmpty(label)) {
                ProviderLabel = label;
            }
        }

        private void BuildLayout() {
            iconContainer = new VisualElement { name = "social-btn__icon-container" };
            iconContainer.AddToClassList("social-btn__icon-container");

            iconBadgeLabel = new Label { name = "social-btn__icon-badge" };
            iconBadgeLabel.AddToClassList("social-btn__icon-badge");
            iconContainer.Add(iconBadgeLabel);

            labelElement = new Label { name = "social-btn__label" };
            labelElement.AddToClassList("social-btn__label");

            Add(iconContainer);
            Add(labelElement);
        }

        private void ApplyProviderDefaults() {
            switch (provider) {
                case SocialAuthProvider.Google:
                    providerLabel = "Google";
                    iconBadge = "G";
                    break;
                case SocialAuthProvider.Facebook:
                    providerLabel = "Facebook";
                    iconBadge = "f";
                    break;
                case SocialAuthProvider.Apple:
                    providerLabel = "Apple";
                    iconBadge = "";
                    break;
                case SocialAuthProvider.Discord:
                    providerLabel = "Discord";
                    iconBadge = "D";
                    break;
                case SocialAuthProvider.Custom:
                    if (string.IsNullOrEmpty(providerLabel)) providerLabel = "Sign In";
                    if (string.IsNullOrEmpty(iconBadge)) iconBadge = "★";
                    break;
            }
        }

        private void UpdateVisuals() {
            if (labelElement != null) {
                labelElement.text = providerLabel;
            }

            if (iconBadgeLabel != null) {
                iconBadgeLabel.text = iconBadge;
            }

            RemoveFromClassList("social-btn--google");
            RemoveFromClassList("social-btn--facebook");
            RemoveFromClassList("social-btn--apple");
            RemoveFromClassList("social-btn--discord");

            switch (provider) {
                case SocialAuthProvider.Google:
                    AddToClassList("social-btn--google");
                    break;
                case SocialAuthProvider.Facebook:
                    AddToClassList("social-btn--facebook");
                    break;
                case SocialAuthProvider.Apple:
                    AddToClassList("social-btn--apple");
                    break;
                case SocialAuthProvider.Discord:
                    AddToClassList("social-btn--discord");
                    break;
            }
        }

        private void HandleClicked() {
            string effectiveName = provider == SocialAuthProvider.Custom && !string.IsNullOrEmpty(customProviderName)
                ? customProviderName
                : providerLabel;

            OnSocialButtonClicked?.Invoke(provider, effectiveName);
        }

        /// <summary>
        /// Helper to bind and wrap an existing template instance from UXML.
        /// </summary>
        public static void BindTemplateInstance(
            VisualElement rootElement,
            string elementName,
            SocialAuthProvider provider,
            Action<SocialAuthProvider, string> onClicked = null) {
            
            var btnContainer = rootElement?.Q<VisualElement>(elementName);
            if (btnContainer == null) return;

            var button = btnContainer as Button ?? btnContainer.Q<Button>("social-btn");
            var label = btnContainer.Q<Label>("social-btn__label");
            var iconBadge = btnContainer.Q<Label>("social-btn__icon-badge");

            if (button != null) {
                button.RemoveFromClassList("social-btn--google");
                button.RemoveFromClassList("social-btn--facebook");
                button.RemoveFromClassList("social-btn--apple");

                string badgeText = "★";
                string labelText = provider.ToString();

                switch (provider) {
                    case SocialAuthProvider.Google:
                        button.AddToClassList("social-btn--google");
                        badgeText = "G";
                        labelText = "Google";
                        break;
                    case SocialAuthProvider.Facebook:
                        button.AddToClassList("social-btn--facebook");
                        badgeText = "f";
                        labelText = "Facebook";
                        break;
                    case SocialAuthProvider.Apple:
                        button.AddToClassList("social-btn--apple");
                        badgeText = "";
                        labelText = "Apple";
                        break;
                }

                if (label != null) label.text = labelText;
                if (iconBadge != null) iconBadge.text = badgeText;

                button.clicked += () => {
                    onClicked?.Invoke(provider, labelText);
                };
            }
        }
    }
}
