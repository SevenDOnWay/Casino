using TMPro;
using UnityEngine;

namespace Assets.Script.NetWorkScript {
    /// <summary>
    /// Dumb view: renders whatever session name UiManager passes down.
    /// Knows nothing about Runner, seats or lobby logic.
    /// </summary>
    public class SessionDisplayUI : MonoBehaviour {

        [SerializeField] private TMP_Text sessionNameText;

        public void RenderSession( string sessionName ) {
            if ( sessionNameText == null ) return;

            sessionNameText.text = string.IsNullOrEmpty(sessionName)
                ? "Room: ..."
                : $"Room: {sessionName}";
        }
    }
}
