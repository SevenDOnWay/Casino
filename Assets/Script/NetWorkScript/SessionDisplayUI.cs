using Fusion;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Assets.Script.NetWorkScript {
    public class SessionDisplayUI : NetworkBehaviour {

        [SerializeField] private TMP_Text sessionNameText;

        public override void Spawned() {
            UpdateSessionDisplay();
        }

        private void UpdateSessionDisplay() {
            if ( Runner != null && Runner.SessionInfo.IsValid ) {
                string sessionName = Runner.SessionInfo.Name;
                sessionNameText.text = $"Room: {sessionName}";
            }
        }
    }
}