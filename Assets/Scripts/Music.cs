using UnityEngine;

public class Music : MonoBehaviour
{
    public AudioClip track1;
    public AudioClip track2;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // Set the default track
        PlayTrack("track2");
    }

    public void PlayTrack(string trackName)
    {
        AudioClip track = null;

        switch (trackName)
        {
            case "track1":
                track = track1;
                break;
            case "track2":
                track = track2;
                break;
            default:
                Debug.LogWarning("Unknown track name: " + trackName);
                break;
        }

        if (track != null)
        {
            audioSource.clip = track;
            audioSource.Play();
        }
        else
        {
            Debug.LogError("Failed to load AudioClip: " + trackName);
        }
    }
}
