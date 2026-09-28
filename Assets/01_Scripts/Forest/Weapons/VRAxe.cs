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
        readonly HashSet<Health> struckWithoutSeparation = new HashSet<Health>();
        readonly HashSet<Health> contacts = new HashSet<Health>();
        float swingTravel, lastMotionAt;
        // Round damage zone on the head of the axe: a trigger sphere on the blade point (editable in the Inspector),
        // sized from the weapon settings. Hits are swept with its radius, so a glancing blow still lands.
        SphereCollider tip;
        // How far the head must travel in a swing to count as a blow, independent of the zone size.
        const float MinimumSwingTravel = .07f;
        float TipRadius => tip != null ? tip.radius * MaxScale(blade) : grip.settings.hitRadius;
        void Awake()
        {
            grip = GetComponent<WeaponGrip>();
            if (blade == null) return;
            tip = blade.GetComponent<SphereCollider>();
            if (tip == null)
            {
                tip = blade.gameObject.AddComponent<SphereCollider>();
                tip.radius = grip.settings.hitRadius / MaxScale(blade);
            }
            // Only marks the zone: it neither blocks nor is grabbed; the hits are found by the sweep below.
            tip.isTrigger = true;
        }
        static float MaxScale(Transform t) { var s = t.lossyScale; return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z), .0001f); }
        void LateUpdate()
        {
            // Paused (pause menu): swinging the axe does not hurt anyone.
            if (blade == null || !grip.CanUse || PauseMenu.Paused)
            {
                tracked = false; swingTravel = 0;
                struckWithoutSeparation.Clear(); contacts.Clear();
                return;
            }
            var now = blade.position;
            if (!tracked)
            {
                previous = now; previousOwner = grip.SourcePosition;
                lastMotionAt = Time.time; tracked = true; return;
            }
            var movement = now - previous;
            var swing = movement - (grip.SourcePosition - previousOwner);
            if (movement.magnitude >= 1.5f)
            {
                previous = now; previousOwner = grip.SourcePosition; swingTravel = 0;
                return; // Ignore teleports and grab snapping.
            }
            if (swing.magnitude > Mathf.Max(0.00001f, Time.deltaTime * 0.05f))
            {
                swingTravel += swing.magnitude;
                lastMotionAt = Time.time;
            }
            else if (Time.time - lastMotionAt > 0.2f) swingTravel = 0;

            // Sweep the blade point so a fast swing cannot pass through a thin collider.
            float radius = TipRadius;
            var hits = Physics.OverlapCapsule(previous, now, radius,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a,b) =>
                (a.ClosestPoint(previous) - previous).sqrMagnitude.CompareTo(
                    (b.ClosestPoint(previous) - previous).sqrMagnitude));
            contacts.Clear();
            foreach (var hit in hits)
            {
                var health = Target(hit);
                if (health != null) contacts.Add(health);
            }
            // Keep a hit latched while the edge is resting on the same body, even after cooldown.
            foreach (var hit in Physics.OverlapSphere(now, radius,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                var health = Target(hit);
                if (health != null) contacts.Add(health);
            }
            struckWithoutSeparation.RemoveWhere(health => health == null || !contacts.Contains(health));

            if (swingTravel >= MinimumSwingTravel && Time.time - lastMotionAt <= 0.2f)
            {
                foreach (var hit in hits)
                {
                    if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(grip.Owner.transform)) continue;
                    var health = Target(hit);
                    if (health != null && !health.IsDead && !struckWithoutSeparation.Contains(health)
                        && (!nextHits.TryGetValue(health, out float until) || Time.time >= until))
                    {
                        nextHits[health] = Time.time + grip.settings.cooldown;
                        health.TakeHit(grip.settings.damage, grip.SourcePosition);
                        struckWithoutSeparation.Add(health);
                        swingTravel = 0;
                        Buzz();
                    }
                    break; // Walls block the blade sweep before an enemy behind them.
                }
            }
            previous = now; previousOwner = grip.SourcePosition;
        }
        // The hand holding the axe feels the blow.
        void Buzz()
        {
            foreach (var interactor in grip.Grab.interactorsSelecting)
                if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor input)
                    input.SendHapticImpulse(.7f, .12f);
        }
        Health Target(Collider hit)
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(grip.Owner.transform)) return null;
            var health = hit.GetComponentInParent<Health>();
            return health != grip.Owner ? health : null;
        }
    }
}

