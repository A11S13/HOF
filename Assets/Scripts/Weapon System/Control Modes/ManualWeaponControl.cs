using UnityEngine;

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
        if (turretJoystick == null) return -1f;

        HandleTurretRotation(weapon);
        return HandleFire();
    }

    public Vector3 GetAimPosition(Weapon weapon)
    {
        return weapon.transform.position + weapon.transform.forward * 100f;
    }

    void HandleTurretRotation(Weapon weapon)
    {
        // FIX 1: Respect Locked Rotation
        if (weapon.lockRotation)
        {
            // Optional: Reset to forward if you want it strictly fixed
            weapon.transform.localRotation = Quaternion.RotateTowards(
                weapon.transform.localRotation,
                Quaternion.identity,
                Time.deltaTime * weapon.rotationSpeed
            );
            return;
        }

        Vector3 joystickDir = new Vector3(turretJoystick.Direction.x, 0, turretJoystick.Direction.y);

        if (joystickDir.magnitude > localRotationDeadZone)
        {
            // FIX 2: Respect Fire Arc (Clamping)
            if (weapon.fireArc < 360f)
            {
                // Convert joystick direction (World) to Local space of the SHIP (parent)
                // This assumes the joystick matches the ship's orientation context
                Vector3 localDir = weapon.transform.parent.InverseTransformDirection(joystickDir);

                float angle = Vector3.SignedAngle(Vector3.forward, localDir, Vector3.up);
                float halfArc = weapon.fireArc * 0.5f;

                if (Mathf.Abs(angle) > halfArc)
                {
                    float clampedAngle = Mathf.Sign(angle) * halfArc;
                    // Reconstruct clamped direction
                    Quaternion clampRot = Quaternion.Euler(0, clampedAngle, 0);
                    Vector3 clampedLocal = clampRot * Vector3.forward;
                    joystickDir = weapon.transform.parent.TransformDirection(clampedLocal);
                }
            }

            // Apply Rotation
            Quaternion targetRotation = Quaternion.LookRotation(joystickDir);
            weapon.transform.rotation = Quaternion.Slerp(
                weapon.transform.rotation,
                targetRotation,
                Time.deltaTime * weapon.rotationSpeed
            );
        }
    }

    float HandleFire()
    {
        Vector3 fireDir = new Vector3(turretJoystick.Direction.x, 0, turretJoystick.Direction.y);
        float mag = fireDir.magnitude;

        // If joystick is released (magnitude small), return -1
        if (mag <= turretJoystick.DeadZone) return -1f;

        return Mathf.Clamp01(mag);
    }
}
