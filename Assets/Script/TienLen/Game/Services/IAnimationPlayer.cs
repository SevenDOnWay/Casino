using Assets.Script.NetWorkScript;
using Fusion;

namespace Assets.Script.TienLen.Game.Services {
    public interface IAnimationPlayer {
        void PlayDealAnimation();
        void PlayCardPlayAnimation( PlayerRef playerRef, int seatIndex, NetworkCard[] cards );
        void ClearTableCenter();
    }
}
