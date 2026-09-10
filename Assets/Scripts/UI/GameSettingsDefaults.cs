using UnityEngine;
namespace RogueDrive.UI
{
    [CreateAssetMenu(menuName = "RogueDrive/Game Settings Defaults")]
    public sealed class GameSettingsDefaults : ScriptableObject
    {
        [SerializeField, Range(0,1)] private float masterVolume = .85f;
        [SerializeField, Range(0,1)] private float musicVolume = .75f;
        [SerializeField, Range(0,1)] private float effectsVolume = .9f;
        [SerializeField, Range(.5f,2)] private float steeringSensitivity = 1f;
        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float EffectsVolume => effectsVolume;
        public float SteeringSensitivity => steeringSensitivity;
    }
}
