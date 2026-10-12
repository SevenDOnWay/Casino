using UnityEngine;

namespace Assets.Script.UI.SO {
    /// <summary>
    /// ScriptableObject defining color palettes for Login UI (Dark / Light).
    /// </summary>
    [CreateAssetMenu(fileName = "LoginThemePalette", menuName = "UI/Login Theme Palette")]
    public class LoginThemePaletteSO : ScriptableObject {
        public bool isDarkTheme = true;

        [Header("Screen & Card")]
        public Color screenBackgroundColor = new Color32(9, 9, 11, 255);
        public Color cardBackgroundColor = new Color32(24, 24, 27, 245);
        public Color cardBorderColor = new Color(1f, 1f, 1f, 0.08f);

        [Header("Typography")]
        public Color titleTextColor = new Color32(244, 244, 245, 255);
        public Color subtitleTextColor = new Color32(161, 161, 170, 255);
        public Color labelTextColor = new Color32(228, 228, 231, 255);
        public Color secondaryTextColor = new Color32(161, 161, 170, 255);

        [Header("Input Fields")]
        public Color inputBackgroundColor = new Color(0.035f, 0.035f, 0.043f, 0.7f);
        public Color inputBorderColor = new Color(1f, 1f, 1f, 0.12f);
        public Color inputFocusedBorderColor = new Color32(244, 244, 245, 255);
        public Color inputTextColor = Color.white;
        public Color inputPlaceholderColor = new Color(1f, 1f, 1f, 0.4f);

        [Header("Primary Action (Submit)")]
        public Color submitButtonBg = new Color32(244, 244, 245, 255);
        public Color submitButtonText = new Color32(9, 9, 11, 255);

        [Header("Social Buttons")]
        public Color socialButtonBg = new Color(0.094f, 0.094f, 0.106f, 0.8f);
        public Color socialButtonBorder = new Color(1f, 1f, 1f, 0.12f);
        public Color socialButtonText = new Color32(244, 244, 245, 255);

        [Header("Dividers & Badges")]
        public Color dividerLineColor = new Color(1f, 1f, 1f, 0.1f);
        public Color dividerBadgeBg = new Color32(24, 24, 27, 255);
        public Color dividerBadgeText = new Color32(113, 113, 122, 255);

        [Header("Alerts")]
        public Color alertErrorBg = new Color(0.957f, 0.247f, 0.369f, 0.15f);
        public Color alertErrorText = new Color32(253, 164, 175, 255);
        public Color alertSuccessBg = new Color(0.063f, 0.725f, 0.506f, 0.15f);
        public Color alertSuccessText = new Color32(110, 231, 183, 255);
    }
}
