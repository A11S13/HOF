using UnityEngine;

public class DestroyParticlesOutOfOrthoCamera : MonoBehaviour
{
    private Camera orthoCamera;

    void Start()
    {
        // Assuming this script is attached to the particle system GameObject
        orthoCamera = Camera.main; // Change this if your orthographic camera is different
    }

    void Update()
    {
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[GetComponent<ParticleSystem>().particleCount];
        int numParticlesAlive = GetComponent<ParticleSystem>().GetParticles(particles);

        for (int i = 0; i < numParticlesAlive; i++)
        {
            // Check if the particle is outside the orthographic camera's view
            if (!IsParticleInOrthoView(particles[i].position))
            {
                // Destroy the particle
                particles[i].remainingLifetime = 0f;
            }
        }

        // Update the particle system with the modified particles
        GetComponent<ParticleSystem>().SetParticles(particles, numParticlesAlive);
    }

    bool IsParticleInOrthoView(Vector3 particlePosition)
    {
        // Convert the particle's world position to screen space
        Vector3 screenPos = orthoCamera.WorldToScreenPoint(particlePosition);

        // Check if the screen position is within the camera's viewport
        return (screenPos.x >= 0 && screenPos.x <= Screen.width &&
                screenPos.y >= 0 && screenPos.y <= Screen.height);
    }
}
