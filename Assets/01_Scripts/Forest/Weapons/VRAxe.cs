using System;
using System.Collections.Generic;
using UnityEngine;
namespace ForestVR
{
    [RequireComponent(typeof(WeaponGrip))]
    public sealed class VRAxe : MonoBehaviour
    {
        public Transform blade;
        WeaponGrip grip;
        Vector3 previous, previousOwner;
        bool tracked;
        readonly Dictionary<Health,float> nextHits = new Dictionary<Health,float>();
        void Awake() => grip = GetComponent<WeaponGrip>();
        void LateUpdate()
        {
            if (blade == null || !grip.CanUse) { tracked = false; return; }
            var now = blade.position;
            if (!tracked) { previous = now; previousOwner = grip.SourcePosition; tracked = true; return; }
            var movement = now - previous;
            var swing = movement - (grip.SourcePosition - previousOwner);
            float speed = swing.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
            // Ignore teleport discontinuities and walking with a motionless hand.
            if (speed >= grip.settings.minimumSwingSpeed && movement.magnitude < 1.5f)
            {
                var hits = Physics.SphereCastAll(previous, grip.settings.hitRadius, movement.normalized,
                    movement.magnitude, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(grip.Owner.transform)) continue;
                    var health = hit.collider.GetComponentInParent<Health>();
                    if (health != null && !health.IsDead && Vector3.Distance(now, grip.SourcePosition + Vector3.up) <= grip.settings.maximumMeleeReach
                        && (!nextHits.TryGetValue(health, out float until) || Time.time >= until))
                    {
                        nextHits[health] = Time.time + grip.settings.cooldown;
                        health.TakeHit(grip.settings.damage, grip.SourcePosition);
                    }
                    break; // Walls block the blade sweep before an enemy behind them.
                }
            }
            previous = now; previousOwner = grip.SourcePosition;
        }
    }
}
