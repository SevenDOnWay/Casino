using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Game;
using Assets.Script.TienLen.Player;
using Fusion;
using NUnit.Framework;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Assets.Script.TienLen.UI {
    public class TableVisualLayoutManager : MonoBehaviour {
        [Header("Dependencies")]
        private SeatProvider seatProvider;
        private SeatManager seatManager;
        private LobbySessionController lobbySessionController;
        private ILocalPlayerService localPlayerService;

        private const int totalSeats = 4;

        private IReadOnlyList<PlayerSeat> playerSeats;

        [Inject]
        void Construct( SeatProvider seatProvider,
            SeatManager seatManager,
            LobbySessionController lobbySessionController,
            ILocalPlayerService localPlayerService ) {
            this.lobbySessionController = lobbySessionController;
            this.seatManager = seatManager;
            this.localPlayerService = localPlayerService;

            //playerSeats = seatProvider.AllSeats;
        }

        public void Init() {
            //lobbySessionController.OnPlayerJoinedEvent += HandlePlayerJoin;
            //lobbySessionController.OnPlayerLeftEvent += HandlePlayerLeft;
        }



        private async Task HandlePlayerJoin() {
            Debug.Log($"[TableLayout] HandlePlayerJoin fired.");

            NetworkDictionary<int, NetworkObject> networkDictionary = seatManager.GetNetworkOccupiedSeats();
            Dictionary<int, TienLenNetWorkPlayer> seatToPlayer = new Dictionary<int, TienLenNetWorkPlayer>();
            playerSeats ??= await seatProvider.GetPlayerSeatsAsync();

            Debug.Log($"[TableLayout] NetworkPlayers count in LobbySessionController: {networkDictionary.Count}");

            FindLocalSeatIndex(networkDictionary, seatToPlayer, out int localSeatIndex);

            if ( localSeatIndex == -1 ) {
                Debug.LogWarning($"[TableLayout] Local player {localPlayerService.GetLocalNetworkPlayer().PlayerRef} not found in occupied seats.");
                return;
            }

            BindToLocalSeat(seatToPlayer, localSeatIndex);
        }

        private void BindToLocalSeat( Dictionary<int, TienLenNetWorkPlayer> seatToPlayer, int localSeatIndex ) {
            foreach ( var (networkSeat, player) in seatToPlayer ) {
                if ( player == null ) {
                    Debug.LogWarning($"[TableLayout] Player is NULL for seat {networkSeat}");
                    continue;
                }

                int visualSlotIndex = (networkSeat - localSeatIndex + totalSeats) % totalSeats;

                var tienlenPlayer = new TienLenPlayer(player);

                Debug.Log($"[TableLayout] Network Seat {networkSeat} → Visual Slot {visualSlotIndex} (Player: {player.PlayerRef})");
                playerSeats[visualSlotIndex].BindPlayer(tienlenPlayer);
            }
        }

        private int FindLocalSeatIndex( NetworkDictionary<int, NetworkObject> networkDictionary, Dictionary<int, TienLenNetWorkPlayer> seatToPlayer, out int localSeatIndex ) {
            localSeatIndex = -1;
            foreach ( var kvp in networkDictionary ) {
                int seatIndex = kvp.Key;
                NetworkObject netObj = kvp.Value;

                var networkPlayer = netObj.GetComponent<TienLenNetWorkPlayer>();
                if ( networkPlayer == null ) continue;

                seatToPlayer[seatIndex] = networkPlayer;

                if ( networkPlayer.PlayerRef == localPlayerService.GetLocalNetworkPlayer().PlayerRef ) {
                    localSeatIndex = seatIndex;
                    Debug.Log($"[TableLayout] Found local player at seat {seatIndex}");
                }
            }

            return localSeatIndex;
        }

        private void HandlePlayerLeft( NetworkRunner runner ) {
            // Update the visual layout to reflect the player leaving
            Debug.Log("Player left. Updating table layout.");
            // Implement your logic to update the table layout here


        }


        //TODO: This method shuold handle both player join and leave events, and update the visual layout accordingly.
        public void RefreshLayout() {
            HandlePlayerJoin();
        }

    }
}