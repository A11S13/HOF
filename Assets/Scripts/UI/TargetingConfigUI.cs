using UnityEngine;
using UnityEngine.UI;

public class TargetingConfigUI : MonoBehaviour
{
    [SerializeField] TargetingConfig config;
    [SerializeField] Toggle targetAll;
    [SerializeField] Toggle ships;
    [SerializeField] Toggle asteroids;
    [SerializeField] Toggle aliens;

    void OnEnable()
    {
        targetAll.isOn = config.targetAll;
        ships.isOn = config.ships;
        asteroids.isOn = config.asteroids;
        aliens.isOn = config.aliens;

        targetAll.onValueChanged.AddListener(v => config.targetAll = v);
        ships.onValueChanged.AddListener(v => config.ships = v);
        asteroids.onValueChanged.AddListener(v => config.asteroids = v);
        aliens.onValueChanged.AddListener(v => config.aliens = v);
    }
}
