using UnityEngine;

namespace Assets.Script.TienLen.Effects {
    /// <summary>
    /// Extensible interface for celebratory win effects (particles, animations, sound, etc.).
    /// </summary>
    public interface IPlayerWinEffect {
        void PlayWinEffect( int winnerSeatIndex, Vector3 worldPosition );
        void StopWinEffect();
    }
}
