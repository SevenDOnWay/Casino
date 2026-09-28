using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.TienLen.UI {
    /// <summary>
    /// Dumb view: renders whatever lobby state UiManager passes down.
    /// Knows nothing about seats, authority or start rules. The click
    /// meaning is owned by UiManager, which wires <see cref="StartButton"/>.
    /// </summary>
    public class StartGameUI : MonoBehaviour {
        [SerializeField] private Button startBtn;
        [SerializeField] private TextMeshProUGUI startBtnText;

        public Button StartButton => startBtn;

        public void RenderStartButton( int playerCount, bool isHost, bool canStart, bool isGameStarted ) {
            if ( startBtn == null || startBtnText == null )
                return;

            startBtn.gameObject.SetActive(!isGameStarted);

            if ( isGameStarted ) {
                startBtnText.text = "Game Started";
                return;
            }

            // Clients can never click the button.
            startBtn.interactable = isHost && canStart;

            if ( !isHost ) {
                startBtnText.text = "Wait for host to start";
            }
            else if ( canStart ) {
                startBtnText.text = $"Start Game ({playerCount})";
            }
            else {
                startBtnText.text = $"Wait for player join ({playerCount})";
            }
        }


    }
}
