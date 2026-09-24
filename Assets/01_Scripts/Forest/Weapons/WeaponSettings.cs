using UnityEngine;
namespace ForestVR
{
    [CreateAssetMenu(menuName = "Forest VR/Weapon Settings")]
    public sealed class WeaponSettings : ScriptableObject
    {
        [Min(0)] public float damage = 25;
        [Min(0.01f)] public float cooldown = 0.5f;
        [Min(1)] public float projectileSpeed = 50;
        [Min(0)] public float gravity = 9.81f;
        [Min(0.001f)] public float hitRadius = 0.015f;
        [Min(1)] public float projectileLifetime = 15;
        [Min(0.01f)] public float maximumDraw = 0.55f;
        [Min(0.01f)] public float minimumDraw = 0.08f;
        [Min(0)] public float minimumSwingSpeed = 1.2f;
        [Min(0.1f)] public float maximumMeleeReach = 1.6f;
    }
}
