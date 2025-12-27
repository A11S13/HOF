using UnityEngine;

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
        }
    }

    protected override Vector3 CalculateAimPosition()
    {
        if (target == null)
            return transform.position + transform.forward * 100f;

        var to = target.transform.position - weaponSpawnPoint.position;

        if (to.magnitude / projectileSpeed < MinPredictTime)
            return target.transform.position;

        return Predict(weaponSpawnPoint.position, target.transform.position, targetVel, projectileSpeed);
    }

    Vector3 Predict(Vector3 origin, Vector3 pos, Vector3 vel, float speed)
    {
        vel -= shipRb.linearVelocity;

        var to = pos - origin;
        var t = Mathf.Min(to.magnitude / speed, 2f);
        return pos + vel * t;
    }

    protected override void Fire(float aimQuality)
    {
        EventBus.Instance.Publish(new PlayerShotEvent());

        float spread = spreadAngle;
        float yaw = Random.Range(-spread, spread);
        Quaternion rot = weaponSpawnPoint.rotation * Quaternion.Euler(0, yaw, 0);

        var p = Instantiate(projectilePrefab, weaponSpawnPoint.position, rot);
        p.GetComponent<Projectile>().Initialize(shipRb.linearVelocity);
        Destroy(p, 3f);
    }
}
