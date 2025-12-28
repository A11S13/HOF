using UnityEngine;

/// <summary>
/// Rocket/Missile weapon with homing capabilities
/// </summary>
public class RocketWeapon : Weapon
{
    [Header("Rocket Settings")]
    public GameObject rocketPrefab;
    public float spreadAngle = 5f;

    public override Vector3 CalculateAimPosition(GameObject target, Vector3 targetVelocity)
    {
        // Rockets home in, so just aim at current position
        return target != null ? target.transform.position : transform.position + transform.forward * 100f;
    }

    protected override void Fire(float aimQuality)
    {
        if (rocketPrefab == null)
        {
            Debug.LogError($"RocketWeapon.Fire: No rocket prefab on {gameObject.name}");
            return;
        }

        if (weaponSpawnPoint == null)
        {
            Debug.LogError($"RocketWeapon.Fire: No weapon spawn point on {gameObject.name}");
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

        var rocket = Instantiate(rocketPrefab, weaponSpawnPoint.position, rot);

        // TODO: Implement homing logic later
        // For now, rockets just fly straight like projectiles

        Destroy(rocket, 5f);
    }

    protected override float ModifyTargetScore(GameObject target, float baseScore)
    {
        // Rockets are better at longer ranges
        return baseScore * 1.2f;
    }
}
