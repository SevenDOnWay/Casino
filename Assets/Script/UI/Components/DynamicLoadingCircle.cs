using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Components {
    /// <summary>
    /// Dynamic circular loading indicator for Unity UI (uGUI).
    /// Provides continuous rotation and dynamic radial arc expansion/contraction,
    /// with complete lifecycle management (Awake, OnEnable, OnDisable, OnDestroy).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class DynamicLoadingCircle : MonoBehaviour {

        [Header("Rotation Settings")]
        [Tooltip("Rotation speed in degrees per second.")]
        [SerializeField] private float rotationSpeed = 360f;
        [Tooltip("Clockwise rotation if true, counter-clockwise if false.")]
        [SerializeField] private bool clockwise = true;
        [Tooltip("Use unscaled delta time so animation plays even if Time.timeScale is 0.")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Dynamic Arc Settings (Filled Image)")]
        [Tooltip("Whether to animate the fill amount of the circle arc if Image.type is Filled.")]
        [SerializeField] private bool animateArcSweep = true;
        [Tooltip("Speed of the arc expanding and contracting.")]
        [SerializeField] private float arcSweepSpeed = 1.8f;
        [Range(0.05f, 0.5f)]
        [SerializeField] private float minFillAmount = 0.1f;
        [Range(0.5f, 0.98f)]
        [SerializeField] private float maxFillAmount = 0.85f;

        [Header("Pulse / Scale Effect (Optional)")]
        [SerializeField] private bool animateScalePulse = false;
        [SerializeField] private float scalePulseSpeed = 2.0f;
        [SerializeField] private float minScale = 0.95f;
        [SerializeField] private float maxScale = 1.05f;

        [Header("Fading & Raycast (CanvasGroup)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 0.15f;
        [SerializeField] private bool blockRaycastsWhileLoading = true;

        [Header("Lifecycle Options")]
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool hideOnAwake = true;

        private RectTransform rectTransform;
        private Image targetImage;
        private bool isPlaying = false;
        private float arcTimer = 0f;
        private CancellationTokenSource fadeCts;

        public bool IsPlaying => isPlaying;

        private CanvasGroup ActiveCanvasGroup {
            get {
                if ( canvasGroup == null ) {
                    canvasGroup = GetComponent<CanvasGroup>();
                }
                return canvasGroup;
            }
        }

        private RectTransform ActiveRectTransform {
            get {
                if ( rectTransform == null ) {
                    rectTransform = GetComponent<RectTransform>();
                }
                return rectTransform;
            }
        }

        private Image ActiveTargetImage {
            get {
                if ( targetImage == null ) {
                    targetImage = GetComponent<Image>();
                }
                return targetImage;
            }
        }

        private void Awake() {
            _ = ActiveRectTransform;
            _ = ActiveTargetImage;
            var cg = ActiveCanvasGroup;

            if ( hideOnAwake ) {
                ResetVisuals();
                if ( cg != null ) {
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                }
            }
        }

        private void OnEnable() {
            if ( playOnEnable ) {
                Play();
            }
        }

        private void OnDisable() {
            StopAnimationInternal();
        }

        private void OnDestroy() {
            CancelFade();
        }

        private void Update() {
            if ( !isPlaying ) return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if ( dt <= 0f ) return;

            // 1. Continuous rotation around Z axis
            float rotationDirection = clockwise ? -1f : 1f;
            ActiveRectTransform.Rotate(0f, 0f, rotationDirection * rotationSpeed * dt);

            // 2. Dynamic radial arc sweep (expands and contracts)
            var img = ActiveTargetImage;
            if ( animateArcSweep && img != null && img.type == Image.Type.Filled ) {
                arcTimer += dt * arcSweepSpeed;
                float pingPong = Mathf.PingPong(arcTimer, 1f);
                // Smooth sine interpolation for natural expansion/contraction
                float smoothT = Mathf.SmoothStep(0f, 1f, pingPong);
                img.fillAmount = Mathf.Lerp(minFillAmount, maxFillAmount, smoothT);
            }

            // 3. Optional subtle scale pulse
            if ( animateScalePulse ) {
                float scalePingPong = Mathf.PingPong(arcTimer * (scalePulseSpeed / arcSweepSpeed), 1f);
                float currentScale = Mathf.Lerp(minScale, maxScale, scalePingPong);
                ActiveRectTransform.localScale = new Vector3(currentScale, currentScale, 1f);
            }
        }

        /// <summary>
        /// Starts the dynamic loading animation with optional smooth fade-in.
        /// </summary>
        public void Play() {
            if ( !gameObject.activeSelf ) {
                gameObject.SetActive(true);
            }

            isPlaying = true;
            var cg = ActiveCanvasGroup;

            if ( cg != null ) {
                if ( blockRaycastsWhileLoading ) {
                    cg.blocksRaycasts = true;
                }
                CancelFade();
                fadeCts = new CancellationTokenSource();
                _ = FadeAlphaAsync(cg.alpha, 1f, fadeDuration, fadeCts.Token);
            }
        }

        /// <summary>
        /// Stops the dynamic loading animation and resets visuals.
        /// </summary>
        public void Stop( bool instant = false ) {
            isPlaying = false;
            var cg = ActiveCanvasGroup;

            if ( cg != null ) {
                cg.blocksRaycasts = false;
                CancelFade();
                if ( instant || fadeDuration <= 0f ) {
                    cg.alpha = 0f;
                    StopAnimationInternal();
                    gameObject.SetActive(false);
                }
                else {
                    fadeCts = new CancellationTokenSource();
                    _ = FadeOutAndStopAsync(fadeDuration, fadeCts.Token);
                }
            }
            else {
                StopAnimationInternal();
                gameObject.SetActive(false);
            }
        }

        private void StopAnimationInternal() {
            isPlaying = false;
            arcTimer = 0f;
            ResetVisuals();
            CancelFade();
        }

        private void ResetVisuals() {
            var rt = ActiveRectTransform;
            if ( rt != null ) {
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }
            var img = ActiveTargetImage;
            if ( img != null && img.type == Image.Type.Filled ) {
                img.fillAmount = minFillAmount;
            }
        }

        private void CancelFade() {
            if ( fadeCts != null ) {
                fadeCts.Cancel();
                fadeCts.Dispose();
                fadeCts = null;
            }
        }

        private async UniTask FadeAlphaAsync( float startAlpha, float targetAlpha, float duration, CancellationToken ct ) {
            var cg = ActiveCanvasGroup;
            if ( cg == null ) return;
            if ( duration <= 0f ) {
                cg.alpha = targetAlpha;
                return;
            }

            float elapsed = 0f;
            while ( elapsed < duration ) {
                if ( ct.IsCancellationRequested ) return;
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if ( cg != null ) {
                    cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if ( cg != null ) {
                cg.alpha = targetAlpha;
            }
        }

        private async UniTaskVoid FadeOutAndStopAsync( float duration, CancellationToken ct ) {
            var cg = ActiveCanvasGroup;
            if ( cg != null && duration > 0f ) {
                await FadeAlphaAsync(cg.alpha, 0f, duration, ct);
            }

            if ( !ct.IsCancellationRequested ) {
                StopAnimationInternal();
                if ( gameObject.activeSelf ) {
                    gameObject.SetActive(false);
                }
            }
        }
    }
}
