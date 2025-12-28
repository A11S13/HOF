using UnityEngine;

/// <summary>
/// Manual weapon control mode - player joystick control
/// </summary>
public class ManualWeaponControl : IWeaponControlMode
{
    private FixedJoystick turretJoystick;
    private float localRotationDeadZone;

    public ManualWeaponControl(FixedJoystick joystick, float deadZone)
    {
        turretJoystick = joystick;
        localRotationDeadZone = deadZone;
    }

    public float UpdateControl(Weapon weapon)
    {
        if (turretJoystick == null)
            return -1f;

        HandleTurretRotation(weapon);
        return HandleFire();
    }

    public Vector3 GetAimPosition(Weapon weapon)
    {
        return weapon.transform.position + weapon.transform.forward * 100f;
    }

    void HandleTurretRotation(Weapon weapon)
    {
        Vector3 turretDirection = new Vector3(
            turretJoystick.Direction.x,
            0,
            turretJoystick.Direction.y
        );

        if (turretDirection.magnitude > localRotationDeadZone)
        {
            float angle = Vector3.SignedAngle(Vector3.forward, turretDirection, Vector3.up);
            Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);
            weapon.transform.rotation = Quaternion.Slerp(
                weapon.transform.rotation,
                targetRotation,
                Time.deltaTime * weapon.rotationSpeed
            );
        }
    }

    float HandleFire()
    {
        Vector3 fireDir = new Vector3(
            turretJoystick.Direction.x,
            0,
            turretJoystick.Direction.y
        );

        float mag = fireDir.magnitude;
        if (mag <= turretJoystick.DeadZone)
            return -1f;

        return Mathf.Clamp01(mag);
    }
}
