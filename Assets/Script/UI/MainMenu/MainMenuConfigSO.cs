using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.UI {
    [CreateAssetMenu(fileName = "MainMenuConfig", menuName = "MinimalArena/MainMenuConfig")]
    public class MainMenuConfigSO : ScriptableObject {
        [Header("General Settings")]
        [SerializeField] private string serverRegion = "NORTH-AMERICA";
        [SerializeField] private string versionString = "v2.1.0";
        [SerializeField] private string gameplaySceneName = "GameScene";
        [SerializeField] private string signInSceneName = "SignInScene";

        [Header("Matchmaking & Lobbies")]
        [SerializeField] private string defaultRoomName = "ArenaTable";
        [SerializeField] private int defaultPlayerCount = 4;
        [SerializeField] private long defaultStake = 1000;
        [SerializeField] private string activePlayersFormatted = "4.2k active";

        [Header("Network Latency Emulation / Polling")]
        [SerializeField] private bool simulatePing = true;
        [SerializeField] private int minSimulatedPing = 18;
        [SerializeField] private int maxSimulatedPing = 32;

        [Header("Available Regions")]
        [SerializeField] private List<string> serverRegions = new List<string> {
            "NORTH-AMERICA",
            "EUROPE",
            "ASIA-PACIFIC"
        };

        public string ServerRegion => serverRegion;
        public string VersionString => versionString;
        public string GameplaySceneName => gameplaySceneName;
        public string SignInSceneName => signInSceneName;
        public string DefaultRoomName => defaultRoomName;
        public int DefaultPlayerCount => defaultPlayerCount;
        public long DefaultStake => defaultStake;
        public string ActivePlayersFormatted => activePlayersFormatted;
        public bool SimulatePing => simulatePing;
        public int MinSimulatedPing => minSimulatedPing;
        public int MaxSimulatedPing => maxSimulatedPing;
        public IReadOnlyList<string> ServerRegions => serverRegions;
    }
}
