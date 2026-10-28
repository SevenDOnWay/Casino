using Assets.Script.Data.Services;
using System.Collections.Generic;

namespace Assets.Script.UI.MainMenu.Panel {
    public interface ILobbySessionBrowserView {
        void SetLoading(bool isLoading);
        void SetSelectedStakeButton(int betIndex);
        void UpdateChannelDropdown(int channelCount, int selectedChannel);
        void RenderSessions(List<LobbySessionDto> sessions);
        void ShowError(string message);
    }
}
