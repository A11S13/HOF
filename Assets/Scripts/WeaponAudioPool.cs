using UnityEngine;
using UnityEngine.Audio;

public class WeaponAudioPool : MonoBehaviour
{
    [SerializeField] AudioClip shotClip;
    [SerializeField] AudioMixerGroup outputGroup; 
    [SerializeField] int poolSize = 8;

    AudioSource[] pool;
    int index;

    void Awake()
    {
        pool = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            pool[i] = gameObject.AddComponent<AudioSource>();
            pool[i].clip = shotClip;
            pool[i].outputAudioMixerGroup = outputGroup;
            pool[i].playOnAwake = false;
        }
    }

    public void PlayShot()
    {
        var src = pool[index];
        src.pitch = Random.Range(0.95f, 1.05f);
        src.volume = Random.Range(0.6f, 0.8f);
        src.Play();
        index = (index + 1) % pool.Length;
    }
}