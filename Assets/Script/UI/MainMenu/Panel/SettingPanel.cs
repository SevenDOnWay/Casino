using Assets.Script.Core;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Assets.Script.UI.MainMenu.Panel {
    public class SettingPanel : MonoBehaviour {
        [Header("Dependencies")]

        private VolumeSetting volumeSetting;

        [Header("Serialized Field")]
        [SerializeField] Button settingBtn;
        [SerializeField] Slider musicVolumeSlider;
        [SerializeField] Slider sfxVolumeSlider;


        [Inject]
        void Construct( VolumeSetting volumeSetting ) {
            this.volumeSetting = volumeSetting;
        }

        private void OnEnable() {
            musicVolumeSlider.value = volumeSetting.MusicVolume;
            sfxVolumeSlider.value = volumeSetting.SfxVolume;

            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

        }

        void OnDisable() {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        }


        void OnMusicVolumeChanged( float value ) {
            volumeSetting.MusicVolume = value;
        }

        void OnSfxVolumeChanged( float value ) {
            volumeSetting.SfxVolume = value;
        }


    }
}
