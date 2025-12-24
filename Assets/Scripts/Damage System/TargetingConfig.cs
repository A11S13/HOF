using UnityEngine;

[CreateAssetMenu(menuName = "Targeting/Targeting Config")]
public class TargetingConfig : ScriptableObject
{
    public bool targetAll = true;

    public bool ships = true;
    public bool asteroids = true;
    public bool aliens = true;

    public bool Allows(Targetable t)
    {
        if (!CategoryAllowed(t.type))
            return false;

        if (!targetAll &&
            t.TryGetComponent(out Alignment a) &&
            a.isFriendly)
            return false;

        return true;
    }

    bool CategoryAllowed(TargetType type) =>
        (type == TargetType.Ship && ships)
     || (type == TargetType.Asteroid && asteroids)
     || (type == TargetType.Alien && aliens);
}
