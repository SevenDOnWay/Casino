using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Script.UI {
    /// <summary>
    /// Handles responsive adaptations for UI Toolkit across Desktop PC and Mobile platforms.
    /// Manages orientation classes (is-portrait, is-landscape), narrow screen adaptations,
    /// and mobile safe-area insets (notches, dynamic islands, home bars).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuResponsiveLayout : MonoBehaviour {
        [Header("Responsive Thresholds")]
        [SerializeField] private float narrowScreenWidth = 480f;
        [SerializeField] private bool applyMobileSafeArea = true;

        private UIDocument uiDocument;
        private VisualElement rootElement;
        private VisualElement safeAreaContainer;

        private ScreenOrientation lastOrientation;
        private Rect lastSafeArea;
        private Vector2 lastScreenSize;

        private void Awake() {
            uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable() {
            if (uiDocument == null) return;
            rootElement = uiDocument.rootVisualElement;

            if (rootElement != null) {
                safeAreaContainer = rootElement.Q<VisualElement>("safe-area-container");
                rootElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                UpdateLayoutState();
            }
        }

        private void OnDisable() {
            if (rootElement != null) {
                rootElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }
        }

        private void Update() {
            // Track screen/safe area changes during runtime (e.g. mobile orientation flip)
            if (Screen.orientation != lastOrientation ||
                Screen.safeArea != lastSafeArea ||
                Screen.width != (int)lastScreenSize.x ||
                Screen.height != (int)lastScreenSize.y) {
                UpdateLayoutState();
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) {
            UpdateLayoutState();
        }

        public void UpdateLayoutState() {
            if (rootElement == null) {
                if (uiDocument != null) rootElement = uiDocument.rootVisualElement;
                if (rootElement == null) return;
            }

            if (safeAreaContainer == null) {
                safeAreaContainer = rootElement.Q<VisualElement>("safe-area-container");
            }

            float currentWidth = rootElement.resolvedStyle.width;
            float currentHeight = rootElement.resolvedStyle.height;

            if (float.IsNaN(currentWidth) || currentWidth <= 0) {
                currentWidth = Screen.width;
                currentHeight = Screen.height;
            }

            bool isPortrait = currentHeight > currentWidth;
            bool isNarrow = currentWidth <= narrowScreenWidth;
            bool isMobilePlatform = Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;

            // Manage Classes on Root
            rootElement.EnableInClassList("is-portrait", isPortrait);
            rootElement.EnableInClassList("is-landscape", !isPortrait);
            rootElement.EnableInClassList("is-narrow-screen", isNarrow);
            rootElement.EnableInClassList("is-mobile", isMobilePlatform);

            // Manage Safe Area
            if (applyMobileSafeArea && safeAreaContainer != null) {
                ApplySafeAreaInsets(currentWidth, currentHeight);
            }

            lastOrientation = Screen.orientation;
            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2(Screen.width, Screen.height);
        }

        private void ApplySafeAreaInsets(float elementWidth, float elementHeight) {
            Rect safeArea = Screen.safeArea;

            if (Screen.width <= 0 || Screen.height <= 0) return;

            // Calculate ratios for UI Toolkit pixels
            float leftRatio = safeArea.xMin / Screen.width;
            float rightRatio = (Screen.width - safeArea.xMax) / Screen.width;
            float topRatio = (Screen.height - safeArea.yMax) / Screen.height;
            float bottomRatio = safeArea.yMin / Screen.height;

            float padLeft = leftRatio * elementWidth;
            float padRight = rightRatio * elementWidth;
            float padTop = topRatio * elementHeight;
            float padBottom = bottomRatio * elementHeight;

            safeAreaContainer.style.paddingLeft = Mathf.Max(padLeft, 0f);
            safeAreaContainer.style.paddingRight = Mathf.Max(padRight, 0f);
            safeAreaContainer.style.paddingTop = Mathf.Max(padTop, 0f);
            safeAreaContainer.style.paddingBottom = Mathf.Max(padBottom, 0f);
        }
    }
}
