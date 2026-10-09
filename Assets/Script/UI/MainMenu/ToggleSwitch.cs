using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Assets.Script.UI.MainMenu {
    /// <summary>
    /// Lightweight uGUI on/off switch used for the Public / Private table setting.
    /// Keeps its visual state (knob position + colors) in sync with <see cref="IsOn"/>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ToggleSwitch : MonoBehaviour {
        [Tooltip("Knob that slides when the switch is toggled.")]
        public RectTransform knob;
        [Tooltip("Track image tinted by the on/off colors.")]
        public Image background;
        [Tooltip("Optional label describing the current state.")]
        public TMP_Text valueLabel;

        [Header("Labels")]
        [SerializeField] private string onText = "PRIVATE TABLE";
        [SerializeField] private string offText = "PUBLIC TABLE";

        [Header("Colors")]
        [SerializeField] private Color onColor = new Color(0.204f, 0.827f, 0.600f, 1f);
        [SerializeField] private Color offColor = new Color(0.22f, 0.24f, 0.28f, 1f);

        [Header("Knob Layout")]
        [SerializeField] private float knobOnPosition = 13f;
        [SerializeField] private float knobOffPosition = -13f;

        [HideInInspector] public bool IsOn { get; private set; }

        public event Action<bool> OnValueChanged;

        private Button button;

        private void Awake() {
            button = GetComponent<Button>();
            if (button == null) button = gameObject.AddComponent<Button>();

            button.transition = Selectable.Transition.None;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Toggle);
        }

        private void OnEnable() => ApplyVisualState();

        /// <summary>Flips the value without raising <see cref="OnValueChanged"/>.</summary>
        public void SetValue(bool value, bool notify = true) {
            if (IsOn == value) {
                ApplyVisualState();
                return;
            }

            IsOn = value;
            ApplyVisualState();
            if (notify) OnValueChanged?.Invoke(value);
        }

        public void Toggle() => SetValue(!IsOn);

        private void ApplyVisualState() {
            if (background != null) background.color = IsOn ? onColor : offColor;

            if (knob != null) {
                knob.anchorMin = new Vector2(IsOn ? 1f : 0f, 0.5f);
                knob.anchorMax = new Vector2(IsOn ? 1f : 0f, 0.5f);
                knob.pivot = new Vector2(0.5f, 0.5f);
                knob.anchoredPosition = new Vector2(IsOn ? -knobOnPosition : knobOffPosition, 0f);
            }

            if (valueLabel != null) valueLabel.text = IsOn ? onText : offText;
        }

#if UNITY_EDITOR
        private void OnValidate() => ApplyVisualState();
#endif
    }
}
