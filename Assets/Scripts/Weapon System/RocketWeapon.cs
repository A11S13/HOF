using UnityEngine;

public class RocketWeapon : Weapon
{
    [Header("Rocket Settings")]
    public GameObject rocketPrefab;
    public float lockOnTime = 1f;

    protected override void InitializeWeapon()
    {
    }

    protected override Vector3 CalculateAimPosition()
    {
        return target != null ? target.transform.position : transform.position + transform.forward * 100f;
    }

    protected override void Fire(float aimQuality)
    {
        EventBus.Instance.Publish(new PlayerShotEvent());
        Debug.Log("Rocket fire - TODO: Implement homing missile spawn");
    }

    protected override float ModifyTargetScore(GameObject target, float baseScore)
    {
        var targetable = target.GetComponent<Targetable>();
        if (targetable != null && targetable.type == TargetType.Ship)
        {
            return baseScore * 0.6f;
        }
        return baseScore * 1.5f;
    }
}
