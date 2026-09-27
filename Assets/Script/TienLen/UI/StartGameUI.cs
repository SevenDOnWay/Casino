using Assets.Script.TienLen.Game;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class StartGameUI : MonoBehaviour {
        [Header("Dependencies")]
        private TienLenGameController tienLenGameController;


        [SerializeField] private Button startBtn;
        [SerializeField] private TextMeshProUGUI startBtnText;

        [Inject]
        void Construct( TienLenGameController tienLenGameController ) {
            this.tienLenGameController = tienLenGameController;
        }


        private void OnEnable() {
            startBtn.onClick.AddListener(OnStartGameClicked);
            tienLenGameController.OnLobbyChanged += RefreshStartButton;

        }

        private void OnDisable() {
            startBtn.onClick.RemoveListener(OnStartGameClicked);
            tienLenGameController.OnLobbyChanged -= RefreshStartButton;
        }

        private void Start() {
            //RefreshStartButton();
        }


        private void OnStartGameClicked() {
            // Only host (state authority) is allowed to start.
            // Clients have interactable = false, but guard here anyway.
            if ( tienLenGameController == null ) return;
            if ( !IsHost() ) return;

            if ( !IsGameStartable() ) {
                RefreshStartButton();
                return;
            }

            startBtnText.text = "Starting...";
            startBtn.interactable = false;

            tienLenGameController.StartGame();
        }

        private bool IsHost() {
            if ( tienLenGameController == null || tienLenGameController.Object == null )
                return false;
            return tienLenGameController.Object.HasStateAuthority;
        }

        /// <summary>
        /// Single place that queries TienLenGameController.CanStartGame(),
        /// which counts registered TienLenNetWorkPlayers (each has a PlayerRef).
        /// </summary>
        private bool IsGameStartable() {
            if ( tienLenGameController == null )
                return false;

            return tienLenGameController.CanStartGame();
        }

        /// <summary>
        /// Called on enable + every time the replicated seat list changes,
        /// so host + clients stay in sync without polling Runner.ActivePlayers.
        /// </summary>
        public void RefreshStartButton() {
            if ( startBtn == null || startBtnText == null )
                return;

            bool isHost = IsHost();
            bool startable = IsGameStartable();

            // Clients can never click the button.
            startBtn.interactable = isHost && startable;

            if ( tienLenGameController != null && tienLenGameController.IsGameStarted ) {
                startBtnText.text = "Game Started";
                startBtn.gameObject.SetActive(false);
                return;
            }

            if ( !isHost ) {
                startBtnText.text = "Wait for host to start";
            }
            else if ( startable ) {
                startBtnText.text = $"Start Game ({tienLenGameController.RegisteredPlayerCount})";
            }
            else {
                startBtnText.text = $"Wait for player join ({tienLenGameController?.RegisteredPlayerCount ?? 0})";
            }
        }


    }
}
