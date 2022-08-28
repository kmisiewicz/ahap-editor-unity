using UnityEngine;
using MoreMountains.NiceVibrations;

public class NiceVibrationsRumbleTester : MonoBehaviour
{
    [SerializeField] AudioSource _AudioSource;
    [SerializeField] AudioClip _AudioClip;
    [SerializeField] MMNVRumbleWaveFormAsset _RumbleWaveForm;

    public void TestRumble()
    {
        if (_AudioSource != null && _AudioClip != null)
            _AudioSource.PlayOneShot(_AudioClip);

        MMVibrationManager.AdvancedHapticPattern(null, null, null, -1, _RumbleWaveForm.WaveForm.Pattern,
            _RumbleWaveForm.WaveForm.LowFrequencyAmplitudes, _RumbleWaveForm.WaveForm.HighFrequencyAmplitudes,
            -1, HapticTypes.LightImpact, this);
    }
}
