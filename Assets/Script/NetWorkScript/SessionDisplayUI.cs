using TMPro;
using UnityEngine;

namespace Assets.Script.NetWorkScript {
    /// <summary>
    /// Dumb view: renders whatever session name UiManager passes down.
    /// Knows nothing about Runner, seats or lobby logic.
    /// </summary>
    public class SessionDisplayUI : MonoBehaviour {

        [SerializeField] private TMP_Text sessionNameText;
        [SerializeField, Tooltip("Optional. Assign a Text element to show announcements (e.g. winner).")]
        private TMP_Text announcementText;

        public void RenderSession( string sessionName ) {
            if ( sessionNameText == null ) return;

            sessionNameText.text = string.IsNullOrEmpty(sessionName)
                ? "Room: ..."
                : $"Room: {sessionName}";
        }

        public void RenderAnnouncement( string message ) {
            if ( announcementText == null ) return;
            announcementText.text = message ?? string.Empty;
        }
    }
}
