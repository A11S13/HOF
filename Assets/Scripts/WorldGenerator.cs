using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    GWS _gws;

    public int numberOfAsteroids = 1000;
    public float appliedScale;
    public Texture2D texture;

    void Start()
    {
        _gws = GetComponent<GWS>();
        appliedScale = _gws.scaleFactor;
        SpawnRandomAsteroids();
    }

    void Update() { }

    public void RandomizeSector(int x, int y) { }

    void SpawnRandomAsteroids()
    {
        for (int i = 0; i < numberOfAsteroids; i++)
        {
            // Generate random position within the desired world range
            float randomX =
                Random.Range(-_gws.worldTexture.width / 2f, _gws.worldTexture.width / 2f)
                * appliedScale;
            float randomZ =
                Random.Range(-_gws.worldTexture.height / 2f, _gws.worldTexture.height / 2f)
                * appliedScale;

            Vector3 asteroidPosition = new Vector3(randomX, 0f, randomZ);

            // Instantiate the asteroid prefab
            GameObject asteroid = Instantiate(
                _gws.asteroidPrefab,
                asteroidPosition,
                Quaternion.identity
            );

            // Convert the asteroid's position to texture coordinates and write to the texture
            Vector2 textureCoords = _gws.WorldToTexture(asteroidPosition);
            _gws.WritePixelToTexture(textureCoords, Color.grey);
        }

        _gws.SaveWorldTexture();
    }
}
