using UnityEngine;
using UnityEngine.Audio;

public class WeaponAudioDSP : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;  // Optional: Assign in Inspector
    [SerializeField] AudioClip clip;
    [SerializeField] AudioMixerGroup weaponsBus;

    GameObject createdDspObject;  // Track if we created a child GameObject

    float phase;
    float gain;
    float releaseStep;
    const float DenseMakeup = 1.45f;
    float targetGain;

    const float ReleaseTime = 0.02f;
    const float WeaponGain = 0.3f;

    int shotCount;
    int lastProcessedShot;
    float lastShotTime;

    float recentFireInterval;
    const float RapidFireThreshold = 0.08f;
    const float ChainsawThreshold = 0.05f;
    float processingAmount;
    bool chainsawMode;

    float driftPhase;
    float lp;
    float lpSlow;
    bool resetLpOnNextShot;

    float[] data;
    float step;

    void Awake()
    {
        // Only create AudioSource if not assigned in Inspector
        if (audioSource == null)
        {
            // Create a dedicated child GameObject for DSP AudioSource
            createdDspObject = new GameObject("WeaponDSP_AudioSource");
            createdDspObject.transform.SetParent(transform);
            createdDspObject.transform.localPosition = Vector3.zero;

            // Add AudioSource to the child (isolated from other AudioSources)
            audioSource = createdDspObject.AddComponent<AudioSource>();
        }

        // Configure the AudioSource (whether assigned or created)
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.outputAudioMixerGroup = weaponsBus;
        audioSource.spatialBlend = 1f;  // 3D sound
        audioSource.Play();  // Start playing (DSP processes even with no clip)

        // Load audio data
        data = new float[clip.samples];
        clip.GetData(data, 0);

        int outputRate = AudioSettings.outputSampleRate;
        step = (float)clip.frequency / outputRate;
        releaseStep = 1f / (ReleaseTime * outputRate);

        phase = data.Length;
        gain = 0f;

        step *= Random.Range(0.98f, 1.02f);
        driftPhase = Random.Range(0f, 1f);

        shotCount = 0;
        lastProcessedShot = -1;
        lastShotTime = -1f;
        recentFireInterval = 0.15f;
        resetLpOnNextShot = true;
        chainsawMode = false;
    }

    public void NotifyShot()
    {
        shotCount++;

        float now = Time.time;

        if (lastShotTime > 0f)
        {
            float dt = now - lastShotTime;
            recentFireInterval = Mathf.Lerp(recentFireInterval, dt, 0.7f);
        }

        lastShotTime = now;
    }

    public void Drive()
    {
        if (shotCount > 0)
        {
            targetGain = 1f;
        }
        else
        {
            targetGain = 0f;
        }

        if (shotCount > lastProcessedShot)
        {
            chainsawMode = recentFireInterval < ChainsawThreshold;

            if (chainsawMode)
            {
                processingAmount = 1f;
                resetLpOnNextShot = false;
                phase = Random.Range(0f, 0.5f);
                gain = Mathf.Max(gain, 0.95f);
            }
            else if (recentFireInterval < RapidFireThreshold)
            {
                processingAmount = 1f;
                resetLpOnNextShot = false;
                phase = Random.Range(0f, 2f);
                gain = Mathf.Max(gain, 0.7f);
            }
            else if (recentFireInterval < RapidFireThreshold * 2f)
            {
                float t = (recentFireInterval - RapidFireThreshold) / RapidFireThreshold;
                processingAmount = 1f - (t * t * 0.6f);
                resetLpOnNextShot = false;
                phase = Random.Range(0f, 2f);
                gain = Mathf.Max(gain, 0.7f);
            }
            else if (recentFireInterval > 0.25f)
            {
                processingAmount = 0f;
                resetLpOnNextShot = true;
                phase = Random.Range(0f, 2f);
                gain = Mathf.Max(gain, 0.7f);
            }
            else
            {
                float dt = Time.time - lastShotTime;
                processingAmount = Mathf.Clamp01(1f - (dt / 0.35f));
                processingAmount = processingAmount * processingAmount * 0.8f;
                resetLpOnNextShot = (processingAmount < 0.1f);
                phase = Random.Range(0f, 2f);
                gain = Mathf.Max(gain, 0.7f);
            }

            lastProcessedShot = shotCount;
        }
    }

    void OnAudioFilterRead(float[] buffer, int channels)
    {
        if (shotCount == 0)
            return;

        const float driftRate = 0.00025f;

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
                float raw = data[i0] + (data[i1] - data[i0]) * t;

                driftPhase += driftRate;
                float localStep = step * (1f + Mathf.Sin(driftPhase) * 0.002f);

                if (resetLpOnNextShot && i == 0)
                {
                    lp = raw;
                    lpSlow = raw;
                    resetLpOnNextShot = false;
                }

                if (processingAmount > 0.05f)
                {
                    float lpCoeff = chainsawMode
                        ? 0.08f
                        : Mathf.Lerp(0.22f, 0.14f, processingAmount);

                    lp += (raw - lp) * lpCoeff;
                    lpSlow += (lp - lpSlow) * 0.04f;

                    float processed = lp;

                    float v = processed * gain * WeaponGain;
                    float k = Mathf.Lerp(0.24f, 0.10f, processingAmount);
                    float sat = v - v * v * v * k;

                    sat += raw * 0.05f * processingAmount;
                    sat += (raw - processed) * 0.09f * processingAmount;

                    if (chainsawMode)
                    {
                        sat += lpSlow * 0.3f;
                    }

                    float makeup = Mathf.Lerp(1f, DenseMakeup, processingAmount);
                    float mix = Mathf.Lerp(
                        raw * gain * WeaponGain,
                        sat * makeup,
                        processingAmount
                    );

                    outSample = mix;
                }
                else
                {
                    outSample = raw * gain * WeaponGain;
                }

                phase += localStep;
                gain += (targetGain - gain) * 0.0025f;
            }
            else
            {
                float releaseMult = chainsawMode
                    ? 0.3f
                    : Mathf.Lerp(1f, 0.6f, processingAmount);

                gain = Mathf.Max(0f, gain - releaseStep * releaseMult);
            }

            for (int c = 0; c < channels; c++)
                buffer[i + c] += outSample;
        }
    }

    void OnDestroy()
    {
        // Clean up child GameObject only if we created it
        if (createdDspObject != null)
        {
            Destroy(createdDspObject);
        }
    }
}
