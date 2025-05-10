using UnityEngine;
using static UnityEngine.ParticleSystem;

public class Essence : MonoBehaviour
{
    [SerializeField]
    private Vector2Int _ageRange = new Vector2Int(1, 100); // Customizable age range
    [SerializeField]
    private Vector2 _biotaRange = new Vector2(0f, 5f); // Example Biota range
    [SerializeField]
    private Vector2 _inertaRange = new Vector2(20f, 80f); // Example Inerta range
    [SerializeField]
    private Vector2 _gasRange = new Vector2(5f, 20f);    // Example Gas range

    [SerializeField]
    public int Age;
    public float Biota;
    public float Inerta;
    public float Gas;

    private int _hexSeed; // Store the integer representation of the hex seed

    void Start()
    {
        GenerateAzoth();
    }

    private void GenerateAzoth()
    {
        GenerateAgeFromHexSeed();
        GenerateBiotaFromAge();
        GenerateInertaFromAge();
        GenerateGasFromAge();
        SetRockFx();
        SetNebulaFx();
        SetColonyFx();
    }

    private void SetRockFx()
    {
        Rocks rocks = transform.Find("Inerta")?.GetComponent<Rocks>();

        if (rocks != null)
        {
            rocks.rockCount = Mathf.RoundToInt(Mathf.Lerp(1, 15, Age / 300f)); // Adjust the range as needed
            rocks.sizeRange = new Vector2(Mathf.Lerp(1f, 5f, Age / 100f), Mathf.Lerp(1f, 5f, Age / 100f)); // Adjust the range as needed
            rocks.GenerateRocks();
        }
    }
    private void SetNebulaFx()
    {
        // Find the child object named "Nebula"
        Transform nebulaTransform = transform.Find("Gas");

        if (nebulaTransform != null)
        {
            // Get the ParticleSystem component from the "Nebula" child
            ParticleSystem ps = nebulaTransform.GetComponent<ParticleSystem>();

            if (ps != null)
            {
                var mainModule = ps.main;

                // Stop the particle system and clear any existing particles
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                // Now that it's fully stopped, set the random seed
                ps.randomSeed = (uint)_hexSeed;

                // Apply the age-based duration
                mainModule.duration *= Age;

                // Calculate scale based on Age (clamped to 1-5 range)
                float scale = Mathf.Clamp(Age / 30f, 1f, 4f); //  Adjust divisor (20f) for desired scaling
                nebulaTransform.localScale = new Vector3(scale, scale, scale);

                // Start the particle system again with the new seed and duration
                ps.Play();
            }
        }
    }

    private void SetColonyFx()
    {
        // Find the child object named "Nebula"
        Transform colonyTransform = transform.Find("Biota");

        if (colonyTransform != null)
        {
            // Get the ParticleSystem component from the "Nebula" child
            ParticleSystem ps = colonyTransform.GetComponent<ParticleSystem>();

            if (ps != null)
            {
                var mainModule = ps.main;
                var emissionModule = ps.emission;

                // Stop the particle system and clear any existing particles
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                // Now that it's fully stopped, set the random seed
                ps.randomSeed = (uint)_hexSeed;

                // Apply the age-based duration
                mainModule.duration *= Age;
                mainModule.maxParticles *= Age / 10;
                float rateFactor = Age / 10f;
                emissionModule.rateOverTime = new MinMaxCurve(emissionModule.rateOverTime.constant * rateFactor,
                                                             emissionModule.rateOverTime.curveMultiplier);

                // Calculate scale based on Age (clamped to 1-5 range)
                float scale = Mathf.Clamp(Age / 30f, 1f, 6f); //  Adjust divisor (20f) for desired scaling
                colonyTransform.localScale = new Vector3(scale, scale, scale);

                // Start the particle system again with the new seed and duration
                ps.Play();
            }
        }
    }

    private void GenerateBiotaFromAge()
    {
        Biota = Mathf.Lerp(_biotaRange.x, _biotaRange.y, Age / 100f);
    }

    private void GenerateInertaFromAge()
    {
        Inerta = Mathf.Lerp(_inertaRange.x, _inertaRange.y, Age / 100f);
    }

    private void GenerateGasFromAge()
    {
        Gas = Mathf.Lerp(_gasRange.x, _gasRange.y, Age / 100f);
    }

    private bool GenerateAgeFromHexSeed()
    {
        string parentName = gameObject.name;
        string[] nameParts = parentName.Split('_');

        if (nameParts.Length == 2)
        {
            string hexSeedString = nameParts[1];

            if (int.TryParse(hexSeedString, System.Globalization.NumberStyles.HexNumber, null, out _hexSeed))
            {
                Random.InitState(_hexSeed); // Seed the random number generator with the hex value
                Age = Random.Range(_ageRange.x, _ageRange.y + 1); // +1 because Random.Range is exclusive for max
                return true;
            }
            else
            {
                Debug.LogError($"Could not parse hex value '{hexSeedString}' from parent name '{parentName}'. Setting Age to 0 and randomSeed to 0.");
                Age = 0;
                _hexSeed = 0;
                return false;
            }
        }
        else
        {
            Age = 0;
            _hexSeed = 0;
            return false;
        }
    }
}