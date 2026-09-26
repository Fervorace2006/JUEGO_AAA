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
                // A cast misses colliders already touching the edge at the start of a swing.
                // Overlap the whole path of the blade point, including both ends.
                var hits = Physics.OverlapCapsule(previous, now, grip.settings.hitRadius,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a,b) =>
                    (a.ClosestPoint(previous) - previous).sqrMagnitude.CompareTo(
                        (b.ClosestPoint(previous) - previous).sqrMagnitude));
                foreach (var hit in hits)
                {
                    if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(grip.Owner.transform)) continue;
                    var health = hit.GetComponentInParent<Health>();
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
