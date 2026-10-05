using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Script.UI.Components {
    public enum LobbyButtonVariant {
        Primary,   // Solid Pale / White (High visual hierarchy, Quick Join)
        Outline,   // Dark Zinc Monochrome Outline (Host, Search)
        Muted      // Subtle outline with muted text (Practice, Free)
    }

    /// <summary>
    /// Reusable UI Toolkit Lobby Menu Button Component matching the Minimal Arena design system.
    /// Supports Primary, Outline, and Muted variants, title, subtitle, icon glyph, badge text, and chevron.
    /// Fully responsive and tactile on PC and Mobile.
    /// </summary>
    [UxmlElement]
    public partial class LobbyActionButton : Button {
        // UI Sub-elements
        private VisualElement leftContainer;
        private VisualElement iconBox;
        private Label iconLabel;
        private VisualElement textContainer;
        private Label titleLabel;
        private Label subtitleLabel;
        private VisualElement rightContainer;
        private Label badgeLabel;
        private Label chevronLabel;

        // Serialized / Backing Properties
        private LobbyButtonVariant variant = LobbyButtonVariant.Outline;
        private string title = "Action Title";
        private string subtitle = "Description text";
        private string iconGlyph = "⚡";
        private string badgeText = string.Empty;
        private bool showChevron = true;

        [UxmlAttribute]
        public LobbyButtonVariant Variant {
            get => variant;
            set {
                variant = value;
                UpdateVariantStyle();
            }
        }

        [UxmlAttribute]
        public string Title {
            get => title;
            set {
                title = value;
                if (titleLabel != null) titleLabel.text = value;
            }
        }

        [UxmlAttribute]
        public string Subtitle {
            get => subtitle;
            set {
                subtitle = value;
                if (subtitleLabel != null) {
                    subtitleLabel.text = value;
                    subtitleLabel.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }
        }

        [UxmlAttribute]
        public string IconGlyph {
            get => iconGlyph;
            set {
                iconGlyph = value;
                if (iconLabel != null) iconLabel.text = value;
            }
        }

        [UxmlAttribute]
        public string BadgeText {
            get => badgeText;
            set {
                badgeText = value;
                if (badgeLabel != null) {
                    badgeLabel.text = value;
                    badgeLabel.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }
        }

        [UxmlAttribute]
        public bool ShowChevron {
            get => showChevron;
            set {
                showChevron = value;
                if (chevronLabel != null) {
                    chevronLabel.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        public LobbyActionButton() {
            AddToClassList("lobby-menu-btn");
            BuildHierarchy();
            UpdateVariantStyle();
            UpdateAllText();

            // Tactile micro-interactions for pointer
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        private void BuildHierarchy() {
            // Left block (Icon + Titles)
            leftContainer = new VisualElement { name = "lobby-btn-left" };
            leftContainer.AddToClassList("lobby-btn-left");

            iconBox = new VisualElement { name = "lobby-btn-icon-box" };
            iconBox.AddToClassList("lobby-btn-icon-box");

            iconLabel = new Label { name = "lobby-btn-icon" };
            iconLabel.AddToClassList("lobby-btn-icon");
            iconBox.Add(iconLabel);
            leftContainer.Add(iconBox);

            textContainer = new VisualElement { name = "lobby-btn-text-group" };
            textContainer.AddToClassList("lobby-btn-text-group");

            titleLabel = new Label { name = "lobby-btn-title" };
            titleLabel.AddToClassList("lobby-btn-title");
            textContainer.Add(titleLabel);

            subtitleLabel = new Label { name = "lobby-btn-subtitle" };
            subtitleLabel.AddToClassList("lobby-btn-subtitle");
            textContainer.Add(subtitleLabel);

            leftContainer.Add(textContainer);
            Add(leftContainer);

            // Right block (Badge + Chevron)
            rightContainer = new VisualElement { name = "lobby-btn-right" };
            rightContainer.AddToClassList("lobby-btn-right");

            badgeLabel = new Label { name = "lobby-btn-badge" };
            badgeLabel.AddToClassList("lobby-btn-badge");
            rightContainer.Add(badgeLabel);

            chevronLabel = new Label { name = "lobby-btn-chevron", text = "→" };
            chevronLabel.AddToClassList("lobby-btn-chevron");
            rightContainer.Add(chevronLabel);

            Add(rightContainer);
        }

        private void UpdateVariantStyle() {
            RemoveFromClassList("lobby-menu-btn--primary");
            RemoveFromClassList("lobby-menu-btn--outline");
            RemoveFromClassList("lobby-menu-btn--muted");

            switch (variant) {
                case LobbyButtonVariant.Primary:
                    AddToClassList("lobby-menu-btn--primary");
                    break;
                case LobbyButtonVariant.Outline:
                    AddToClassList("lobby-menu-btn--outline");
                    break;
                case LobbyButtonVariant.Muted:
                    AddToClassList("lobby-menu-btn--muted");
                    break;
            }
        }

        private void UpdateAllText() {
            if (titleLabel != null) titleLabel.text = title;
            if (subtitleLabel != null) {
                subtitleLabel.text = subtitle;
                subtitleLabel.style.display = string.IsNullOrEmpty(subtitle) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (iconLabel != null) iconLabel.text = iconGlyph;
            if (badgeLabel != null) {
                badgeLabel.text = badgeText;
                badgeLabel.style.display = string.IsNullOrEmpty(badgeText) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (chevronLabel != null) {
                chevronLabel.style.display = showChevron ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnPointerDown(PointerDownEvent evt) {
            AddToClassList("lobby-menu-btn--active");
        }

        private void OnPointerUp(PointerUpEvent evt) {
            RemoveFromClassList("lobby-menu-btn--active");
        }

        private void OnPointerCancel(PointerCancelEvent evt) {
            RemoveFromClassList("lobby-menu-btn--active");
        }
    }
}
