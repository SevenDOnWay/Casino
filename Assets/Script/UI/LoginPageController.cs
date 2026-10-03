using System;
using System.Collections;
using System.Text.RegularExpressions;
using Assets.Script.Data.Models;
using Assets.Script.Data.Repositories;
using Assets.Script.Data.Services;
using Assets.Script.UI.Components;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VContainer;

namespace Assets.Script.UI {
    public enum AlertType {
        Success,
        Error,
        Info
    }

    /// <summary>
    /// UI Toolkit controller managing the modern login & register interface.
    /// Supports email/password authentication, validation, mode toggling, and backend integration.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LoginPageController : MonoBehaviour {
        [Header("Configuration")]
        [SerializeField] private bool startWithDarkTheme = true;
        [SerializeField] private string nextSceneName = "MainMenuScene";
        [SerializeField] private bool useMockIfUninjected = false;
        [SerializeField] private string defaultApiUrl = "http://localhost:5000/api";

        [Inject]
        private IAuthService authService;

        // Visual Tree References
        private UIDocument uiDocument;
        private VisualElement root;

        // Header & Theme
        private Button themeToggleBtn;
        private Label themeToggleIcon;
        private bool isDarkTheme = true;

        // Alerts & Feedback
        private VisualElement alertBox;
        private Label alertText;
        private Coroutine alertDismissCoroutine;

        // Form Elements
        private Label cardTitle;
        private Label cardSubtitle;
        private TextField emailInput;
        private Label emailError;
        private TextField passwordInput;
        private Label passwordError;
        private Button togglePasswordBtn;
        private Label togglePasswordIcon;
        private VisualElement rememberRow;
        private Toggle rememberToggle;
        private Button forgotPasswordBtn;

        // Submit Button & Loading Indicator
        private Button submitBtn;
        private Label submitBtnText;
        private Label submitSpinner;
        private bool isSubmitting;
        private bool isPasswordVisible;

        // Footer & Secondary Actions
        private VisualElement signupPromptRow;
        private Label signupPromptText;
        private Button signUpBtn;
        private Button privacyBtn;
        private Button termsBtn;
        private Button supportBtn;

        private bool isRegisterMode = false;

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
            uiDocument = GetComponent<UIDocument>();
            EnsureAuthService();
        }

        private void EnsureAuthService() {
            if ( authService == null ) {
                IPlayerRepository repo = useMockIfUninjected
                    ? new MockPlayerRepository()
                    : new RestPlayerRepository(defaultApiUrl);
                authService = new AuthService(repo);
            }
        }

        private void OnEnable() {
            InitializeUI();
        }

        private void OnDisable() {
            UnbindUI();
        }

        /// <summary>
        /// Queries and binds all UI Toolkit visual elements.
        /// </summary>
        public void InitializeUI() {
            if ( uiDocument == null ) {
                uiDocument = GetComponent<UIDocument>();
            }

            if ( uiDocument == null || uiDocument.rootVisualElement == null ) {
                return;
            }

            root = uiDocument.rootVisualElement;

            // 1. Header & Theme
            themeToggleBtn = root.Q<Button>("theme-toggle-btn");
            themeToggleIcon = root.Q<Label>("theme-toggle-icon");
            if ( themeToggleBtn != null ) {
                themeToggleBtn.clicked += ToggleTheme;
            }

            isDarkTheme = startWithDarkTheme;
            ApplyThemeVisuals();

            // 2. Alert Box
            alertBox = root.Q<VisualElement>("alert-box");
            alertText = root.Q<Label>("alert-text");
            HideAlert();

            // 3. Social Sign-In Buttons
            BindSocialButtons();

            // 4. Form Controls
            cardTitle = root.Q<Label>(className: "auth-card__title");
            cardSubtitle = root.Q<Label>(className: "auth-card__subtitle");

            emailInput = root.Q<TextField>("email-input");
            emailError = root.Q<Label>("email-error");

            passwordInput = root.Q<TextField>("password-input");
            passwordError = root.Q<Label>("password-error");
            togglePasswordBtn = root.Q<Button>("toggle-password-btn");
            togglePasswordIcon = root.Q<Label>("toggle-password-icon");
            forgotPasswordBtn = root.Q<Button>("forgot-password-btn");

            rememberRow = root.Q<VisualElement>("remember-row");
            rememberToggle = root.Q<Toggle>("remember-toggle");

            if ( togglePasswordBtn != null ) togglePasswordBtn.clicked += TogglePasswordVisibility;
            if ( forgotPasswordBtn != null ) forgotPasswordBtn.clicked += HandleForgotPassword;

            if ( emailInput != null ) {
                emailInput.RegisterValueChangedCallback(_ => ClearFieldError(emailInput, emailError));
            }

            if ( passwordInput != null ) {
                passwordInput.RegisterValueChangedCallback(_ => ClearFieldError(passwordInput, passwordError));
            }

            // 5. Submit Button
            submitBtn = root.Q<Button>("submit-btn");
            submitBtnText = root.Q<Label>("submit-btn-text");
            submitSpinner = root.Q<Label>("submit-spinner");

            if ( submitBtn != null ) {
                submitBtn.clicked += HandleSubmit;
            }

            // 6. Footer Links & Mode Toggle
            signupPromptRow = root.Q<VisualElement>("signup-prompt-row");
            signupPromptText = root.Q<Label>(className: "signup-prompt-text");
            signUpBtn = root.Q<Button>("sign-up-btn");
            privacyBtn = root.Q<Button>("privacy-btn");
            termsBtn = root.Q<Button>("terms-btn");
            supportBtn = root.Q<Button>("support-btn");

            if ( signUpBtn != null ) signUpBtn.clicked += ToggleAuthMode;
            if ( privacyBtn != null ) privacyBtn.clicked += HandlePrivacyPolicy;
            if ( termsBtn != null ) termsBtn.clicked += HandleTermsOfService;
            if ( supportBtn != null ) supportBtn.clicked += HandleSupport;

            UpdateModeVisuals();
        }

        private void UnbindUI() {
            if ( themeToggleBtn != null ) themeToggleBtn.clicked -= ToggleTheme;
            if ( togglePasswordBtn != null ) togglePasswordBtn.clicked -= TogglePasswordVisibility;
            if ( forgotPasswordBtn != null ) forgotPasswordBtn.clicked -= HandleForgotPassword;
            if ( submitBtn != null ) submitBtn.clicked -= HandleSubmit;
            if ( signUpBtn != null ) signUpBtn.clicked -= ToggleAuthMode;
            if ( privacyBtn != null ) privacyBtn.clicked -= HandlePrivacyPolicy;
            if ( termsBtn != null ) termsBtn.clicked -= HandleTermsOfService;
            if ( supportBtn != null ) supportBtn.clicked -= HandleSupport;
        }

        private void BindSocialButtons() {
            SocialLoginButton.BindTemplateInstance(
                root,
                "google-login-btn",
                SocialAuthProvider.Google,
                ( provider, name ) => HandleSocialLogin(provider, name)
            );

            SocialLoginButton.BindTemplateInstance(
                root,
                "facebook-login-btn",
                SocialAuthProvider.Facebook,
                ( provider, name ) => HandleSocialLogin(provider, name)
            );
        }

        public void ToggleAuthMode() {
            isRegisterMode = !isRegisterMode;
            UpdateModeVisuals();
            HideAlert();
        }

        private void UpdateModeVisuals() {
            if ( isRegisterMode ) {
                if ( cardTitle != null ) cardTitle.text = "Create an Account";
                if ( cardSubtitle != null ) cardSubtitle.text = "Enter your email & password to sign up";
                if ( submitBtnText != null ) submitBtnText.text = "Sign up";
                if ( signupPromptText != null ) signupPromptText.text = "Already have an account?";
                if ( signUpBtn != null ) signUpBtn.text = "Sign in";
                if ( rememberRow != null ) rememberRow.style.display = DisplayStyle.None;
            }
            else {
                if ( cardTitle != null ) cardTitle.text = "Welcome back";
                if ( cardSubtitle != null ) cardSubtitle.text = "Enter your details to access your account";
                if ( submitBtnText != null ) submitBtnText.text = "Sign in";
                if ( signupPromptText != null ) signupPromptText.text = "Don't have an account?";
                if ( signUpBtn != null ) signUpBtn.text = "Create an account";
                if ( rememberRow != null ) rememberRow.style.display = DisplayStyle.Flex;
            }
        }

        public void HandleSubmit() {
            if ( isSubmitting ) return;

            HideAlert();

            string email = emailInput?.value?.Trim() ?? string.Empty;
            string password = passwordInput?.value ?? string.Empty;
            bool rememberMe = rememberToggle?.value ?? false;

            bool isValid = true;

            if ( string.IsNullOrEmpty(email) || !EmailRegex.IsMatch(email) ) {
                ShowFieldError(emailInput, emailError, "Please enter a valid email address.");
                isValid = false;
            }
            else {
                ClearFieldError(emailInput, emailError);
            }

            if ( string.IsNullOrEmpty(password) || password.Length < 6 ) {
                ShowFieldError(passwordInput, passwordError, "Password must be at least 6 characters.");
                isValid = false;
            }
            else {
                ClearFieldError(passwordInput, passwordError);
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
            string email = emailInput?.value?.Trim() ?? string.Empty;

            if ( string.IsNullOrEmpty(email) || !EmailRegex.IsMatch(email) ) {
                ShowFieldError(emailInput, emailError, "Please enter a valid email address first.");
                ShowAlert("Please provide a valid email address to send the reset link.", AlertType.Error);
                return;
            }

            ShowAlert($"Password reset instructions sent to {email}", AlertType.Success, 4f);
            OnForgotPasswordRequested?.Invoke(email);
        }

        public void TogglePasswordVisibility() {
            if ( passwordInput == null ) return;

            isPasswordVisible = !isPasswordVisible;
            passwordInput.isPasswordField = !isPasswordVisible;

            if ( togglePasswordIcon != null ) {
                togglePasswordIcon.text = isPasswordVisible ? "Ø" : "👁";
            }
        }

        public void ToggleTheme() {
            isDarkTheme = !isDarkTheme;
            ApplyThemeVisuals();
        }

        private void ApplyThemeVisuals() {
            if ( root == null ) return;

            var screen = root.Q<VisualElement>("login-screen");
            if ( screen != null ) {
                if ( isDarkTheme ) {
                    screen.RemoveFromClassList("theme-light");
                    screen.AddToClassList("theme-dark");
                }
                else {
                    screen.RemoveFromClassList("theme-dark");
                    screen.AddToClassList("theme-light");
                }
            }

            if ( themeToggleIcon != null ) {
                themeToggleIcon.text = isDarkTheme ? "☼" : "☽";
            }
        }

        public void ShowAlert( string message, AlertType type, float autoHideDuration = 0f ) {
            if ( alertBox == null || alertText == null ) return;

            if ( alertDismissCoroutine != null ) {
                StopCoroutine(alertDismissCoroutine);
                alertDismissCoroutine = null;
            }

            alertBox.RemoveFromClassList("alert-box--error");
            alertBox.RemoveFromClassList("alert-box--success");
            alertBox.RemoveFromClassList("hidden");

            switch ( type ) {
                case AlertType.Error:
                    alertBox.AddToClassList("alert-box--error");
                    break;
                case AlertType.Success:
                case AlertType.Info:
                    alertBox.AddToClassList("alert-box--success");
                    break;
            }

            alertText.text = message;

            if ( autoHideDuration > 0f ) {
                alertDismissCoroutine = StartCoroutine(DismissAlertAfter(autoHideDuration));
            }
        }

        public void HideAlert() {
            if ( alertBox != null ) {
                alertBox.AddToClassList("hidden");
            }
        }

        private IEnumerator DismissAlertAfter( float seconds ) {
            yield return new WaitForSeconds(seconds);
            HideAlert();
            alertDismissCoroutine = null;
        }

        private void ShowFieldError( TextField field, Label errorLabel, string message ) {
            if ( field != null ) field.AddToClassList("input--error");
            if ( errorLabel != null ) {
                errorLabel.text = message;
                errorLabel.RemoveFromClassList("hidden");
            }
        }

        private void ClearFieldError( TextField field, Label errorLabel ) {
            if ( field != null ) field.RemoveFromClassList("input--error");
            if ( errorLabel != null ) errorLabel.AddToClassList("hidden");
        }

        private void SetSubmittingState( bool submitting ) {
            isSubmitting = submitting;

            if ( submitBtn != null ) {
                submitBtn.SetEnabled(!submitting);
            }

            if ( submitBtnText != null ) {
                submitBtnText.text = submitting
                    ? (isRegisterMode ? "Registering..." : "Authenticating...")
                    : (isRegisterMode ? "Sign up" : "Sign in");
            }

            if ( submitSpinner != null ) {
                if ( submitting ) submitSpinner.RemoveFromClassList("hidden");
                else submitSpinner.AddToClassList("hidden");
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
