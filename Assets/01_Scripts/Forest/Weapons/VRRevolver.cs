using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
namespace ForestVR
{
    [RequireComponent(typeof(WeaponGrip))]
    public sealed class VRRevolver : MonoBehaviour
    {
        public Transform muzzle;
        public WeaponProjectile projectilePrefab;
        WeaponGrip grip;
        float readyAt;
        void Start() { grip = GetComponent<WeaponGrip>(); grip.Grab.activated.AddListener(Fire); }
        void Fire(ActivateEventArgs args) => TryFire();
        public bool TryFire()
        {
            if (grip == null || !grip.CanUse || Time.time < readyAt || projectilePrefab == null || muzzle == null) return false;
            readyAt = Time.time + grip.settings.cooldown;
            var shot = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
            shot.Launch(grip.settings, grip.Owner, transform, muzzle.forward, 1, muzzle.position);
            return true;
        }
        void OnDestroy() { if (grip != null && grip.Grab != null) grip.Grab.activated.RemoveListener(Fire); }
    }
}
