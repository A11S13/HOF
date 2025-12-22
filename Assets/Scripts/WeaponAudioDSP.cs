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
    float releaseStep;

    float dt;

    const float ReleaseTime = 0.02f;   // click-safe
    const float WeaponGain = 0.3f;    // gain staging

    bool wasFiring;

    void Awake()
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.outputAudioMixerGroup = weaponsBus;

        data = new float[clip.samples];
        clip.GetData(data, 0);

        int outputRate = AudioSettings.outputSampleRate;
        step = (float)clip.frequency / outputRate;
        releaseStep = 1f / (ReleaseTime * outputRate);

        phase = data.Length;
        gain = Random.Range(0.8f, 1f);


        step *= Random.Range(0.98f, 1.02f);
    }

    public void Drive(bool isFiring, float firePhase01)
    {
        if (isFiring && firePhase01 < firePhase)
        {
            phase = Random.Range(0f, 2f);
            gain = Mathf.Max(gain, 0.6f);
        }



        firing = isFiring;
        firePhase = firePhase01;
    }


    void OnAudioFilterRead(float[] buffer, int channels)
    {
        for (int i = 0; i < buffer.Length; i += channels)
        {
            if (gain <= 0f)
                break;

            int i0 = (int)phase;
            int i1 = i0 + 1;

            if (i1 < data.Length)
            {
                float t = phase - i0;
                float s = data[i0] + (data[i1] - data[i0]) * t;
                float v = s * gain * WeaponGain;

                for (int c = 0; c < channels; c++)
                    buffer[i + c] += v;

                phase += step;
            }
            else
            {
                gain = Mathf.Max(0f, gain - releaseStep);
            }
        }
    }
}

