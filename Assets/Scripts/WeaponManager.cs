using UnityEngine;

/// <summary>
/// Manages weapon UI toggles for player ship only
/// </summary>
public class WeaponManager : MonoBehaviour
{
    public GameObject weaponTogglePrefab;

    private ShipCore shipCore;
    private Transform toggleParent;

    private void Awake()
    {
        // Find MOTHERSHIP in children
        var mothership = transform.GetComponentInChildren<ShipCore>();
        if (mothership != null)
        {
            shipCore = mothership;
        }
        else
        {
            Debug.LogError("WeaponManager: Could not find ShipCore in children!");
            enabled = false;
            return;
        }

        // Find toggle parent
        var canvas = GameObject.Find("Canvas/Weapons/Scroll/Panel");
        if (canvas != null)
        {
            toggleParent = canvas.transform;
        }
        else
        {
            Debug.LogWarning("WeaponManager: Canvas/Weapons/Scroll/Panel not found. Weapon toggles will not be created.");
        }
    }

    void Start()
    {
        if (shipCore == null || toggleParent == null || weaponTogglePrefab == null)
            return;

        // Wait one frame for weapons to be initialized
        StartCoroutine(CreateWeaponToggles());
    }

    System.Collections.IEnumerator CreateWeaponToggles()
    {
        yield return null; // Wait for ShipCore initialization

        if (shipCore.Weapons == null || shipCore.Weapons.Length == 0)
        {
            Debug.LogWarning("WeaponManager: No weapons found on ShipCore");
            yield break;
        }

        foreach (var weapon in shipCore.Weapons)
        {
            if (weapon == null) continue;

            // Instantiate toggle
            GameObject newToggle = Instantiate(weaponTogglePrefab, toggleParent);

            // Setup toggle
            WeaponToggle weaponToggle = newToggle.GetComponent<WeaponToggle>();
            if (weaponToggle != null)
            {
                weaponToggle.SetAssociatedWeapon(weapon);
                weaponToggle.SetWeaponInfo(weapon);
            }
        }
    }
}
