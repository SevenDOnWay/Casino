using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.MainMenu {
    /// <summary>
    /// Builds the uGUI main-menu Canvas at runtime.
    ///
    /// This keeps the migration from UI Toolkit to uGUI self-contained: the whole
    /// hierarchy (player info corner, center play options, profile panel, host panel
    /// and search panel) is generated from code, so no prefab has to be authored by hand.
    ///
    /// Layout
    ///   Top-Left  : player info (avatar, name, level, money) + profile button
    ///   Center    : Quick Join / Host Table / Search Table
    ///   Modal     : Profile panel (change display name &amp; avatar)
    ///   Modal     : Host panel (table name, game type, stake, players, public/private)
    ///   Modal     : Search panel (join by room code)
    /// </summary>
    public static class MainMenuUILayoutBuilder {
        private static readonly Color BgColor = new Color(0.078f, 0.086f, 0.102f, 1f);
        private static readonly Color PanelColor = new Color(0.129f, 0.141f, 0.169f, 1f);
        private static readonly Color ButtonColor = new Color(0.188f, 0.204f, 0.239f, 1f);
        private static readonly Color PrimaryColor = new Color(0.204f, 0.827f, 0.600f, 1f);
        private static readonly Color AccentColor = new Color(0.204f, 0.827f, 0.600f, 1f);
        private static readonly Color ChipColor = new Color(0.22f, 0.24f, 0.28f, 1f);
        private static readonly Color TextColor = new Color(0.925f, 0.929f, 0.941f, 1f);
        private static readonly Color MutedColor = new Color(0.615f, 0.639f, 0.706f, 1f);
        private static readonly Color DangerColor = new Color(0.937f, 0.267f, 0.267f, 1f);
        private static readonly Color MoneyColor = new Color(0.984f, 0.749f, 0.141f, 1f);
        private static readonly Color InputColor = new Color(0.09f, 0.098f, 0.118f, 1f);
        private static readonly Color ClearColor = new Color(0f, 0f, 0f, 0f);

        /// <summary>Every UI reference produced by <see cref="Build"/>.</summary>
        public sealed class Refs {
            public GameObject playerInfoPanel;
            public Image avatarImage;
            public TMP_Text avatarInitialsText;
            public TMP_Text playerNameText;
            public TMP_Text playerLevelText;
            public TMP_Text playerMoneyText;
            public Button btnOpenProfile;

            public GameObject playOptionsPanel;
            public Button btnQuickJoin;
            public Button btnHostTable;
            public Button btnSearchTable;

            public GameObject profilePanel;
            public TMP_InputField inputDisplayName;
            public Button btnAvatarPrev;
            public Button btnAvatarNext;
            public Image profileAvatarPreview;
            public TMP_Text profileAvatarNameText;
            public Button btnSaveProfile;
            public Button btnCancelProfile;

            public GameObject hostPanel;
            public TMP_InputField inputTableName;
            public Button btnGameTypeCasual;
            public Button btnGameTypeRanked;
            public Button btnGameTypeFast;
            public TMP_Text gameTypeText;
            public TMP_Text stakeDisplayText;
            public Button btnStake1k;
            public Button btnStake5k;
            public Button btnStake20k;
            public Button btnStake100k;
            public Button btnPlayers2;
            public Button btnPlayers3;
            public Button btnPlayers4;
            public ToggleSwitch togglePrivateRoom;
            public Button btnConfirmHost;
            public Button btnCancelHost;

            public GameObject searchPanel;
            public TMP_InputField inputRoomCode;
            public Button btnJoinByCode;
            public Button btnCloseSearch;
        }

        private static readonly string[] GameTypeNames = { "Casual", "Ranked", "Fast Play" };

        /// <summary>Creates the main-menu Canvas below <paramref name="owner"/> and returns its references.</summary>
        public static Refs Build(Component owner) {
            var canvasGo = new GameObject("MainMenuCanvas");
            canvasGo.transform.SetParent(owner.transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<MainMenuUIRootMarker>();

            var root = CreateImage(canvasGo.transform, "MainMenuUI", BgColor);
            Stretch(root);

            var refs = new Refs();

            BuildPlayerInfoPanel(root.transform, refs);
            BuildPlayOptionsPanel(root.transform, refs);
            BuildProfilePanel(root.transform, refs);
            BuildHostPanel(root.transform, refs);
            BuildSearchPanel(root.transform, refs);

            return refs;
        }

        // =====================================================================
        // Top-Left corner: Player info
        // =====================================================================
        private static void BuildPlayerInfoPanel(Transform parent, Refs refs) {
            var panel = CreateImage(parent, "PlayerInfoPanel", PanelColor);
            AnchorTo(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(380f, 108f));
            refs.playerInfoPanel = panel.gameObject;

            var avatar = CreateImage(panel.transform, "AvatarFrame", new Color(0.16f, 0.17f, 0.20f, 1f));
            AnchorTo(avatar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(80f, 80f));

            var initials = CreateText(avatar.transform, "Initials", "P1", 26, TextAlignmentOptions.Center);
            Stretch(initials);
            refs.avatarInitialsText = initials.GetComponent<TextMeshProUGUI>();
            refs.avatarImage = avatar;

            var info = CreateEmpty(panel.transform, "Info");
            AnchorTo(info, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(96f, 8f), new Vector2(-190f, -16f));
            var cols = info.gameObject.AddComponent<VerticalLayoutGroup>();
            cols.padding = new RectOffset(0, 0, 8, 8);
            cols.spacing = 0f;
            cols.childControlWidth = true;
            cols.childControlHeight = true;
            cols.childForceExpandWidth = true;
            cols.childForceExpandHeight = false;
            cols.childAlignment = TextAnchor.UpperLeft;

            refs.playerNameText = CreateText(info, "PlayerName", "PLAYER_01", 26, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            refs.playerLevelText = CreateText(info, "PlayerLevel", "LV 1", 17, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            refs.playerLevelText.color = MutedColor;
            refs.playerMoneyText = CreateText(info, "PlayerMoney", "0 CR", 20, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            refs.playerMoneyText.color = MoneyColor;

            var edit = CreateButton(panel.transform, "BtnEditProfile", "PROFILE", ButtonColor, TextColor, 18);
            AnchorTo(edit, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-76f, 0f), new Vector2(136f, 46f));
            refs.btnOpenProfile = edit.GetComponent<Button>();
        }

        // =====================================================================
        // Center: Play options
        // =====================================================================
        private static void BuildPlayOptionsPanel(Transform parent, Refs refs) {
            var panel = CreateEmpty(parent, "PlayOptions");
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(620f, 460f);
            refs.playOptionsPanel = panel.gameObject;

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childAlignment = TextAnchor.MiddleCenter;

            var title = CreateText(panel, "Title", "ARENA LOBBY", 58, TextAlignmentOptions.Center).GetComponent<TextMeshProUGUI>();
            AddLayoutElement(title.gameObject).preferredHeight = 74f;

            var subtitle = CreateText(panel, "Subtitle", "TABLE MATCHMAKING", 22, TextAlignmentOptions.Center).GetComponent<TextMeshProUGUI>();
            subtitle.color = MutedColor;
            AddLayoutElement(subtitle.gameObject).preferredHeight = 40f;

            refs.btnQuickJoin = CreateActionButton(panel, "BtnQuickJoin", "QUICK JOIN", "Immediate match based on skill", PrimaryColor, true);
            refs.btnHostTable = CreateActionButton(panel, "BtnHostTable", "HOST TABLE", "Create custom stakes & room rules", ButtonColor, false);
            refs.btnSearchTable = CreateActionButton(panel, "BtnSearchTable", "SEARCH TABLE", "Filter public lobbies or enter code", ButtonColor, false);
        }

        private static Button CreateActionButton(Transform parent, string name, string label, string sublabel, Color bg, bool isPrimary) {
            var go = CreateImage(parent, name, bg);
            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = go;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = AccentColor;
            btn.colors = colors;

            var labelText = CreateText(go.transform, "Label", label, 28, TextAlignmentOptions.MidlineLeft).GetComponent<TextMeshProUGUI>();
            labelText.color = isPrimary ? new Color(0.05f, 0.07f, 0.09f, 1f) : TextColor;
            labelText.margin = new Vector4(28f, 0f, 0f, 0f);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(1f, 0.62f);
            labelText.rectTransform.offsetMin = new Vector2(28f, 0f);
            labelText.rectTransform.offsetMax = new Vector2(-28f, 0f);

            var subText = CreateText(go.transform, "SubLabel", sublabel, 15, TextAlignmentOptions.TopLeft).GetComponent<TextMeshProUGUI>();
            subText.color = isPrimary ? new Color(0.05f, 0.14f, 0.10f, 1f) : MutedColor;
            subText.rectTransform.anchorMin = new Vector2(0f, 0f);
            subText.rectTransform.anchorMax = new Vector2(1f, 0.38f);
            subText.rectTransform.offsetMin = new Vector2(28f, 0f);
            subText.rectTransform.offsetMax = new Vector2(-28f, 0f);

            AddLayoutElement(go).preferredHeight = 84f;

            return btn;
        }

        // =====================================================================
        // Modals
        // =====================================================================
        private static RectTransform CreateModal(Transform parent, string name, float width, float height) {
            var scrim = CreateImage(parent, name + "_Scrim", new Color(0f, 0f, 0f, 0.74f));
            Stretch(scrim);

            var dialog = CreateImage(scrim.transform, name, PanelColor);
            var dialogRect = dialog.rectTransform;
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(width, height);
            return dialogRect;
        }

        private static void BuildProfilePanel(Transform parent, Refs refs) {
            var dialog = CreateModal(parent, "ProfilePanel", 560f, 460f);
            refs.profilePanel = dialog.gameObject;
            dialog.gameObject.SetActive(false);

            var vlg = dialog.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(30, 30, 26, 26);
            vlg.spacing = 14f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childAlignment = TextAnchor.UpperCenter;

            var header = CreateText(dialog, "Header", "CHANGE PROFILE", 32, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            AddLayoutElement(header.gameObject).preferredHeight = 44f;

            // --- Avatar picker ---
            var avatarRow = CreateEmpty(dialog, "AvatarRow");
            var hlg = avatarRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            AddLayoutElement(avatarRow).preferredHeight = 104f;

            refs.btnAvatarPrev = CreateButton(avatarRow, "BtnAvatarPrev", "<", ButtonColor, TextColor, 30).GetComponent<Button>();
            AddLayoutElement(refs.btnAvatarPrev.gameObject).preferredWidth = 56f;

            var preview = CreateImage(avatarRow, "AvatarPreview", new Color(0.16f, 0.17f, 0.20f, 1f));
            preview.rectTransform.sizeDelta = new Vector2(104f, 104f);
            refs.profileAvatarPreview = preview;

            refs.btnAvatarNext = CreateButton(avatarRow, "BtnAvatarNext", ">", ButtonColor, TextColor, 30).GetComponent<Button>();
            AddLayoutElement(refs.btnAvatarNext.gameObject).preferredWidth = 56f;

            refs.profileAvatarNameText = CreateText(avatarRow, "AvatarName", "AVATAR", 16, TextAlignmentOptions.MidlineLeft).GetComponent<TextMeshProUGUI>();
            refs.profileAvatarNameText.color = MutedColor;
            AddLayoutElement(refs.profileAvatarNameText.gameObject).preferredWidth = 110f;

            // --- Display name ---
            var nameLabel = CreateText(dialog, "NameLabel", "DISPLAY NAME", 18, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            nameLabel.color = MutedColor;
            AddLayoutElement(nameLabel.gameObject).preferredHeight = 28f;

            refs.inputDisplayName = CreateInputField(dialog, "InputDisplayName", "Your display name").GetComponent<TMP_InputField>();
            AddLayoutElement(refs.inputDisplayName.gameObject).preferredHeight = 54f;

            // --- Actions ---
            var actions = CreateEmpty(dialog, "Actions");
            var ah = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            ah.spacing = 12f;
            ah.childControlWidth = true;
            ah.childControlHeight = false;
            ah.childForceExpandWidth = true;
            AddLayoutElement(actions).preferredHeight = 58f;

            refs.btnSaveProfile = CreateButton(actions, "BtnSaveProfile", "SAVE", PrimaryColor, new Color(0.05f, 0.07f, 0.09f, 1f), 22).GetComponent<Button>();
            refs.btnCancelProfile = CreateButton(actions, "BtnCancelProfile", "CANCEL", DangerColor, Color.white, 22).GetComponent<Button>();
            AddLayoutElement(refs.btnSaveProfile.gameObject).flexibleWidth = 2f;
            AddLayoutElement(refs.btnCancelProfile.gameObject).flexibleWidth = 1f;
        }

        private static void BuildHostPanel(Transform parent, Refs refs) {
            var dialog = CreateModal(parent, "HostPanel", 660f, 700f);
            refs.hostPanel = dialog.gameObject;
            dialog.gameObject.SetActive(false);

            var vlg = dialog.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 26, 26);
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childAlignment = TextAnchor.UpperCenter;

            var header = CreateText(dialog, "Header", "HOST A TABLE", 34, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            AddLayoutElement(header.gameObject).preferredHeight = 46f;

            // ---- Table name ----
            var nameLabel = CreateText(dialog, "NameLabel", "TABLE NAME", 18, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            nameLabel.color = MutedColor;
            AddLayoutElement(nameLabel.gameObject).preferredHeight = 26f;

            refs.inputTableName = CreateInputField(dialog, "InputTableName", "Table name").GetComponent<TMP_InputField>();
            AddLayoutElement(refs.inputTableName.gameObject).preferredHeight = 56f;

            // ---- Game type ----
            var gtLabel = CreateText(dialog, "GameTypeLabel", "GAME TYPE", 18, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            gtLabel.color = MutedColor;
            AddLayoutElement(gtLabel.gameObject).preferredHeight = 26f;

            var gtRow = AddLayoutElement(CreateEmpty(dialog, "GameTypeRow"));
            gtRow.preferredHeight = 54f;
            AddRowLayout(gtRow.gameObject);
            refs.btnGameTypeCasual = CreateChip(gtRow.gameObject.transform, "BtnGameTypeCasual", GameTypeNames[0]);
            refs.btnGameTypeRanked = CreateChip(gtRow.gameObject.transform, "BtnGameTypeRanked", GameTypeNames[1]);
            refs.btnGameTypeFast = CreateChip(gtRow.gameObject.transform, "BtnGameTypeFast", GameTypeNames[2]);

            refs.gameTypeText = CreateText(dialog, "GameTypeValue", GameTypeNames[0], 17, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            refs.gameTypeText.color = AccentColor;
            AddLayoutElement(refs.gameTypeText.gameObject).preferredHeight = 24f;

            // ---- Stake / how much ----
            var stakeLabel = CreateText(dialog, "StakeLabel", "STAKE / HOW MUCH", 18, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            stakeLabel.color = MutedColor;
            AddLayoutElement(stakeLabel.gameObject).preferredHeight = 26f;

            var stakeRow = AddLayoutElement(CreateEmpty(dialog, "StakeRow"));
            stakeRow.preferredHeight = 54f;
            AddRowLayout(stakeRow.gameObject);
            refs.btnStake1k = CreateChip(stakeRow.gameObject.transform, "BtnStake1k", "1,000 CR");
            refs.btnStake5k = CreateChip(stakeRow.gameObject.transform, "BtnStake5k", "5,000 CR");
            refs.btnStake20k = CreateChip(stakeRow.gameObject.transform, "BtnStake20k", "20,000 CR");
            refs.btnStake100k = CreateChip(stakeRow.gameObject.transform, "BtnStake100k", "100,000 CR");

            refs.stakeDisplayText = CreateText(dialog, "StakeValue", "1,000 CR", 17, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            refs.stakeDisplayText.color = new Color(0.259f, 0.545f, 0.796f, 1f);
            AddLayoutElement(refs.stakeDisplayText.gameObject).preferredHeight = 24f;

            // ---- Max players ----
            var playersLabel = CreateText(dialog, "PlayersLabel", "MAX PLAYERS", 18, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            playersLabel.color = MutedColor;
            AddLayoutElement(playersLabel.gameObject).preferredHeight = 26f;

            var playersRow = AddLayoutElement(CreateEmpty(dialog, "PlayersRow"));
            playersRow.preferredHeight = 54f;
            AddRowLayout(playersRow.gameObject);
            refs.btnPlayers2 = CreateChip(playersRow.gameObject.transform, "BtnPlayers2", "2 Players");
            refs.btnPlayers3 = CreateChip(playersRow.gameObject.transform, "BtnPlayers3", "3 Players");
            refs.btnPlayers4 = CreateChip(playersRow.gameObject.transform, "BtnPlayers4", "4 Players");

            // ---- Public / Private ----
            var privacyRow = CreateImage(dialog, "PrivacyRow", ClearColor);
            AddLayoutElement(privacyRow).preferredHeight = 48f;
            AddRowLayout(privacyRow.gameObject);

            refs.togglePrivateRoom = BuildToggleSwitch(privacyRow.gameObject);

            // ---- Actions ----
            var actions = CreateEmpty(dialog, "Actions");
            var ah = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            ah.spacing = 12f;
            ah.childControlWidth = true;
            ah.childControlHeight = false;
            ah.childForceExpandWidth = true;
            AddLayoutElement(actions).preferredHeight = 60f;

            refs.btnConfirmHost = CreateButton(actions, "BtnConfirmHost", "CREATE TABLE", PrimaryColor, new Color(0.05f, 0.07f, 0.09f, 1f), 22).GetComponent<Button>();
            refs.btnCancelHost = CreateButton(actions, "BtnCancelHost", "CANCEL", DangerColor, Color.white, 22).GetComponent<Button>();
            AddLayoutElement(refs.btnConfirmHost.gameObject).flexibleWidth = 2f;
            AddLayoutElement(refs.btnCancelHost.gameObject).flexibleWidth = 1f;
        }

        private static ToggleSwitch BuildToggleSwitch(GameObject row) {
            var track = CreateImage(row.transform, "Track", new Color(0.22f, 0.24f, 0.28f, 1f));
            track.rectTransform.sizeDelta = new Vector2(56f, 30f);
            AddLayoutElement(track.gameObject).preferredWidth = 56f;

            var knob = CreateImage(track.transform, "Knob", Color.white);
            knob.rectTransform.sizeDelta = new Vector2(22f, 22f);

            var label = CreateText(row.transform, "PrivacyLabel", "PUBLIC TABLE", 20, TextAlignmentOptions.MidlineLeft).GetComponent<TextMeshProUGUI>();
            label.color = TextColor;

            var clickable = row.AddComponent<Image>();
            clickable.color = ClearColor;

            var button = row.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = clickable;

            var toggle = row.AddComponent<ToggleSwitch>();
            toggle.knob = knob.rectTransform;
            toggle.background = track;
            toggle.valueLabel = label;
            toggle.SetValue(false, false);

            return toggle;
        }

        private static void BuildSearchPanel(Transform parent, Refs refs) {
            var dialog = CreateModal(parent, "SearchPanel", 560f, 320f);
            refs.searchPanel = dialog.gameObject;
            dialog.gameObject.SetActive(false);

            var vlg = dialog.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(30, 30, 26, 26);
            vlg.spacing = 14f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childAlignment = TextAnchor.UpperCenter;

            var header = CreateText(dialog, "Header", "SEARCH TABLES", 34, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            AddLayoutElement(header.gameObject).preferredHeight = 46f;

            var codeLabel = CreateText(dialog, "CodeLabel", "ENTER ROOM CODE", 18, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();
            codeLabel.color = MutedColor;
            AddLayoutElement(codeLabel.gameObject).preferredHeight = 26f;

            refs.inputRoomCode = CreateInputField(dialog, "InputRoomCode", "e.g. ARENA-982").GetComponent<TMP_InputField>();
            AddLayoutElement(refs.inputRoomCode.gameObject).preferredHeight = 58f;

            var actions = CreateEmpty(dialog, "Actions");
            var ah = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            ah.spacing = 12f;
            ah.childControlWidth = true;
            ah.childControlHeight = false;
            ah.childForceExpandWidth = true;
            AddLayoutElement(actions).preferredHeight = 58f;

            refs.btnJoinByCode = CreateButton(actions, "BtnJoinByCode", "JOIN", PrimaryColor, new Color(0.05f, 0.07f, 0.09f, 1f), 22).GetComponent<Button>();
            refs.btnCloseSearch = CreateButton(actions, "BtnCloseSearch", "CLOSE", ButtonColor, TextColor, 22).GetComponent<Button>();
            AddLayoutElement(refs.btnJoinByCode.gameObject).flexibleWidth = 2f;
            AddLayoutElement(refs.btnCloseSearch.gameObject).flexibleWidth = 1f;
        }

        // =====================================================================
        // Factory helpers
        // =====================================================================
        private static Image CreateImage(Transform parent, string name, Color color) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static RectTransform CreateEmpty(Transform parent, string name) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform CreateText(Transform parent, string name, string content, float size, TextAlignmentOptions align) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = TextColor;
            tmp.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            return (RectTransform)go.transform;
        }

        private static RectTransform CreateButton(Transform parent, string name, string label, Color bg, Color fg, float fontSize) {
            var go = CreateImage(parent, name, bg);
            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = go;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = AccentColor;
            btn.colors = colors;

            var tmp = go.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = fg;
            tmp.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            Stretch((RectTransform)tmp.transform);

            AddLayoutElement(go).preferredHeight = 46f;
            return go.rectTransform;
        }

        private static Button CreateChip(Transform parent, string name, string label) {
            return CreateButton(parent, name, label, ChipColor, TextColor, 18).gameObject.GetComponent<Button>();
        }

        private static RectTransform CreateInputField(Transform parent, string name, string placeholder) {
            var go = CreateImage(parent, name, InputColor);
            var input = go.gameObject.AddComponent<TMP_InputField>();
            input.transition = Selectable.Transition.ColorTint;

            var area = CreateEmpty(go.transform, "Text Area");
            AnchorTo(area, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            area.gameObject.AddComponent<RectMask2D>();
            input.textViewport = area;

            var text = CreateText(area, "Text", string.Empty, 22, TextAlignmentOptions.MidlineLeft).GetComponent<TextMeshProUGUI>();
            Stretch((RectTransform)text.transform);
            text.raycastTarget = true;
            input.textComponent = text;

            var ph = CreateText(area, "Placeholder", placeholder, 22, TextAlignmentOptions.MidlineLeft).GetComponent<TextMeshProUGUI>();
            Stretch((RectTransform)ph.transform);
            ph.color = new Color(0.44f, 0.47f, 0.53f, 1f);
            ph.raycastTarget = true;
            input.placeholder = ph;

            AddLayoutElement(go).preferredHeight = 54f;
            return go.rectTransform;
        }

        // =====================================================================
        // Rect helpers
        // =====================================================================
        private static void AddRowLayout(GameObject go) {
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;
        }

        private static LayoutElement AddLayoutElement(GameObject go) => go.AddComponent<LayoutElement>();
        private static LayoutElement AddLayoutElement(Component c) => c.gameObject.AddComponent<LayoutElement>();

        private static void Stretch(RectTransform rt) {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Stretch(Graphic graphic) => Stretch(graphic.rectTransform);

        private static void AnchorTo(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta) {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        private static void AnchorTo(Graphic graphic, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta) =>
            AnchorTo(graphic.rectTransform, anchorMin, anchorMax, anchoredPosition, sizeDelta);
    }

    /// <summary>Marks a generated main-menu Canvas so it is built only once.</summary>
    public sealed class MainMenuUIRootMarker : MonoBehaviour { }
}
