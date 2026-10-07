using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Assets.Script.UI.SO;

public static class FixLoginLayout {
    [MenuItem("Tools/UI/Fix Login Layout")]
    public static void Fix() {
        Debug.Log("Executing comprehensive UI polish on SignInScene...");

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SignInScene.unity");
        var canvas = GameObject.Find("LoginPageCanvas");
        if (canvas == null) { Debug.LogError("LoginPageCanvas not found!"); return; }

        LoginThemePaletteSO darkPalette = AssetDatabase.LoadAssetAtPath<LoginThemePaletteSO>(
            "Assets/Script/UI/SO/Resources/LoginThemePalette_Dark.asset");

        Sprite inputSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/RoundedInput.png");
        Sprite buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/RoundedButton.png");
        Sprite cardSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/RoundedCard.png");

        // ═══════════════════════════════════════════════════════════
        // CONTAINER: stretch full, controls children, no force expand
        // ═══════════════════════════════════════════════════════════
        var container = Find(canvas, "Container");
        FixVLG(container, TextAnchor.UpperCenter, true, true, false, false, 12);

        // ═══════════════════════════════════════════════════════════
        // HEADER BAR: fixed 48px height, full width, 32px padding
        // ═══════════════════════════════════════════════════════════
        var header = Find(canvas, "HeaderBar");
        SetLayoutElement(header, -1, 48, -1, 48, -1, -1);
        var hHLG = header.GetComponent<HorizontalLayoutGroup>();
        if (hHLG == null) hHLG = header.AddComponent<HorizontalLayoutGroup>();
        hHLG.childAlignment = TextAnchor.MiddleLeft;
        hHLG.childControlWidth = false;
        hHLG.childControlHeight = true;
        hHLG.childForceExpandWidth = false;
        hHLG.childForceExpandHeight = false;
        hHLG.padding = new RectOffset(32, 32, 0, 0);
        EditorUtility.SetDirty(hHLG);

        // ═══════════════════════════════════════════════════════════
        // AUTH CARD WRAPPER: flex height, centers the card
        // ═══════════════════════════════════════════════════════════
        var wrapper = Find(canvas, "AuthCardWrapper");
        SetLayoutElement(wrapper, -1, -1, -1, -1, -1, 1);
        FixVLG(wrapper, TextAnchor.MiddleCenter, false, false, false, false, 0);

        // ═══════════════════════════════════════════════════════════
        // AUTH CARD: 440px wide, preferred size vertical, no force expand
        // ═══════════════════════════════════════════════════════════
        var authCard = Find(canvas, "AuthCard");
        SetLayoutElement(authCard, 440, -1, 440, -1, -1, -1);
        FixVLG(authCard, TextAnchor.UpperCenter, true, true, false, false, 14);

        if (cardSprite != null) {
            var cardImg = authCard.GetComponent<Image>();
            if (cardImg != null) {
                cardImg.sprite = cardSprite;
                cardImg.type = Image.Type.Sliced;
                EditorUtility.SetDirty(cardImg);
            }
        }

        var cardRT = authCard.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(440, 0);
        cardRT.anchoredPosition = Vector2.zero;
        EditorUtility.SetDirty(cardRT);

        // ═══════════════════════════════════════════════════════════
        // CARD HEADER: centered text titles
        // ═══════════════════════════════════════════════════════════
        var cardHeader = Find(canvas, "CardHeader");
        FixVLG(cardHeader, TextAnchor.MiddleCenter, true, true, true, false, 4);

        // ═══════════════════════════════════════════════════════════
        // SOCIAL BUTTONS ROW: two buttons side by side, 380px wide
        // ═══════════════════════════════════════════════════════════
        var socialRow = Find(canvas, "SocialButtonsRow");
        SetLayoutElement(socialRow, 380, 44, 380, 44, 0, -1);
        FixHLG(socialRow, TextAnchor.MiddleCenter, true, true, true, false, 8);

        var googleBtn = Find(canvas, "GoogleLoginBtn");
        var googleLE = googleBtn.GetComponent<LayoutElement>();
        if (googleLE == null) googleLE = googleBtn.AddComponent<LayoutElement>();
        googleLE.flexibleWidth = 1;
        googleLE.preferredHeight = 44;
        googleLE.minHeight = 44;
        googleLE.preferredWidth = -1;
        googleLE.minWidth = -1;
        EditorUtility.SetDirty(googleLE);

        var fbBtn = Find(canvas, "FacebookLoginBtn");
        var fbLE = fbBtn.GetComponent<LayoutElement>();
        if (fbLE == null) fbLE = fbBtn.AddComponent<LayoutElement>();
        fbLE.flexibleWidth = 1;
        fbLE.preferredHeight = 44;
        fbLE.minHeight = 44;
        fbLE.preferredWidth = -1;
        fbLE.minWidth = -1;
        EditorUtility.SetDirty(fbLE);

        // ═══════════════════════════════════════════════════════════
        // DIVIDER CONTAINER: 380px wide
        // ═══════════════════════════════════════════════════════════
        var divider = Find(canvas, "DividerContainer");
        FixHLG(divider, TextAnchor.MiddleCenter, true, true, false, false, 0);
        SetLayoutElement(divider, 380, 20, 380, 20, 0, -1);

        var divLeft = Find(canvas, "DivLeft");
        var dlle = divLeft.GetComponent<LayoutElement>();
        if (dlle == null) dlle = divLeft.AddComponent<LayoutElement>();
        dlle.flexibleWidth = 1; dlle.minHeight = 1; dlle.preferredHeight = 1;
        EditorUtility.SetDirty(dlle);

        var divRight = Find(canvas, "DivRight");
        var drle = divRight.GetComponent<LayoutElement>();
        if (drle == null) drle = divRight.AddComponent<LayoutElement>();
        drle.flexibleWidth = 1; drle.minHeight = 1; drle.preferredHeight = 1;
        EditorUtility.SetDirty(drle);

        SetLayoutElement(Find(canvas, "DivBadge"), -1, 18, -1, 18, -1, -1);

        // ═══════════════════════════════════════════════════════════
        // LOGIN FORM: 380px wide
        // ═══════════════════════════════════════════════════════════
        var loginForm = Find(canvas, "LoginForm");
        SetLayoutElement(loginForm, 380, -1, 380, -1, 0, -1);
        FixVLG(loginForm, TextAnchor.UpperLeft, true, true, true, false, 14);

        // ═══════════════════════════════════════════════════════════
        // EMAIL GROUP
        // ═══════════════════════════════════════════════════════════
        FixVLG(Find(canvas, "EmailGroup"), TextAnchor.UpperLeft, true, true, true, false, 6);

        var emailInput = Find(canvas, "EmailInput");
        SetLayoutElement(emailInput, -1, 42, -1, 42, -1, -1);
        AddBorderOutline(emailInput, inputSprite, darkPalette != null ? darkPalette.inputBorderColor : new Color(0.25f, 0.25f, 0.27f));
        FixInputField(emailInput, darkPalette);

        var emailError = Find(canvas, "EmailError");
        SetLayoutElement(emailError, -1, 16, -1, 16, -1, -1);

        // ═══════════════════════════════════════════════════════════
        // PASSWORD GROUP & TOGGLE ICON
        // ═══════════════════════════════════════════════════════════
        FixVLG(Find(canvas, "PasswordGroup"), TextAnchor.UpperLeft, true, true, true, false, 6);

        var pwHeader = Find(canvas, "PasswordHeader");
        FixHLG(pwHeader, TextAnchor.MiddleLeft, true, true, false, false, 0);
        SetLayoutElement(pwHeader, -1, 18, -1, 18, -1, -1);

        var pwInput = Find(canvas, "PasswordInput");
        SetLayoutElement(pwInput, -1, 42, -1, 42, -1, -1);
        AddBorderOutline(pwInput, inputSprite, darkPalette != null ? darkPalette.inputBorderColor : new Color(0.25f, 0.25f, 0.27f));
        FixInputField(pwInput, darkPalette);

        // Fix Password toggle button & label
        var togglePwBtn = Find(canvas, "TogglePwBtn");
        if (togglePwBtn != null) {
            var tpRT = togglePwBtn.GetComponent<RectTransform>();
            tpRT.anchorMin = new Vector2(1f, 0.5f);
            tpRT.anchorMax = new Vector2(1f, 0.5f);
            tpRT.pivot = new Vector2(1f, 0.5f);
            tpRT.anchoredPosition = new Vector2(-6, 0);
            tpRT.sizeDelta = new Vector2(48, 28);
            EditorUtility.SetDirty(tpRT);
        }

        var toggleIcon = Find(canvas, "ToggleIcon");
        if (toggleIcon != null) {
            var iconTMP = toggleIcon.GetComponent<TextMeshProUGUI>();
            if (iconTMP != null) {
                iconTMP.text = "SHOW";
                iconTMP.fontSize = 11;
                iconTMP.fontStyle = FontStyles.Bold;
                iconTMP.alignment = TextAlignmentOptions.Center;
                if (darkPalette != null) iconTMP.color = darkPalette.secondaryTextColor;
                EditorUtility.SetDirty(iconTMP);
            }
        }

        var pwError = Find(canvas, "PasswordError");
        SetLayoutElement(pwError, -1, 16, -1, 16, -1, -1);

        // ═══════════════════════════════════════════════════════════
        // REMEMBER ROW
        // ═══════════════════════════════════════════════════════════
        var remRow = Find(canvas, "RememberRow");
        FixHLG(remRow, TextAnchor.MiddleLeft, false, true, false, false, 8);
        SetLayoutElement(remRow, -1, 22, -1, 22, -1, -1);
        SetLayoutElement(Find(canvas, "RememberToggle"), 18, 18, 18, 18, -1, -1);

        // ═══════════════════════════════════════════════════════════
        // SUBMIT BUTTON: 44px tall
        // ═══════════════════════════════════════════════════════════
        var submitBtn = Find(canvas, "SubmitBtn");
        SetLayoutElement(submitBtn, -1, 44, -1, 44, -1, -1);
        FixHLG(submitBtn, TextAnchor.MiddleCenter, true, true, false, false, 6);

        if (buttonSprite != null) {
            var submitImg = submitBtn.GetComponent<Image>();
            if (submitImg != null) {
                submitImg.sprite = buttonSprite;
                submitImg.type = Image.Type.Sliced;
                EditorUtility.SetDirty(submitImg);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // SIGNUP PROMPT ROW: 380px wide
        // ═══════════════════════════════════════════════════════════
        var signupRow = Find(canvas, "SignupPromptRow");
        SetLayoutElement(signupRow, 380, 24, 380, 24, 0, -1);
        FixHLG(signupRow, TextAnchor.MiddleCenter, false, true, false, false, 4);

        // ═══════════════════════════════════════════════════════════
        // FOOTER: 32px height, 32px padding
        // ═══════════════════════════════════════════════════════════
        var footer = Find(canvas, "FooterBar");
        SetLayoutElement(footer, -1, 32, -1, 32, -1, -1);
        var fHLG = footer.GetComponent<HorizontalLayoutGroup>();
        if (fHLG == null) fHLG = footer.AddComponent<HorizontalLayoutGroup>();
        fHLG.childAlignment = TextAnchor.MiddleCenter;
        fHLG.childControlWidth = false;
        fHLG.childControlHeight = true;
        fHLG.childForceExpandWidth = false;
        fHLG.childForceExpandHeight = false;
        fHLG.padding = new RectOffset(32, 32, 0, 0);
        EditorUtility.SetDirty(fHLG);

        // ═══════════════════════════════════════════════════════════
        // Save prefab and scene
        // ═══════════════════════════════════════════════════════════
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            canvas, "Assets/Resources/Prefabs/LoginPageCanvas.prefab",
            InteractionMode.AutomatedAction);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("SignInScene & LoginPageCanvas.prefab UI polish successfully saved!");
    }

    static void FixVLG(GameObject go, TextAnchor alignment,
        bool ctrlW, bool ctrlH, bool expandW, bool expandH, float spacing) {
        if (go == null) return;
        var vlg = go.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = alignment;
        vlg.childControlWidth = ctrlW;
        vlg.childControlHeight = ctrlH;
        vlg.childForceExpandWidth = expandW;
        vlg.childForceExpandHeight = expandH;
        vlg.spacing = spacing;
        EditorUtility.SetDirty(vlg);
    }

    static void FixHLG(GameObject go, TextAnchor alignment,
        bool ctrlW, bool ctrlH, bool expandW, bool expandH, float spacing) {
        if (go == null) return;
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = alignment;
        hlg.childControlWidth = ctrlW;
        hlg.childControlHeight = ctrlH;
        hlg.childForceExpandWidth = expandW;
        hlg.childForceExpandHeight = expandH;
        hlg.spacing = spacing;
        EditorUtility.SetDirty(hlg);
    }

    static void SetLayoutElement(GameObject go, float minW, float minH,
        float prefW, float prefH, float flexW, float flexH) {
        if (go == null) return;
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minWidth = minW;
        le.minHeight = minH;
        le.preferredWidth = prefW;
        le.preferredHeight = prefH;
        le.flexibleWidth = flexW;
        le.flexibleHeight = flexH;
        EditorUtility.SetDirty(le);
    }

    static void AddBorderOutline(GameObject inputGo, Sprite borderSprite, Color borderColor) {
        if (inputGo == null) return;
        var existing = inputGo.transform.Find("Border");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var borderGo = new GameObject("Border", typeof(RectTransform));
        borderGo.transform.SetParent(inputGo.transform, false);
        borderGo.transform.SetAsFirstSibling();

        var rt = borderGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        var img = borderGo.AddComponent<Image>();
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 2;
        img.color = borderColor;
        img.raycastTarget = false;
        img.fillCenter = false;

        EditorUtility.SetDirty(borderGo);
    }

    static void FixInputField(GameObject go, LoginThemePaletteSO palette) {
        if (go == null) return;
        var inp = go.GetComponent<TMP_InputField>();
        if (inp == null) return;

        inp.customCaretColor = true;
        inp.caretColor = palette != null ? palette.inputTextColor : Color.white;
        inp.caretWidth = 2;
        inp.selectionColor = new Color(0.65f, 0.81f, 1f, 0.4f);
        inp.transition = UnityEngine.UI.Selectable.Transition.None;

        EditorUtility.SetDirty(inp);
    }

    static GameObject Find(GameObject root, string name) {
        if (root == null) return null;
        var transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in transforms) {
            if (t.gameObject.name == name) return t.gameObject;
        }
        return null;
    }
}
