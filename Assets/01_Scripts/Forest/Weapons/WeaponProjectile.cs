using System;
using UnityEngine;
namespace ForestVR
{
    public sealed class WeaponProjectile : MonoBehaviour
    {
        [Min(0)] public float tipLength;
        public bool stickOnHit = true;
        [Tooltip("Glowing shooting-star streak behind the projectile that shows where the shot went.")]
        public bool shootingStarTrail;
        TrailRenderer trail;
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
            if (shootingStarTrail) CreateTrail();
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
        void CreateTrail()
        {
            var go = new GameObject("Shooting Star Trail");
            go.transform.SetParent(transform, false);
            trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.3f; trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 0.025f), new Keyframe(1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, .97f, .8f), 0), new GradientColorKey(new Color(1f, .6f, .15f), .35f), new GradientColorKey(new Color(1f, .35f, .05f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.7f, .35f), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
            // Sprites/Default is unlit, alpha blended and uses the vertex colours, so the streak glows in the dark forest.
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            trail.material = new Material(shader);
        }
        // Leave the streak where the shot ended so it fades out instead of vanishing with the projectile.
        void DetachTrail()
        {
            if (trail == null) return;
            trail.transform.SetParent(null, true);
            trail.emitting = false; trail.autodestruct = true;
            Destroy(trail.material, trail.time + .1f);
            trail = null;
        }
        void OnDestroy() { if (trail != null) Destroy(trail.material); }
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
                DetachTrail();
                if (stickOnHit) transform.SetParent(hit.transform, true); else Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
