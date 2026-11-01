using Assets.Script.NetWorkScript;
using Assets.Script.TienLen.Player;
using Assets.Script.TienLen.Rule;

namespace Assets.Script.TienLen.Game.Services {
    public interface IActionHandler {
        bool IsFirstPlayOfGame { get; set; }

        bool TryValidateAndExecutePlay(
            TienLenPlayer player,
            NetworkCard[] requestedCards,
            out CardCombination combination,
            out string failureReason
        );

        bool TryExecutePass(
            TienLenPlayer player,
            out bool shouldClearTableCenter,
            out string failureReason
        );

        bool CheckPlayerWin( TienLenPlayer player );
    }
}
