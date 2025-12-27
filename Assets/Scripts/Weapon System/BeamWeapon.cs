using UnityEngine;

public class BeamWeapon : Weapon
{
    [Header("Beam Settings")]
    public float beamRange = 50f;
    public float beamDamagePerSecond = 20f;
    public float beamWidth = 0.2f;
    public Color beamColor = Color.cyan;

    LineRenderer beamRenderer;

    protected override void InitializeWeapon()
    {
        beamRenderer = gameObject.AddComponent<LineRenderer>();
        beamRenderer.positionCount = 2;
        beamRenderer.startWidth = beamWidth;
        beamRenderer.endWidth = beamWidth * 0.5f;
        beamRenderer.material = new Material(Shader.Find("Sprites/Default"));
        beamRenderer.startColor = beamColor;
        beamRenderer.endColor = beamColor * 0.5f;
        beamRenderer.enabled = false;
    }

    protected override Vector3 CalculateAimPosition()
    {
        return target != null ? target.transform.position : transform.position + transform.forward * beamRange;
    }

    protected override void Fire(float aimQuality)
    {
        EventBus.Instance.Publish(new PlayerShotEvent());
        Debug.Log("Beam fire - TODO: Implement continuous damage and visuals");
    }

    protected override float ModifyTargetScore(GameObject target, float baseScore)
    {
        return baseScore * 0.8f;
    }
}
