using System;
using UnityEngine;
namespace ForestVR
{
    public sealed class WeaponProjectile : MonoBehaviour
    {
        [Min(0)] public float tipLength;
        public bool stickOnHit = true;
        public bool IsFlying { get; private set; }
        WeaponSettings settings;
        Health owner;
        Transform weapon;
        Vector3 velocity, source;
        float damage;
        public void Launch(WeaponSettings config, Health shooter, Transform firedFrom, Vector3 direction, float strength, Vector3 safeOrigin)
        {
            settings = config; owner = shooter; weapon = firedFrom;
            source = shooter != null ? shooter.transform.position : safeOrigin;
            damage = settings.damage * Mathf.Clamp01(strength);
            velocity = direction.normalized * settings.projectileSpeed * Mathf.Lerp(0.35f, 1, Mathf.Clamp01(strength));
            IsFlying = true;
            Destroy(gameObject, settings.projectileLifetime);
            // Sweep from the hand to the tip before flight so a muzzle pushed through a wall cannot bypass it.
            Sweep(safeOrigin, transform.position + direction.normalized * tipLength);
        }
        void Update()
        {
            if (!IsFlying) return;
            var before = transform.position;
            var nextVelocity = velocity + Vector3.down * settings.gravity * Time.deltaTime;
            var next = before + (velocity + nextVelocity) * (0.5f * Time.deltaTime);
            if (!Sweep(before, next + nextVelocity.normalized * tipLength))
            {
                transform.position = next;
                if (nextVelocity.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(nextVelocity);
            }
            velocity = nextVelocity;
        }
        bool Sweep(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return false;
            var hits = Physics.SphereCastAll(from, settings.hitRadius, delta.normalized, delta.magnitude, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || (owner != null && hit.transform.IsChildOf(owner.transform))
                    || (weapon != null && hit.transform.IsChildOf(weapon))) continue;
                var ownWeapon = hit.collider.GetComponentInParent<WeaponGrip>();
                if (ownWeapon != null && ownWeapon.Owner == owner) continue;
                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null && health != owner) health.TakeHit(damage, source);
                IsFlying = false;
                transform.position = hit.point - delta.normalized * tipLength;
                if (stickOnHit) transform.SetParent(hit.transform, true); else Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
