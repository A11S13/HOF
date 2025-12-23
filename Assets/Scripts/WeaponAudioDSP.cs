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
    const float DenseMakeup = 1.45f;



    const float ReleaseTime = 0.02f;   // click-safe
    const float WeaponGain = 0.3f;     // gain staging

    // ---- isolation detection ----
    float lastShotTime;
    [SerializeField] float isolationThreshold = 0.08f; // seconds; > = isolated
    float processingAmount; // 0 => dry, 1 => full processed

    // drift / lp state
    float driftPhase;
    float lp;          // one-pole lowpass state

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
        gain = 0f;

        // small static pitch offset so single weapon remains natural
        step *= Random.Range(0.98f, 1.02f);

        // init per-voice drift base
        driftPhase = Random.Range(0f, 1f);
    }

    // called on main thread
    public void Drive(bool isFiring, float firePhase01)
    {
        // bullet edge detection (wrap)
        if (isFiring && firePhase01 < firePhase)
        {
            // compute isolation: time since last shot
            float now = Time.time;
            float dt = now - lastShotTime;

            // if last shot was long ago, keep it dry (0), else full processing (1)
            processingAmount = dt > isolationThreshold ? 0f : 1f;

            // kick the clip
            phase = Random.Range(0f, 2f);
            // ensure audibility baseline
            gain = Mathf.Max(gain, 0.7f);

            lastShotTime = now;
        }

        firing = isFiring;
        firePhase = firePhase01;
    }

    // audio thread: pure math only
    void OnAudioFilterRead(float[] buffer, int channels)
    {
        // small runtime drift rate (pure math, not Unity APIs)
        const float driftRate = 0.00025f; // tune: how fast pitch meanders

        for (int i = 0; i < buffer.Length; i += channels)
        {
            if (gain <= 0f)
                break;

            int i0 = (int)phase;
            int i1 = i0 + 1;

            float outSample = 0f;

            if (i1 < data.Length)
            {
                float t = phase - i0;
                // raw sample (dry): linear interpolation, no LP, no noise, no clip
                float raw = data[i0] + (data[i1] - data[i0]) * t;

                // processed path: add decorrelation, lp, noise, soft clip
                // drift modifies local read step and avoids perfect spectral alignment
                driftPhase += driftRate;
                float localStep = step * (1f + Mathf.Sin(driftPhase) * 0.002f);

                // one-pole lowpass to soften HF (per-weapon)
                // compute processed sample before LP
                float procPre = raw;

                // apply lowpass smoothing (per-voice state)
                float lpCoeff = Mathf.Lerp(0.16f, 0.26f, processingAmount);

                lp += (procPre - lp) * lpCoeff;


                float processed = lp;

                // soft cubic saturation
                float v = processed * gain * WeaponGain;
                float k = Mathf.Lerp(0.24f, 0.10f, processingAmount);
                float sat = v - v * v * v * k;

                sat += raw * 0.05f * processingAmount;
                sat += (raw - processed) * 0.05f * processingAmount;




                // density makeup
                float makeup = Mathf.Lerp(1f, DenseMakeup, processingAmount);
                float mix = Mathf.Lerp(
                    raw * gain * WeaponGain,
                    sat * makeup,
                    processingAmount
                );


                outSample = mix;

                // advance phase using drift-influenced step
                phase += localStep;
            }
            else
            {
                // tail / release
                gain = Mathf.Max(0f, gain - releaseStep);
            }

            // write to all channels
            for (int c = 0; c < channels; c++)
                buffer[i + c] += outSample;
        }
    }
}
