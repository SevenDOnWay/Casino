using System.Collections;
using UnityEngine;

namespace Assets.Script.Core {
    public class VolumeSetting {

        float musicVolume = 1f;
        float sfxVolume = 1f;

        public float MusicVolume {
            get => musicVolume;
            set {
                musicVolume = Mathf.Clamp01(value);
                AudioListener.volume = musicVolume;
            }
        }

        public float SfxVolume {
            get => sfxVolume;
            set {
                sfxVolume = Mathf.Clamp01(value);
                AudioListener.volume = sfxVolume;
            }
        }


    }
}