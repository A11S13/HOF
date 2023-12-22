using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    GWS gws;

    public int numberOfAsteroids = 1000;
    public float appliedScale;
    public Texture2D texture;

    void Start()
    {
        // Get a reference to the GWS script component
        gws = GetComponent<GWS>();
        appliedScale = gws.scaleFactor;
        SpawnRandomAsteroids();
    }

    void Update()
    {
        // Update logic if needed
    }

    public void RandomizeSector(int x, int y)
    {

    }

    void SpawnRandomAsteroids()
{
    // Randomly instantiate asteroids
    for (int i = 0; i < numberOfAsteroids; i++)
    {
        // Generate random position within the desired world range
        float randomX = Random.Range(-gws.worldTexture.width / 2f, gws.worldTexture.width / 2f) * appliedScale;
        float randomZ = Random.Range(-gws.worldTexture.height / 2f, gws.worldTexture.height / 2f) * appliedScale;

        // Convert the random position to world coordinates
        Vector3 asteroidPosition = new Vector3(randomX, 0f, randomZ);

        // Instantiate the asteroid prefab
        GameObject asteroid = Instantiate(gws.asteroidPrefab, asteroidPosition, Quaternion.identity);

        // Convert the asteroid's position to texture coordinates and write to the texture
        Vector2 textureCoords = gws.WorldToTexture(asteroidPosition);
        gws.WritePixelToTexture(textureCoords, Color.grey);
    }
    
   gws.SaveWorldTexture();
}

}

