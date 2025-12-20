using UnityEngine;
using UnityEngine.Audio;

public class WeaponAudioContinuous : MonoBehaviour
{
    [SerializeField] AudioClip clip;
    [SerializeField] AudioMixerGroup output;

    AudioSource a, b;
    AudioSource cur, next;

    float phase;
    float xfade;
    bool releasing;
    bool canRetrigger;
    bool isCrossfading => xfade > 0f;

    const float FadeTime = 0.0005f;

    void Awake()
    {
        a = Create();
        b = Create();
        cur = a;
        next = b;
    }

    AudioSource Create()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.outputAudioMixerGroup = output;
        s.playOnAwake = false;
        s.volume = 0f;
        s.Play();
        return s;
    }

    public void Drive(bool firing, float phase01)
    {
        if (firing)
        {
            releasing = false;

            if (phase01 < phase) // wrapped -> new bullet
            {
                next.timeSamples = 0;
                next.volume = 0f;
                xfade = FadeTime;

                var tmp = cur;
                cur = next;
                next = tmp;
            }

            // only set volume if NOT crossfading
            if (xfade <= 0f)
            {
                cur.volume = 1f;
                next.volume = 0f;
            }

            phase = phase01;
            return;
        }

        if (!releasing)
        {
            releasing = true;
            phase = phase01;
        }

        if (phase01 < phase)
        {
            cur.volume = 0f;
            next.volume = 0f;
        }

        phase = phase01;
    }




    void Update()
    {
        if (xfade <= 0f) return;

        xfade -= Time.deltaTime;
        float t = 1f - xfade / FadeTime;

        cur.volume = t;
        next.volume = 1f - t;
    }
}
