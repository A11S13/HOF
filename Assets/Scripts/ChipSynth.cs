using System;
using UnityEngine;

public class ChipSynth : MonoBehaviour
{
    public bool PlayProjected1;
    public bool PlayProjected2;

    public bool PureSin;
    public bool Phased;

    [Range(1, 20000)] //Creates a slider in the inspector
    public float frequency1;
    public float phase1 = 0;

    [Range(1, 20000)] //Creates a slider in the inspector
    public float frequency2;
    public float phase2 = 0;

    [Range(1, 44100)]
    public float sampleRate = 44100;
    public float waveLengthInSeconds = 2.0f;

    AudioSource audioSource;
    public int timeIndex = 0;
    public float currentPhase1;
    public float currentPhase2;

    void Start()
    {
        PlayProjected1 = false;
        PlayProjected2 = false;
        PureSin = false;
        audioSource = gameObject.GetComponent<AudioSource>();
        audioSource.spatialBlend = 0; //force 2D sound
    }

    private float PlayProjected(int frequency)
    {
        float floatFrequency = (float)frequency + 100;

        return floatFrequency;
    }

    private void UpdateCurrentState()
    {
        currentPhase1 = phase1;
        currentPhase2 = phase2;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (PureSin)
        {
            for (int i = 0; i < data.Length; i += channels)
            {
                phase1 += 2 * Mathf.PI * frequency1 / sampleRate;
                data[i] = Mathf.Sin(phase1);

                phase2 += 2 * Mathf.PI * frequency2 / sampleRate;
                data[i + 1] = Mathf.Sin(phase2);

                if (phase1 >= 2 * Mathf.PI)
                {
                    phase1 -= 2 * Mathf.PI * sampleRate;
                }

                if (phase2 >= 2 * Mathf.PI)
                {
                    phase2 -= 2 * Mathf.PI * sampleRate;
                }

                phase1 = phase1 % (2 * Mathf.PI);
                phase2 = phase2 % (2 * Mathf.PI);
                UpdateCurrentState();
            }
        }
        else
        {
            if (waveLengthInSeconds <= 0.002f)
            {
                waveLengthInSeconds = 0.002f;
            }
            for (int i = 0; i < data.Length; i += channels)
            {
                phase1 += 2 * Mathf.PI * frequency1 * timeIndex / sampleRate;
                data[i] = Mathf.Sin(phase1);

                phase2 += 2 * Mathf.PI * frequency2 * timeIndex / sampleRate;
                data[i + 1] = Mathf.Sin(phase2);

                if (Phased)
                {
                    if (phase1 >= 2 * Mathf.PI)
                    {
                        phase1 -= 2 * Mathf.PI * sampleRate;
                    }

                    if (phase2 >= 2 * Mathf.PI)
                    {
                        phase2 -= 2 * Mathf.PI * sampleRate;
                    }
                }
                phase1 = phase1 % (2 * Mathf.PI);
                phase2 = phase2 % (2 * Mathf.PI);
                UpdateCurrentState();
            }

            timeIndex++;

            //if timeIndex gets too big, reset it to 0
            if (timeIndex >= (sampleRate * waveLengthInSeconds))
            {
                timeIndex = 0;
            }
        }
    }

    //Creates a sinewave
    public float CreateSine(int timeIndex, float frequency, float sampleRate)
    {
        return Mathf.Sin(2 * Mathf.PI * timeIndex * frequency / sampleRate);
    }
}
