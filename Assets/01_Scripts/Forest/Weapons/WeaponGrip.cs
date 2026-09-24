using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ForestVR
{
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public sealed class WeaponGrip : MonoBehaviour
    {
        static readonly List<WeaponGrip> held = new List<WeaponGrip>();
        public WeaponSettings settings;
        public Health Owner { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public InteractorHandedness HeldBy { get; private set; }
        public bool IsHeld => Grab != null && Grab.isSelected;
        public bool CanUse => IsHeld && Owner != null && !Owner.IsDead;
        Transform home;
        Rigidbody body;
        float returnAt;
        XRBaseInputInteractor stickyInteractor;
        XRBaseInputInteractor.InputTriggerType previousTrigger;
        public Vector3 SourcePosition => Owner != null ? Owner.transform.position : transform.position;
        public static WeaponGrip HeldIn(InteractorHandedness hand)
        {
            foreach (var grip in held) if (grip.HeldBy == hand) return grip;
            return null;
        }
        void Awake()
        {
            Grab = GetComponent<XRGrabInteractable>(); body = GetComponent<Rigidbody>();
            // A ray grab would leave the weapon floating at the hit point; always bring it to the hand's grip.
            Grab.farAttachMode = InteractableFarAttachMode.Near;
            Grab.useDynamicAttach = false;
            // The near-far interactor scales held objects with the thumbstick; weapons keep their scene size.
            Grab.trackScale = false;
            Grab.selectEntered.AddListener(OnGrab); Grab.selectExited.AddListener(OnRelease);
        }
        public void Configure(Health owner, Transform holster)
        {
            Owner = owner; home = holster;
            if (owner != null)
                foreach (var a in GetComponentsInChildren<Collider>())
                    foreach (var b in owner.GetComponentsInChildren<Collider>())
                        if (a != b) Physics.IgnoreCollision(a, b);
        }
        void OnGrab(SelectEnterEventArgs args)
        {
            HeldBy = args.interactorObject.handedness;
            // Sticky grip: the weapon stays in the hand after releasing Grip and drops on the next Grip press,
            // so the trigger can be used without holding Grip (the simulator cannot hold G and press T comfortably).
            if (args.interactorObject is XRBaseInputInteractor input && input != stickyInteractor)
            {
                RestoreTrigger();
                stickyInteractor = input; previousTrigger = input.selectActionTrigger;
                input.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;
            }
            if (!held.Contains(this)) held.Add(this);
            var owner = args.interactorObject.transform.GetComponentInParent<Health>();
            if (owner != null) Configure(owner, home);
        }
        void OnRelease(SelectExitEventArgs args)
        {
            if (ReferenceEquals(args.interactorObject, stickyInteractor)) RestoreTrigger();
            if (!Grab.isSelected) held.Remove(this);
            if (home != null) returnAt = Time.time + 0.75f;
            else { body.isKinematic = false; body.useGravity = true; }
        }
        void RestoreTrigger()
        {
            if (stickyInteractor != null) stickyInteractor.selectActionTrigger = previousTrigger;
            stickyInteractor = null;
        }
        void LateUpdate()
        {
            if (IsHeld || home == null || Time.time < returnAt) return;
            body.isKinematic = true;
            transform.SetPositionAndRotation(home.position, home.rotation);
        }
        void OnDestroy()
        {
            held.Remove(this);
            RestoreTrigger();
            if (Grab == null) return;
            Grab.selectEntered.RemoveListener(OnGrab); Grab.selectExited.RemoveListener(OnRelease);
        }
    }
}
