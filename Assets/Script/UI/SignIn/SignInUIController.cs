using Assets.Script.Data.Models;
using Assets.Script.Data.Services;
using Assets.Script.TienLen.UI;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu {
    public class SignInUIController : MonoBehaviour {
        [Header("Dependencies")]
        IAuthService authService;

        [Header("UI Components")]
        [SerializeField] TMP_InputField emailInputField;
        [SerializeField] TMP_InputField passwordInputField;
        [SerializeField] Toggle rememberMeToggle;
        [SerializeField] Button submitButton;
        [SerializeField] TextMeshProUGUI askButton; //this button use to modify the text to "Don't have an account? Sign Up" or "Already have an account? Sign In"
        [SerializeField] Button changeSubmitButton;
        [SerializeField] TextMeshProUGUI changeSubmitButtonText;
        [SerializeField] GameObject alertPanel;
        [SerializeField] Image alertPanelImage;
        [SerializeField] TextMeshProUGUI alertText;
        [SerializeField] GameObject loadingScreen;

        [Header("Next Scene")]
        string nextSceneName = "MainMenuScene";

        private bool isSignInMode = true;

        [Inject]
        void Construct( IAuthService authService ) {
            this.authService = authService;
        }

        void OnEnable() {
            if ( submitButton != null ) submitButton.onClick.AddListener(OnSubmitClicked);
            if ( changeSubmitButton != null ) changeSubmitButton.onClick.AddListener(OnChangeSubmitClicked);
        }

        void OnDisable() {
            if ( submitButton != null ) submitButton.onClick.RemoveListener(OnSubmitClicked);
            if ( changeSubmitButton != null ) changeSubmitButton.onClick.RemoveListener(OnChangeSubmitClicked);
        }

        private void OnSubmitClicked() {
            ReadInput(out string email, out string password, out bool rememberMe);

            if ( !ValidateInput(email, password) ) {
                ShowAlert("Please fill in all required fields.", AlertType.Error, 5f);
                return;
            }

            if ( loadingScreen != null ) loadingScreen.SetActive(true);

#if UNITY_EDITOR
            CheckForAdmin();
#endif

            AuthRequest dto = CreateAuthRequest(email, password);
            SendRequest(dto);

            Debug.Log($"Sign In Clicked: Email={email}, Password={password}, RememberMe={rememberMe}");
        }

        private async void SendRequest( AuthRequest request ) {
            try {
                var response = isSignInMode
                    ? await authService.LoginAsync(request.email, request.password)
                    : await authService.RegisterAsync(request.email, request.password);

                if ( response != null && response.isSuccess ) {
                    string name = response.profile?.displayName ?? "Player";
                    string actionMsg = isSignInMode ? "Signed in successfully" : "Registered successfully";
                    ShowAlert($"{actionMsg}! Welcome, {name}.", AlertType.Success, 2f);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.8f));
                    if ( loadingScreen != null ) loadingScreen.SetActive(false);
                    NavigateToNextScene();
                }
                else {
                    if ( loadingScreen != null ) loadingScreen.SetActive(false);
                    string error = response?.errorMessage;
                    if ( string.IsNullOrEmpty(error) ) {
                        error = "Unable to connect to server. Ensure backend is running.";
                    }
                    ShowAlert(error, AlertType.Error, 5f);
                }
            }
            catch ( Exception ex ) {
                if ( loadingScreen != null ) loadingScreen.SetActive(false);
                ShowAlert($"Connection error: {ex.Message}", AlertType.Error, 5f);
            }
        }

        private AuthRequest CreateAuthRequest( string email, string password ) {
            AuthRequest dto;
            if ( isSignInMode ) dto = new LoginRequest { email = email, password = password };
            else dto = new RegisterRequest { email = email, password = password };
            return dto;
        }

        private void ReadInput( out string email, out string password, out bool rememberMe ) {
            email = emailInputField != null ? emailInputField.text : string.Empty;
            password = passwordInputField != null ? passwordInputField.text : string.Empty;
            rememberMe = rememberMeToggle != null && rememberMeToggle.isOn;
        }

        private void OnChangeSubmitClicked() {
            isSignInMode = !isSignInMode;

            if ( askButton != null ) {
                askButton.text = isSignInMode ? "Don't have an account? Sign Up" : "Already have an account? Sign In";
            }
            if ( changeSubmitButtonText != null ) {
                changeSubmitButtonText.text = isSignInMode ? "Sign In" : "Sign Up";
            }
        }

        private bool ValidateInput( string email, string password ) {
            if ( string.IsNullOrEmpty(email) ) {
                Debug.LogError("Email is required.");
                return false;
            }
            if ( string.IsNullOrEmpty(password) ) {
                Debug.LogError("Password is required.");
                return false;
            }
            return true;
        }

        private void ShowAlert( string s, AlertType type, float duration ) {
            if ( loadingScreen != null ) loadingScreen.SetActive(false);
            StartCoroutine(ShowAlertCoroutine(s, type, duration));
        }

        IEnumerator ShowAlertCoroutine( string message, AlertType type, float duration ) {
            if ( alertPanel != null ) alertPanel.SetActive(true);
            if ( alertText != null ) alertText.text = message;

            if ( alertPanelImage != null ) {
                switch ( type ) {
                    case AlertType.Success:
                        alertPanelImage.color = new Color32(69, 129, 18, 255);
                        break;
                    case AlertType.Error:
                        alertPanelImage.color = new Color32(185, 47, 46, 255);
                        break;
                    case AlertType.Info:
                        alertPanelImage.color = new Color32(212, 231, 177, 255);
                        break;
                }
            }

            yield return new WaitForSeconds(duration);

            if ( alertPanel != null ) alertPanel.SetActive(false);
        }

        private void NavigateToNextScene() {
            SceneManager.LoadSceneAsync(nextSceneName);
        }

        private void CheckForAdmin() {
            if ( emailInputField != null && passwordInputField != null &&
                 emailInputField.text == "admin" && passwordInputField.text == "admin" ) {
                Debug.Log("Admin credentials detected. Navigating to AdminScene.");
                NavigateToNextScene();
            }
        }
    }
}
