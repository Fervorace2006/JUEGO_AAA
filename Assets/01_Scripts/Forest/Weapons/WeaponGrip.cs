using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ForestVR
{
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public sealed class WeaponGrip : MonoBehaviour
    {
        public WeaponSettings settings;
        public Health Owner { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public bool IsHeld => Grab != null && Grab.isSelected;
        public bool CanUse => IsHeld && Owner != null && !Owner.IsDead;
        Transform home;
        Rigidbody body;
        float returnAt;
        public Vector3 SourcePosition => Owner != null ? Owner.transform.position : transform.position;
        void Awake()
        {
            Grab = GetComponent<XRGrabInteractable>(); body = GetComponent<Rigidbody>();
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
            var owner = args.interactorObject.transform.GetComponentInParent<Health>();
            if (owner != null) Configure(owner, home);
        }
        void OnRelease(SelectExitEventArgs args) => returnAt = Time.time + 0.75f;
        void LateUpdate()
        {
            if (IsHeld || home == null || Time.time < returnAt) return;
            body.isKinematic = true;
            transform.SetPositionAndRotation(home.position, home.rotation);
        }
        void OnDestroy()
        {
            if (Grab == null) return;
            Grab.selectEntered.RemoveListener(OnGrab); Grab.selectExited.RemoveListener(OnRelease);
        }
    }
}
