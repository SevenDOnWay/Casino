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


        [Header("Next Scene")]
        string nextSceneName = "MainMenuScene";

        private bool isSignInMode = true;

        [Inject]
        void Construct( IAuthService authService ) {
            this.authService = authService;
        }

        void Awake() {
            // SignInScene owns the persistent root container, pull the auth session from it.
            RootLifeTimeScope.Ensure()?.InjectGameObject(gameObject);
        }

        void OnEnable() {
            submitButton.onClick.AddListener(OnSubmitClicked);
            changeSubmitButton.onClick.AddListener(OnChangeSubmitClicked);
        }

        private void OnSubmitClicked() {
            ReadInput(out string email, out string password, out bool rememberMe);
            
            if ( !ValidateInput(email, password) ) {
                ShowAlert("Please fill in all required fields.", AlertType.Error, 5f);
                return;
            }

            AuthRequest dto = CreateAuthRequest(email, password);
            SendRequest(dto);

            Debug.Log($"Sign In Clicked: Email={email}, Password={password}, RememberMe={rememberMe}");
        }

        private async void SendRequest( AuthRequest request ) {
            try {
                var response = await authService.LoginAsync(request.email, request.password); //TODO: Change this to RegisterAsync if isSignInMode is false, and handle the response accordingly.

                if ( response != null && response.isSuccess ) {
                    string name = response.profile?.displayName ?? "Player";
                    ShowAlert($"Signed in successfully! Welcome back, {name}.", AlertType.Success, 2f);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.8f));
                    NavigateToNextScene();
                }
                else {
                    string error = response?.errorMessage;
                    if ( string.IsNullOrEmpty(error) ) {
                        error = "Unable to connect to server. Ensure backend is running at ";
                    }
                    ShowAlert(error, AlertType.Error, 5f);
                }
            }
            catch ( Exception ex ) {
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
            email = emailInputField.text;
            password = passwordInputField.text;
            rememberMe = rememberMeToggle.isOn;
        }

        private void OnChangeSubmitClicked() {
            isSignInMode = !isSignInMode;

            askButton.text = isSignInMode ? "Don't have an account? Sign Up" : "Already have an account? Sign In";
            changeSubmitButtonText.text = isSignInMode ? "Sign In" : "Sign Up";
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
            StartCoroutine(ShowAlertCoroutine(s, type, duration));
        }

        IEnumerator ShowAlertCoroutine( string message, AlertType type, float duration ) {
            alertPanel.SetActive(true);
            alertText.text = message;

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


            yield return new WaitForSeconds(duration);

            alertPanel.SetActive(false);
        }

        private void NavigateToNextScene() {
            SceneManager.LoadSceneAsync(nextSceneName);
        }
    }

}