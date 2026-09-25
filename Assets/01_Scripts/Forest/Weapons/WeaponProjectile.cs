using System;
using UnityEngine;
namespace ForestVR
{
    public sealed class WeaponProjectile : MonoBehaviour
    {
        [Min(0)] public float tipLength;
        public bool stickOnHit = true;
        [Tooltip("Glowing shooting-star streak and a longer tracer line behind the projectile that show where the shot went.")]
        public bool shootingStarTrail;
        readonly System.Collections.Generic.List<TrailRenderer> trails = new System.Collections.Generic.List<TrailRenderer>();
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
            if (shootingStarTrail) CreateTrails();
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
        void CreateTrails()
        {
            // Shooting star: a wide, short, fiery streak right behind the projectile.
            trails.Add(CreateTrail("Shooting Star Trail", 0.6f,
                new AnimationCurve(new Keyframe(0, 0.09f), new Keyframe(0.25f, 0.05f), new Keyframe(1, 0)),
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, .85f, .35f), .15f), new GradientColorKey(new Color(1f, .5f, .1f), .5f), new GradientColorKey(new Color(1f, .25f, .02f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.85f, .3f), new GradientAlphaKey(0, 1) }));
            // Tracer: a thin line that follows the projectile from the muzzle and stays a moment, showing the whole shot path.
            trails.Add(CreateTrail("Tracer Trail", 2.5f,
                new AnimationCurve(new Keyframe(0, 0.018f), new Keyframe(1, 0.008f)),
                new[] { new GradientColorKey(new Color(1f, .95f, .7f), 0), new GradientColorKey(new Color(1f, .75f, .3f), 1) },
                new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(.5f, .5f), new GradientAlphaKey(0, 1) }));
        }
        TrailRenderer CreateTrail(string name, float time, AnimationCurve width, GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = time; trail.minVertexDistance = 0.1f;
            trail.widthCurve = width;
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            trail.colorGradient = gradient;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
            trail.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            trail.sharedMaterial = TrailMaterial;
            return trail;
        }
        // Sprites/Default is unlit, alpha blended and uses the vertex colours, so the streaks glow in the dark forest.
        // One material is shared by every trail instead of one per shot.
        static Material trailMaterial;
        static Material TrailMaterial
        {
            get
            {
                if (trailMaterial != null) return trailMaterial;
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                return trailMaterial = new Material(shader) { name = "Projectile Trail" };
            }
        }
        // Leave the streaks where the shot ended so they fade out instead of vanishing with the projectile.
        void DetachTrails()
        {
            foreach (var trail in trails)
            {
                if (trail == null) continue;
                trail.transform.SetParent(null, true);
                trail.emitting = false; trail.autodestruct = true;
            }
            trails.Clear();
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
                DetachTrails();
                if (stickOnHit) transform.SetParent(hit.transform, true); else Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
