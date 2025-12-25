using UnityEngine;

public class GameEventManager : MonoBehaviour
{
    [SerializeField] int neutralKillThreshold = 3;
    [SerializeField] int wavesPerTrigger = 1;

    int neutralKills;

    void OnEnable()
    {
        EventBus.Instance.Subscribe<AnimaKilledEvent>(OnAnimaKilled);
    }

    void OnAnimaKilled(AnimaKilledEvent e)
    {
        if (!e.WasFriendly)
            return;

        neutralKills++;

        if (neutralKills < neutralKillThreshold)
            return;

        neutralKills = 0;

        EventBus.Instance.Publish(
            new StartAnimaWaveEvent(e.Position, wavesPerTrigger)
        );
    }
}
