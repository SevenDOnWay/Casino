using UnityEngine;

namespace Assets.Script {
    [RequireComponent(typeof(SpriteRenderer))]
    public class ClockWipe : MonoBehaviour {
        [Header("Wipe Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float fillAmount = 1f;
        [SerializeField] private bool clockwise = true;
        [SerializeField] private bool showEmptyAsTint = false;
        [SerializeField] private Color emptyColor = new Color(0f, 0f, 0f, 0.5f);

        [Header("Color / Gradient Settings")]
        [SerializeField] private bool useGradient = true;
        [SerializeField] private Color fillColor = Color.white;
        [SerializeField] private Gradient colorGradient;

        private static readonly int FillAmountID = Shader.PropertyToID("_FillAmount");
        private static readonly int ClockwiseID = Shader.PropertyToID("_Clockwise");
        private static readonly int ShowEmptyID = Shader.PropertyToID("_ShowEmpty");
        private static readonly int FillColorID = Shader.PropertyToID("_FillColor");
        private static readonly int ColorID = Shader.PropertyToID("_Color");

        private SpriteRenderer sr;
        private MaterialPropertyBlock block;

        public float FillAmount {
            get => fillAmount;
            set {
                fillAmount = Mathf.Clamp01(value);
                Apply();
            }
        }

        public Color FillColor {
            get => fillColor;
            set {
                fillColor = value;
                Apply();
            }
        }

        public Color EmptyColor {
            get => emptyColor;
            set {
                emptyColor = value;
                Apply();
            }
        }

        public bool UseGradient {
            get => useGradient;
            set => useGradient = value;
        }

        public Gradient ColorGradient {
            get => colorGradient;
            set => colorGradient = value;
        }

        private void Awake() {
            sr = GetComponent<SpriteRenderer>();
            block = new MaterialPropertyBlock();
            InitializeDefaultGradient();
        }

        private void OnEnable() {
            Apply();
        }

        private void InitializeDefaultGradient() {
            if ( colorGradient == null || colorGradient.colorKeys == null || colorGradient.colorKeys.Length == 0 ) {
                colorGradient = new Gradient();
                colorGradient.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(0.95f, 0.25f, 0.25f), 0.0f),  // Red when time runs out
                        new GradientColorKey(new Color(1.0f, 0.85f, 0.2f), 0.35f),   // Yellow warning
                        new GradientColorKey(new Color(0.25f, 0.85f, 0.35f), 1.0f)   // Green at start
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, 1.0f)
                    }
                );
            }
        }

        /// <summary>
        /// Updates the clock wipe progress (0.0 to 1.0) and dynamically adjusts the color based on gradient.
        /// </summary>
        /// <param name="progress01">Normalized progress from 0.0 (empty) to 1.0 (full).</param>
        public void SetProgress( float progress01 ) {
            fillAmount = Mathf.Clamp01(progress01);

            if ( useGradient ) {
                if ( colorGradient == null || colorGradient.colorKeys == null || colorGradient.colorKeys.Length == 0 ) {
                    InitializeDefaultGradient();
                }
                fillColor = colorGradient.Evaluate(fillAmount);
            }

            Apply();
        }

        public void SetFillAmount( float fill ) {
            fillAmount = Mathf.Clamp01(fill);
            Apply();
        }

        public void SetColor( Color color ) {
            fillColor = color;
            Apply();
        }

        public void SetEmptyColor( Color color ) {
            emptyColor = color;
            Apply();
        }

        public void SetClockwise( bool isClockwise ) {
            clockwise = isClockwise;
            Apply();
        }

        public void SetShowEmptyAsTint( bool show ) {
            showEmptyAsTint = show;
            Apply();
        }

        /// <summary>
        /// Resets the clock wipe to fully filled white (default state).
        /// </summary>
        public void ResetToDefault() {
            fillAmount = 1f;
            fillColor = Color.white;
            Apply();
        }

        public void Apply() {
            if ( sr == null ) {
                sr = GetComponent<SpriteRenderer>();
                if ( sr == null ) return;
            }

            block ??= new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);

            block.SetFloat(FillAmountID, Mathf.Clamp01(fillAmount));
            block.SetFloat(ClockwiseID, clockwise ? 1f : 0f);
            block.SetFloat(ShowEmptyID, showEmptyAsTint ? 1f : 0f);
            block.SetColor(FillColorID, emptyColor);
            block.SetColor(ColorID, fillColor);

            sr.SetPropertyBlock(block);
        }

        private void LateUpdate() {
            // Allows live tuning in the editor during Play mode
            Apply();
        }
    }
}
