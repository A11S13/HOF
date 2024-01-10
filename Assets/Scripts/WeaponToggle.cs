using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponToggle : MonoBehaviour
{
    private Weapon associatedWeapon;

    private void Start()
    {
        // Subscribe to the OnValueChanged event
        GetComponent<Toggle>()
            .onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnToggleValueChanged(bool isOn)
    {
        associatedWeapon.ToggleAI();
        Debug.Log("Toggle value changed: " + isOn);
    }

    public void SetAssociatedWeapon(Weapon weapon)
    {
        associatedWeapon = weapon;
    }

    public void SetWeaponInfo(Weapon weapon)
    {
        // Customize the toggle based on the information of the corresponding weapon
        // For example, set toggle text, icon, etc.
    }
}
