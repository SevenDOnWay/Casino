using Assets.Script.NetWorkScript;
using Fusion;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;
using static UnityEngine.Rendering.CoreUtils;

namespace Assets.Script.UI.MainMenu.Panel {
    public class LobbySessionBrowserPanel : MonoBehaviour {

        private sealed class SessionEntry {
            public GameObject GameObject;
            public TableButtonScript TableButtonScript;
            public int index;
        }

        private Dictionary<string, SessionEntry> sessionEntries = new Dictionary<string, SessionEntry>();
        List<SessionInfo>  availableSessions = new List<SessionInfo>();

        [Header("Dependencies")]
        [Inject] TienLenNetworkController networkController;

        [SerializeField] GameObject betButtonPrefab;
        [SerializeField] GameObject sessionButtonPrefab;
        [SerializeField] Transform betButtonContainer;
        [SerializeField] Transform sessionButtonContainer;

        Dictionary<int,int > dic = new Dictionary<int, int>() {
            {0, 5}, {1,10}, {2,20}, {3,50} ,{4, 100 }
        };

        private int selectedStake = 0;

        void Start() {
            SpawnButton();
        }

        void OnEnable() {
            networkController.SessionListUpdated += OnSessionListUpdated;
        }

        void OnDisable() {
            networkController.SessionListUpdated -= OnSessionListUpdated;
        }

        public void SpawnButton() {
            for ( int i = 0; i < dic.Count; i++ ) {
                var button = Instantiate(betButtonPrefab, transform);
                button.transform.SetParent(betButtonContainer, false);

                BetButtonScript betButtonScript = button.GetComponent<BetButtonScript>();
                betButtonScript.Init(i, dic[i].ToString(), this);
            }
        }

        public void FindTable( int index ) {
            if ( !dic.TryGetValue(index, out selectedStake) ) return;

            RenderSession();
        }

        void OnSessionListUpdated( List<SessionInfo> sessionList ) {
            availableSessions = sessionList;
            RenderSession();
        }

        private void RenderSession() {
            var existingSessionKeys = new HashSet<string>(sessionEntries.Keys);

            foreach ( var session in availableSessions ) {
                if ( !session.IsOpen || !session.IsVisible ) continue;
                if ( !TryGetStake(session, out int roomStake) ) continue;
                if ( roomStake != selectedStake ) continue;

                existingSessionKeys.Remove(session.Name);

                if ( sessionEntries.TryGetValue(session.Name, out var entry) ) {
                    var script = entry.TableButtonScript;

                    if ( script.playerCount != session.PlayerCount ) {
                        script.UpdatePlayerCount(session.PlayerCount);
                    }
                }
                else {
                    var button = Instantiate(sessionButtonPrefab, transform);
                    button.transform.SetParent(sessionButtonContainer, false);
                    var script = button.GetComponent<TableButtonScript>();
                    script.Init(session.Name, roomStake, networkController, session.PlayerCount, session.MaxPlayers);
                    sessionEntries[session.Name] = new SessionEntry {
                        GameObject = button,
                        TableButtonScript = script,
                        index = sessionEntries.Count
                    };
                }

            }
        }



        private bool TryGetStake( SessionInfo session, out int stake ) {
            stake = 0;

            if ( session.Properties == null ||
                !session.Properties.TryGetValue("Stake", out var property) )
                return false;

            try {
                stake = Convert.ToInt32(property.PropertyValue);
                return true;
            }
            catch ( Exception ) {
                return false;
            }
        }


    }
}