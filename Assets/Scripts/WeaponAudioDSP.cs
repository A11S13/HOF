using UnityEngine;
using UnityEngine.Audio;

public class WeaponAudioDSP : MonoBehaviour
{
    [SerializeField] AudioClip clip;
    [SerializeField] AudioMixerGroup weaponsBus;

    float phase;
    float firePhase;
    bool firing;

    float[] data;
    float step;
    float gain;

    float dt;

    const float ReleaseTime = 0.02f;   // click-safe
    const float WeaponGain = 0.3f;    // gain staging

    void Awake()
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = true;
        src.loop = true;
        src.outputAudioMixerGroup = weaponsBus;

        data = new float[clip.samples];
        clip.GetData(data, 0);

        int outputRate = AudioSettings.outputSampleRate;
        step = (float)clip.frequency / outputRate;
        dt = 1f / outputRate;

        // decorrelate multiple weapons
        phase = Random.Range(0f, data.Length * 0.2f);

        // tiny pitch variance (optional but recommended)
        step *= Random.Range(0.98f, 1.02f);
    }

    public void Drive(bool isFiring, float weaponPhase01)
    {
        // retrigger exactly on bullet wrap
        if (isFiring && weaponPhase01 < firePhase)
            phase = 0f;

        firing = isFiring;
        firePhase = weaponPhase01;
    }

    void OnAudioFilterRead(float[] buffer, int channels)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = 0f;

        for (int i = 0; i < buffer.Length; i += channels)
        {
            bool finishingShot = !firing && phase < data.Length;

            gain = (firing || finishingShot)
                ? 1f
                : Mathf.Max(0f, gain - dt / ReleaseTime);

            if (gain <= 0f)
                return;

            int s = (int)phase;
            float v = s < data.Length ? data[s] * gain * WeaponGain : 0f;

            for (int c = 0; c < channels; c++)
                buffer[i + c] += v;

            if (phase < data.Length)
                phase += step;
        }
    }
}
