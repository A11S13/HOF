using UnityEngine;

/// <summary>
/// Projectile-based weapon with predictive aiming
/// </summary>
public class ProjectileWeapon : Weapon
{
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public float spreadAngle = 3f;

    float projectileSpeed;
    const float MinPredictTime = 0.05f;

    protected override void InitializeWeapon()
    {
        if (projectilePrefab != null)
        {
            var proj = projectilePrefab.GetComponent<Projectile>();
            if (proj != null)
                projectileSpeed = proj.speed;
            else
                Debug.LogWarning($"ProjectileWeapon on {gameObject.name}: Projectile prefab has no Projectile component!");
        }
        else
        {
            Debug.LogError($"ProjectileWeapon on {gameObject.name}: No projectile prefab assigned!");
        }
    }

    public override Vector3 CalculateAimPosition(GameObject target, Vector3 targetVelocity)
    {
        if (target == null)
            return transform.position + transform.forward * 100f;

        var to = target.transform.position - weaponSpawnPoint.position;

        if (to.magnitude / projectileSpeed < MinPredictTime)
            return target.transform.position;

        return Predict(weaponSpawnPoint.position, target.transform.position, targetVelocity, projectileSpeed);
    }

    Vector3 Predict(Vector3 origin, Vector3 pos, Vector3 vel, float speed)
    {
        // Safety check
        if (shipRb == null)
        {
            Debug.LogWarning($"ProjectileWeapon.Predict: shipRb is null on {gameObject.name}");
            return pos;
        }

        vel -= shipRb.linearVelocity;

        var to = pos - origin;
        var t = Mathf.Min(to.magnitude / speed, 2f);
        return pos + vel * t;
    }

    protected override void Fire(float aimQuality)
    {
        // Safety checks
        if (projectilePrefab == null)
        {
            Debug.LogError($"ProjectileWeapon.Fire: No projectile prefab on {gameObject.name}");
            return;
        }

        if (weaponSpawnPoint == null)
        {
            Debug.LogError($"ProjectileWeapon.Fire: No weapon spawn point on {gameObject.name}");
            return;
        }

        // Publish event if EventBus exists
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish(new PlayerShotEvent());
        }

        float spread = spreadAngle;
        float yaw = Random.Range(-spread, spread);
        Quaternion rot = weaponSpawnPoint.rotation * Quaternion.Euler(0, yaw, 0);

        var p = Instantiate(projectilePrefab, weaponSpawnPoint.position, rot);

        // MODIFIED: Initialize projectile with ship velocity AND owner layer
        var projectile = p.GetComponent<Projectile>();
        if (projectile != null && shipRb != null)
        {
            // Get the ship's layer (use root to get the main ship object)
            int ownerLayer = transform.root.gameObject.layer;
            projectile.Initialize(shipRb.linearVelocity, ownerLayer);
        }
        else if (projectile != null)
        {
            int ownerLayer = transform.root.gameObject.layer;
            projectile.Initialize(Vector3.zero, ownerLayer);
        }

        Destroy(p, 3f);
    }
}
