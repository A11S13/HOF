using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Sonar : MonoBehaviour
{
    ShipCore _sC;
    public Collider[] enemyColliders;
    public List<Collider> trackedColliders;
    public int maxTrackedCap;
    public float sonarCurrentRadius = 0f;
    public float sonarPrevMaxRadius;
    public Material sonarMaterial;

    private LineRenderer sonarVisual;
    private int maxTracked;

    void FixedUpdate() { }

    void Awake()
    {
        _sC = GameObject.Find("MOTHERSHIP").GetComponent<ShipCore>();
        sonarPrevMaxRadius = _sC.sonarDetectionRadius;
        trackedColliders = new List<Collider>();
        CreateSonarVisual();
    }

    void Start()
    {
        SonarCalculation();
    }

    private void CreateSonarVisual()
    {
        sonarVisual = gameObject.AddComponent<LineRenderer>();
        sonarVisual.material = sonarMaterial;
        sonarVisual.positionCount = 100; // Adjust based on the circle's smoothness

        // Set the initial circle points using polar coordinates
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < 100; i++)
        {
            float angle = i * 2 * Mathf.PI / 100;
            float x = Mathf.Cos(angle) * sonarCurrentRadius;
            float z = Mathf.Sin(angle) * sonarCurrentRadius;
            float y = 0;

            points.Add(new Vector3(x, y, z));
        }

        // Set positions
        sonarVisual.SetPositions(points.ToArray());

        // Set the width of the line
        sonarVisual.startWidth = 0.2f; // Adjust the width as needed
        sonarVisual.endWidth = 0.2f; // Adjust the width as needed
    }

    private void SonarCalculation()
    {
        sonarCurrentRadius += 100f * Time.fixedDeltaTime;

        if (sonarCurrentRadius >= _sC.sonarDetectionRadius)
        {
            sonarCurrentRadius = 0f;
        }

        // Update the circle points based on sonar radius
        for (int i = 0; i < 100; i++)
        {
            float angle = i * 2 * Mathf.PI / 98;
            float x = Mathf.Cos(angle) * sonarCurrentRadius + transform.position.x;
            float z = Mathf.Sin(angle) * sonarCurrentRadius + transform.position.z;
            sonarVisual.SetPosition(i, new Vector3(x, 0, z));
        }
    }

    public void SonarPhysics()
    {
        maxTracked = maxTrackedCap + _sC.Weapons.Length;

        enemyColliders = Physics.OverlapSphere(
            transform.position,
            sonarCurrentRadius,
            LayerMask.GetMask("Enemy")
        );

        if (trackedColliders.Count > maxTracked)
        {
            trackedColliders.AddRange(enemyColliders);
            //sonarCurrentRadius = 0;
        }
        else
        {
            trackedColliders.AddRange(enemyColliders);
        }
    }

    public void MakeupTracked()
    {
        // Remove null elements
        trackedColliders.RemoveAll(item => item == null);

        // Remove duplicate elements
        trackedColliders = trackedColliders.Distinct().ToList();

        // Sort the list by distance to _sonar
        trackedColliders.Sort(
            (a, b) =>
                Vector3
                    .Distance(a.transform.position, transform.position)
                    .CompareTo(Vector3.Distance(b.transform.position, transform.position))
        );

        foreach (Collider collider in trackedColliders)
        {
            collider.transform.Find("IndicatorRed").gameObject.SetActive(false);
            collider.transform.Find("IndicatorBlue").gameObject.SetActive(true);
        }
    }

    public void DriveSonar()
    {
        trackedColliders = new List<Collider>();
        SonarPhysics();
        SonarCalculation();
        MakeupTracked();
    }
}
