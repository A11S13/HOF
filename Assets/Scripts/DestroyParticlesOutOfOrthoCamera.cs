using UnityEngine;

public class DestroyParticlesOutOfRect : MonoBehaviour
{
    private Transform player; 
    [SerializeField] private Vector2 areaSize = new Vector2(1000, 1000); // Width & height of active area

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particlesBuffer;
    private Rect activeArea;

    void Awake()
    {
        player = GetComponentInParent<Transform>();
        ps = GetComponent<ParticleSystem>();
        particlesBuffer = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    void Update()
    {
        // Update active area position based on player
        activeArea = new Rect(
            player.position.x - areaSize.x / 2,
            player.position.z - areaSize.y / 2,
            areaSize.x,
            areaSize.y
        );

        // Get particles and check if they are within the active area
        int numParticles = ps.GetParticles(particlesBuffer);
        for (int i = 0; i < numParticles; i++)
        {
            if (!IsParticleInActiveArea(particlesBuffer[i].position))
            {
                particlesBuffer[i].remainingLifetime = 0f;
            }
        }

        // Apply the updated particles back to the system
        ps.SetParticles(particlesBuffer, numParticles);
    }

    bool IsParticleInActiveArea(Vector3 position)
    {
        return position.x >= activeArea.xMin && position.x <= activeArea.xMax &&
               position.z >= activeArea.yMin && position.z <= activeArea.yMax; // Using Z for Y in world space
    }
}
