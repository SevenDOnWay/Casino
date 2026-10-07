using System;
using System.Collections;
using System.Text.RegularExpressions;
using Assets.Script.Data.Models;
using Assets.Script.Data.Repositories;
using Assets.Script.Data.Services;
using Assets.Script.UI.Components;
using Assets.Script.UI.SO;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI {
    public enum AlertType {
        Success,
        Error,
        Info
    }

    /// <summary>
    /// uGUI controller managing the modern login & register interface.
    /// Supports email/password authentication, validation, mode toggling, theme switching, and backend integration.
    /// </summary>
    public class LoginPageController : MonoBehaviour {
        [Header("Configuration")]
        [SerializeField] private bool startWithDarkTheme = true;
        [SerializeField] private string nextSceneName = "MainMenuScene";
        [SerializeField] private bool useMockIfUninjected = false;
        [SerializeField] private string defaultApiUrl = "http://localhost:5000/api";

        [Header("Theme Palettes")]
        [SerializeField] private LoginThemePaletteSO darkThemePalette;
        [SerializeField] private LoginThemePaletteSO lightThemePalette;
        [SerializeField] private Image screenBackground;
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image cardBorder;

        [Header("Header & Theme Toggle")]
        [SerializeField] private Button themeToggleBtn;
        [SerializeField] private TextMeshProUGUI themeToggleIcon;
        [SerializeField] private Image themeToggleBtnBg;
        [SerializeField] private Image themeToggleBtnBorder;
        [SerializeField] private TextMeshProUGUI brandTitle;
        [SerializeField] private Image brandLogoBg;
        [SerializeField] private TextMeshProUGUI brandLogoIcon;

        [Header("Alert Feedback Banner")]
        [SerializeField] private GameObject alertBox;
        [SerializeField] private Image alertBg;
        [SerializeField] private Image alertBorder;
        [SerializeField] private TextMeshProUGUI alertText;

        [Header("Social Sign-In")]
        [SerializeField] private SocialLoginButtonUGUI googleLoginBtn;
        [SerializeField] private SocialLoginButtonUGUI facebookLoginBtn;

        [Header("Divider")]
        [SerializeField] private Image dividerLineLeft;
        [SerializeField] private Image dividerLineRight;
        [SerializeField] private Image dividerBadgeBg;
        [SerializeField] private TextMeshProUGUI dividerBadgeText;

        [Header("Card Typography")]
        [SerializeField] private TextMeshProUGUI cardTitle;
        [SerializeField] private TextMeshProUGUI cardSubtitle;

        [Header("Form - Email Field")]
        [SerializeField] private TextMeshProUGUI emailLabel;
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private Image emailInputBg;
        [SerializeField] private Image emailInputBorder;
        [SerializeField] private TextMeshProUGUI emailError;

        [Header("Form - Password Field")]
        [SerializeField] private TextMeshProUGUI passwordLabel;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Image passwordInputBg;
        [SerializeField] private Image passwordInputBorder;
        [SerializeField] private TextMeshProUGUI passwordError;
        [SerializeField] private Button togglePasswordBtn;
        [SerializeField] private TextMeshProUGUI togglePasswordIcon;
        [SerializeField] private Button forgotPasswordBtn;
        [SerializeField] private TextMeshProUGUI forgotPasswordBtnText;

        [Header("Form - Remember Me")]
        [SerializeField] private GameObject rememberRow;
        [SerializeField] private Toggle rememberToggle;
        [SerializeField] private TextMeshProUGUI rememberToggleLabel;

        [Header("Form - Submit Button")]
        [SerializeField] private Button submitBtn;
        [SerializeField] private Image submitBtnBg;
        [SerializeField] private TextMeshProUGUI submitBtnText;
        [SerializeField] private GameObject submitSpinner;
        [SerializeField] private TextMeshProUGUI submitSpinnerText;

        [Header("Footer & Secondary Actions")]
        [SerializeField] private GameObject signupPromptRow;
        [SerializeField] private TextMeshProUGUI signupPromptText;
        [SerializeField] private Button signUpBtn;
        [SerializeField] private TextMeshProUGUI signUpBtnText;
        [SerializeField] private TextMeshProUGUI copyrightText;
        [SerializeField] private Button privacyBtn;
        [SerializeField] private TextMeshProUGUI privacyBtnText;
        [SerializeField] private Button termsBtn;
        [SerializeField] private TextMeshProUGUI termsBtnText;
        [SerializeField] private Button supportBtn;
        [SerializeField] private TextMeshProUGUI supportBtnText;

        [Inject]
        private IAuthService authService;

        private bool isDarkTheme = true;
        private bool isRegisterMode = false;
        private bool isSubmitting = false;
        private bool isPasswordVisible = false;
        private Coroutine alertDismissCoroutine;
        private Coroutine spinnerCoroutine;

        // Events for external decoupling
        public event Action<string, string, bool> OnLoginRequested;
        public event Action<string, string> OnRegisterRequested;
        public event Action<SocialAuthProvider, string> OnSocialLoginRequested;
        public event Action<string> OnForgotPasswordRequested;

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private void Awake() {
            EnsureAuthService();
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if ( !Application.isPlaying ) {
                isDarkTheme = startWithDarkTheme;
                ApplyThemeVisuals();
                UpdateModeVisuals();
            }
        }
#endif

        private void EnsureAuthService() {
            if ( authService == null ) {
                IPlayerRepository repo = useMockIfUninjected
                    ? new MockPlayerRepository()
                    : new RestPlayerRepository(defaultApiUrl);
                authService = new AuthService(repo);
            }
        }

        private void OnEnable() {
            BindUI();
            isDarkTheme = startWithDarkTheme;
            ApplyThemeVisuals();
            UpdateModeVisuals();
            HideAlert();
            ClearAllFieldErrors();
        }

        private void OnDisable() {
            UnbindUI();
        }

        private void BindUI() {
            if ( themeToggleBtn != null ) themeToggleBtn.onClick.AddListener(ToggleTheme);
            if ( togglePasswordBtn != null ) togglePasswordBtn.onClick.AddListener(TogglePasswordVisibility);
            if ( forgotPasswordBtn != null ) forgotPasswordBtn.onClick.AddListener(HandleForgotPassword);
            if ( submitBtn != null ) submitBtn.onClick.AddListener(HandleSubmit);
            if ( signUpBtn != null ) signUpBtn.onClick.AddListener(ToggleAuthMode);
            if ( privacyBtn != null ) privacyBtn.onClick.AddListener(HandlePrivacyPolicy);
            if ( termsBtn != null ) termsBtn.onClick.AddListener(HandleTermsOfService);
            if ( supportBtn != null ) supportBtn.onClick.AddListener(HandleSupport);

            if ( emailInput != null ) {
                emailInput.onValueChanged.AddListener(_ => ClearFieldError(emailInputBorder, emailError));
            }

            if ( passwordInput != null ) {
                passwordInput.onValueChanged.AddListener(_ => ClearFieldError(passwordInputBorder, passwordError));
            }

            if ( googleLoginBtn != null ) {
                googleLoginBtn.OnClicked += HandleSocialLogin;
            }

            if ( facebookLoginBtn != null ) {
                facebookLoginBtn.OnClicked += HandleSocialLogin;
            }
        }

        private void UnbindUI() {
            if ( themeToggleBtn != null ) themeToggleBtn.onClick.RemoveListener(ToggleTheme);
            if ( togglePasswordBtn != null ) togglePasswordBtn.onClick.RemoveListener(TogglePasswordVisibility);
            if ( forgotPasswordBtn != null ) forgotPasswordBtn.onClick.RemoveListener(HandleForgotPassword);
            if ( submitBtn != null ) submitBtn.onClick.RemoveListener(HandleSubmit);
            if ( signUpBtn != null ) signUpBtn.onClick.RemoveListener(ToggleAuthMode);
            if ( privacyBtn != null ) privacyBtn.onClick.RemoveListener(HandlePrivacyPolicy);
            if ( termsBtn != null ) termsBtn.onClick.RemoveListener(HandleTermsOfService);
            if ( supportBtn != null ) supportBtn.onClick.RemoveListener(HandleSupport);

            if ( emailInput != null ) emailInput.onValueChanged.RemoveAllListeners();
            if ( passwordInput != null ) passwordInput.onValueChanged.RemoveAllListeners();

            if ( googleLoginBtn != null ) {
                googleLoginBtn.OnClicked -= HandleSocialLogin;
            }

            if ( facebookLoginBtn != null ) {
                facebookLoginBtn.OnClicked -= HandleSocialLogin;
            }
        }

        public void ToggleAuthMode() {
            isRegisterMode = !isRegisterMode;
            UpdateModeVisuals();
            HideAlert();
            ClearAllFieldErrors();
        }

        private void UpdateModeVisuals() {
            if ( isRegisterMode ) {
                if ( cardTitle != null ) cardTitle.text = "Create an Account";
                if ( cardSubtitle != null ) cardSubtitle.text = "Enter your email & password to sign up";
                if ( submitBtnText != null ) submitBtnText.text = "Sign up";
                if ( signupPromptText != null ) signupPromptText.text = "Already have an account?";
                if ( signUpBtnText != null ) signUpBtnText.text = "Sign in";
                if ( rememberRow != null ) rememberRow.SetActive(false);
            }
            else {
                if ( cardTitle != null ) cardTitle.text = "Welcome back";
                if ( cardSubtitle != null ) cardSubtitle.text = "Enter your details to access your account";
                if ( submitBtnText != null ) submitBtnText.text = "Sign in";
                if ( signupPromptText != null ) signupPromptText.text = "Don't have an account?";
                if ( signUpBtnText != null ) signUpBtnText.text = "Create an account";
                if ( rememberRow != null ) rememberRow.SetActive(true);
            }
        }

        public void HandleSubmit() {
            if ( isSubmitting ) return;

            HideAlert();

            string email = emailInput != null ? emailInput.text.Trim() : string.Empty;
            string password = passwordInput != null ? passwordInput.text : string.Empty;
            bool rememberMe = rememberToggle != null && rememberToggle.isOn;

            bool isValid = true;

            if ( string.IsNullOrEmpty(email) || !EmailRegex.IsMatch(email) ) {
                ShowFieldError(emailInputBorder, emailError, "Please enter a valid email address.");
                isValid = false;
            }
            else {
                ClearFieldError(emailInputBorder, emailError);
            }

            if ( string.IsNullOrEmpty(password) || password.Length < 6 ) {
                ShowFieldError(passwordInputBorder, passwordError, "Password must be at least 6 characters.");
                isValid = false;
            }
            else {
                ClearFieldError(passwordInputBorder, passwordError);
            }

            if ( !isValid ) {
                ShowAlert("Please check your email and password.", AlertType.Error);
                return;
            }

            if ( isRegisterMode ) {
                PerformRegisterAsync(email, password).Forget();
            }
            else {
                PerformSignInAsync(email, password, rememberMe).Forget();
            }
        }

        private async UniTaskVoid PerformSignInAsync( string email, string password, bool rememberMe ) {
            SetSubmittingState(true);
            OnLoginRequested?.Invoke(email, password, rememberMe);

            try {
                EnsureAuthService();
                var response = await authService.LoginAsync(email, password);

                SetSubmittingState(false);

                if ( response != null && response.isSuccess ) {
                    string name = response.profile?.displayName ?? "Player";
                    ShowAlert($"Signed in successfully! Welcome back, {name}.", AlertType.Success, 2f);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.8f));
                    NavigateToNextScene();
                }
                else {
                    string error = response?.errorMessage;
                    if ( string.IsNullOrEmpty(error) ) {
                        error = "Unable to connect to server. Ensure backend is running at " + defaultApiUrl;
                    }
                    ShowAlert(error, AlertType.Error, 5f);
                }
            }
            catch ( Exception ex ) {
                SetSubmittingState(false);
                ShowAlert($"Connection error: {ex.Message}", AlertType.Error, 5f);
            }
        }

        private async UniTaskVoid PerformRegisterAsync( string email, string password ) {
            SetSubmittingState(true);
            OnRegisterRequested?.Invoke(email, password);

            try {
                EnsureAuthService();
                var response = await authService.RegisterAsync(email, password);

                SetSubmittingState(false);

                if ( response != null && response.isSuccess ) {
                    string name = response.profile?.displayName ?? "Player";
                    ShowAlert($"Account created successfully! Welcome, {name}.", AlertType.Success, 2f);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.8f));
                    NavigateToNextScene();
                }
                else {
                    string error = response?.errorMessage;
                    if ( string.IsNullOrEmpty(error) ) {
                        error = "Unable to connect to server. Ensure backend is running at " + defaultApiUrl;
                    }
                    ShowAlert(error, AlertType.Error, 5f);
                }
            }
            catch ( Exception ex ) {
                SetSubmittingState(false);
                ShowAlert($"Connection error: {ex.Message}", AlertType.Error, 5f);
            }
        }

        private void NavigateToNextScene() {
            if ( !string.IsNullOrEmpty(nextSceneName) ) {
                try {
                    SceneManager.LoadScene(nextSceneName);
                }
                catch ( Exception ex ) {
                    Debug.LogWarning($"[LoginPageController] Could not load scene '{nextSceneName}': {ex.Message}");
                }
            }
        }

        public void HandleSocialLogin( SocialAuthProvider provider, string providerName ) {
            ShowAlert($"Connecting with {providerName}...", AlertType.Info, 2.5f);
            OnSocialLoginRequested?.Invoke(provider, providerName);
        }

        public void HandleForgotPassword() {
            string email = emailInput != null ? emailInput.text.Trim() : string.Empty;

            if ( string.IsNullOrEmpty(email) || !EmailRegex.IsMatch(email) ) {
                ShowFieldError(emailInputBorder, emailError, "Please enter a valid email address first.");
                ShowAlert("Please provide a valid email address to send the reset link.", AlertType.Error);
                return;
            }

            ShowAlert($"Password reset instructions sent to {email}", AlertType.Success, 4f);
            OnForgotPasswordRequested?.Invoke(email);
        }

        public void TogglePasswordVisibility() {
            if ( passwordInput == null ) return;

            isPasswordVisible = !isPasswordVisible;
            passwordInput.contentType = isPasswordVisible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;

            passwordInput.ForceLabelUpdate();

            if ( togglePasswordIcon != null ) {
                togglePasswordIcon.text = isPasswordVisible ? "HIDE" : "SHOW";
            }
        }

        public void ToggleTheme() {
            isDarkTheme = !isDarkTheme;
            ApplyThemeVisuals();
        }

        private void ApplyThemeVisuals() {
            LoginThemePaletteSO palette = isDarkTheme ? darkThemePalette : lightThemePalette;

            if ( themeToggleIcon != null ) {
                themeToggleIcon.text = isDarkTheme ? "☼" : "☽";
            }

            if ( palette == null ) return;

            if ( screenBackground != null ) screenBackground.color = palette.screenBackgroundColor;
            if ( cardBackground != null ) cardBackground.color = palette.cardBackgroundColor;
            if ( cardBorder != null ) cardBorder.color = palette.cardBorderColor;

            if ( brandTitle != null ) brandTitle.color = palette.titleTextColor;
            if ( brandLogoBg != null ) brandLogoBg.color = palette.submitButtonBg;
            if ( brandLogoIcon != null ) brandLogoIcon.color = palette.submitButtonText;

            if ( themeToggleBtnBg != null ) themeToggleBtnBg.color = palette.socialButtonBg;
            if ( themeToggleBtnBorder != null ) themeToggleBtnBorder.color = palette.socialButtonBorder;
            if ( themeToggleIcon != null ) themeToggleIcon.color = palette.secondaryTextColor;

            if ( cardTitle != null ) cardTitle.color = palette.titleTextColor;
            if ( cardSubtitle != null ) cardSubtitle.color = palette.subtitleTextColor;

            if ( emailLabel != null ) emailLabel.color = palette.labelTextColor;
            if ( emailInputBg != null ) emailInputBg.color = palette.inputBackgroundColor;
            if ( emailInputBorder != null ) emailInputBorder.color = palette.inputBorderColor;
            if ( emailInput != null && emailInput.textComponent != null ) emailInput.textComponent.color = palette.inputTextColor;
            if ( emailInput != null && emailInput.placeholder is TextMeshProUGUI emailPh ) emailPh.color = palette.inputPlaceholderColor;

            if ( passwordLabel != null ) passwordLabel.color = palette.labelTextColor;
            if ( passwordInputBg != null ) passwordInputBg.color = palette.inputBackgroundColor;
            if ( passwordInputBorder != null ) passwordInputBorder.color = palette.inputBorderColor;
            if ( passwordInput != null && passwordInput.textComponent != null ) passwordInput.textComponent.color = palette.inputTextColor;
            if ( passwordInput != null && passwordInput.placeholder is TextMeshProUGUI passPh ) passPh.color = palette.inputPlaceholderColor;
            if ( togglePasswordIcon != null ) togglePasswordIcon.color = palette.secondaryTextColor;
            if ( forgotPasswordBtnText != null ) forgotPasswordBtnText.color = palette.secondaryTextColor;

            if ( rememberToggleLabel != null ) rememberToggleLabel.color = palette.secondaryTextColor;

            if ( submitBtnBg != null ) submitBtnBg.color = palette.submitButtonBg;
            if ( submitBtnText != null ) submitBtnText.color = palette.submitButtonText;

            if ( dividerLineLeft != null ) dividerLineLeft.color = palette.dividerLineColor;
            if ( dividerLineRight != null ) dividerLineRight.color = palette.dividerLineColor;
            if ( dividerBadgeBg != null ) dividerBadgeBg.color = palette.dividerBadgeBg;
            if ( dividerBadgeText != null ) dividerBadgeText.color = palette.dividerBadgeText;

            if ( googleLoginBtn != null ) {
                googleLoginBtn.ApplyThemeColors(palette.socialButtonBg, palette.socialButtonBorder, palette.socialButtonText);
            }
            if ( facebookLoginBtn != null ) {
                facebookLoginBtn.ApplyThemeColors(palette.socialButtonBg, palette.socialButtonBorder, palette.socialButtonText);
            }

            if ( signupPromptText != null ) signupPromptText.color = palette.secondaryTextColor;
            if ( signUpBtnText != null ) signUpBtnText.color = palette.titleTextColor;

            if ( copyrightText != null ) copyrightText.color = palette.secondaryTextColor;
            if ( privacyBtnText != null ) privacyBtnText.color = palette.secondaryTextColor;
            if ( termsBtnText != null ) termsBtnText.color = palette.secondaryTextColor;
            if ( supportBtnText != null ) supportBtnText.color = palette.secondaryTextColor;
        }

        public void ShowAlert( string message, AlertType type, float autoHideDuration = 0f ) {
            if ( alertBox == null || alertText == null ) return;

            if ( alertDismissCoroutine != null ) {
                StopCoroutine(alertDismissCoroutine);
                alertDismissCoroutine = null;
            }

            LoginThemePaletteSO palette = isDarkTheme ? darkThemePalette : lightThemePalette;

            if ( palette != null && alertBg != null ) {
                switch ( type ) {
                    case AlertType.Error:
                        alertBg.color = palette.alertErrorBg;
                        alertText.color = palette.alertErrorText;
                        if ( alertBorder != null ) alertBorder.color = new Color(palette.alertErrorText.r, palette.alertErrorText.g, palette.alertErrorText.b, 0.35f);
                        break;
                    case AlertType.Success:
                    case AlertType.Info:
                        alertBg.color = palette.alertSuccessBg;
                        alertText.color = palette.alertSuccessText;
                        if ( alertBorder != null ) alertBorder.color = new Color(palette.alertSuccessText.r, palette.alertSuccessText.g, palette.alertSuccessText.b, 0.35f);
                        break;
                }
            }

            alertText.text = message;
            alertBox.SetActive(true);

            if ( autoHideDuration > 0f ) {
                alertDismissCoroutine = StartCoroutine(DismissAlertAfter(autoHideDuration));
            }
        }

        public void HideAlert() {
            if ( alertBox != null ) {
                alertBox.SetActive(false);
            }
        }

        private IEnumerator DismissAlertAfter( float seconds ) {
            yield return new WaitForSeconds(seconds);
            HideAlert();
            alertDismissCoroutine = null;
        }

        private void ShowFieldError( Image border, TextMeshProUGUI errorLabel, string message ) {
            if ( border != null ) {
                border.color = new Color32(244, 63, 94, 255);
            }
            if ( errorLabel != null ) {
                errorLabel.text = message;
                errorLabel.gameObject.SetActive(true);
            }
        }

        private void ClearFieldError( Image border, TextMeshProUGUI errorLabel ) {
            if ( border != null ) {
                LoginThemePaletteSO palette = isDarkTheme ? darkThemePalette : lightThemePalette;
                if ( palette != null ) {
                    border.color = palette.inputBorderColor;
                }
            }
            if ( errorLabel != null ) {
                errorLabel.gameObject.SetActive(false);
            }
        }

        private void ClearAllFieldErrors() {
            ClearFieldError(emailInputBorder, emailError);
            ClearFieldError(passwordInputBorder, passwordError);
        }

        private void SetSubmittingState( bool submitting ) {
            isSubmitting = submitting;

            if ( submitBtn != null ) {
                submitBtn.interactable = !submitting;
            }

            if ( submitBtnText != null ) {
                submitBtnText.text = submitting
                    ? (isRegisterMode ? "Registering..." : "Authenticating...")
                    : (isRegisterMode ? "Sign up" : "Sign in");
            }

            if ( submitSpinner != null ) {
                submitSpinner.SetActive(submitting);
                if ( submitting ) {
                    if ( spinnerCoroutine != null ) StopCoroutine(spinnerCoroutine);
                    spinnerCoroutine = StartCoroutine(AnimateSpinner());
                }
                else {
                    if ( spinnerCoroutine != null ) {
                        StopCoroutine(spinnerCoroutine);
                        spinnerCoroutine = null;
                    }
                }
            }
        }

        private IEnumerator AnimateSpinner() {
            if ( submitSpinner == null ) yield break;
            RectTransform spinnerRect = submitSpinner.GetComponent<RectTransform>();
            while ( isSubmitting ) {
                if ( spinnerRect != null ) {
                    spinnerRect.Rotate(0f, 0f, -360f * Time.deltaTime * 2f);
                }
                yield return null;
            }
        }

        public void HandlePrivacyPolicy() {
            Debug.Log("[LoginPageController] Privacy Policy clicked.");
        }

        public void HandleTermsOfService() {
            Debug.Log("[LoginPageController] Terms of Service clicked.");
        }

        public void HandleSupport() {
            Debug.Log("[LoginPageController] Support clicked.");
        }
    }
}
