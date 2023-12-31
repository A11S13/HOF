using System;
using System.Collections;
using UnityEngine;

public class RandomSounds : MonoBehaviour
{
    // Assuming you have an array of AudioClip for your sounds
    public AudioClip[] sounds = new AudioClip[10]; // Assign your sounds in the Unity Editor

    // Other variables and methods in your RandomSounds script

    public void PlaySound(char[] result)
    {
        // Map the result to an index for sound selection
        int soundIndex = MapResultToSoundIndex(result);

        // Assuming you have an AudioSource component attached to the same GameObject
        AudioSource audioSource = GetComponent<AudioSource>(); // Replace this with your actual AudioSource reference

        // Check if the soundIndex is valid
        if (soundIndex >= 0 && soundIndex < sounds.Length)
        {
            // Play the selected sound
            audioSource.PlayOneShot(sounds[soundIndex]);
        }
        else
        {
            // Sound index is not valid, you can choose to play a default sound or skip playing a sound
            // For example, you might play a default sound or do nothing
        }
    }

    int MapResultToSoundIndex(char[] result)
    {
        // Convert the char[] to a string
        string resultString = new string(result);

        // Implement a case statement or any other mapping logic
        switch (resultString)
        {
            case "00000":
                return 0; // Play element 0
            case "11111":
                return 1; // Play element 1
            case "22222":
                return 2; // Play element 2
            case "33333":
                return 3; // Play element 3
            case "44444":
                return 4; // Play element 4
            case "55555":
                return 5; // Play element 5
            case "66666":
                return 6; // Play element 6
            case "77777":
                return 7; // Play element 7
            case "88888":
                return 8; // Play element 8
            case "99999":
                return 9; // Play element 9

            default:
                // If result does not match any specific case, return a special value (-1)
                return -1;
        }
    }
}
