using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public GameObject weaponTogglePrefab;

    ShipCore _sC;

    void Start()
    {
        _sC = GameObject.Find("MOTHERSHIP").GetComponent<ShipCore>();
        foreach (var weapon in _sC.Weapons)
        {
            // Instantiate the weaponTogglePrefab for each weapon
            GameObject newToggle = Instantiate(
                weaponTogglePrefab,
                GameObject.Find("Canvas/Weapons/Scroll/Panel").transform
            );

            // Access the ToggleLogic script on the instantiated Toggle
            WeaponToggle weaponToggle = newToggle.GetComponent<WeaponToggle>();

            weaponToggle.SetAssociatedWeapon(weapon);

            // Assuming Weapon has a unique identifier, use it to customize the toggle
            weaponToggle.SetWeaponInfo(weapon); // Customize the toggle based on the weapon info
        }
    }

    void Update() { }
}
