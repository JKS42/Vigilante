using UnityEngine;

/// <summary>
/// References to the imported Cartoon FX prefabs used by combat.
/// Lives in Resources so weapons can spawn them without scene wiring.
/// </summary>
public class CombatVfxLibrary : ScriptableObject
{
    public GameObject bulletHit;
    public GameObject wallSmoke;
    public GameObject muzzleFire;
    public GameObject boomText;
    public GameObject boingText;
    public GameObject cursedText;
    public GameObject spawnBurst;
}
