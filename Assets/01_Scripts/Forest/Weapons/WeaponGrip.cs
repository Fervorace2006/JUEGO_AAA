using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ForestVR
{
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public sealed class WeaponGrip : MonoBehaviour, IXRSelectFilter
    {
        public enum GripStyle { Auto, Pistol, Axe, Bow }
        static readonly List<WeaponGrip> held = new List<WeaponGrip>();
        public WeaponSettings settings;
        [Tooltip("Hand pose while held. Auto picks it from the weapon script (revolver, axe or bow).")]
        public GripStyle gripStyle;
        [Tooltip("Fine tuning of the right hand on the grip, in meters of weapon space. The left hand is mirrored.")]
        public Vector3 handPositionOffset;
        [Tooltip("Fine tuning of the right hand's turn around the handle, in degrees. The left hand is mirrored.")]
        public float handYawOffset;
        [Tooltip("Size multiplier while held, relative to its size on the table in the scene. 0 = keep the table size.")]
        [Min(0)] public float heldScale;
        public HandPoseStyle Style { get; private set; }
        public VRBow Bow { get; private set; }
        // The bow is held in the left hand so the right hand draws the arrow.
        public InteractorHandedness RequiredHand => Bow != null ? InteractorHandedness.Left : InteractorHandedness.None;
        public Health Owner { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public InteractorHandedness HeldBy { get; private set; }
        public bool IsHeld => Grab != null && Grab.isSelected;
        public bool CanUse => IsHeld && Owner != null && !Owner.IsDead;
        Transform home;
        Rigidbody body;
        float returnAt;
        Vector3 restScale, spawnPosition;
        Quaternion spawnRotation;
        XRBaseInputInteractor stickyInteractor;
        XRBaseInputInteractor.InputTriggerType previousTrigger;
        IXRSelectInteractor handOverFrom;
        // Riser position inside the closed fist, in palm joint space: toward the palm and slightly toward the fingers.
        static readonly Vector3 PalmGripOffset = new Vector3(0, -0.035f, 0.02f);
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
            Grab.selectFilters.Add(this);
            Bow = GetComponent<VRBow>();
            var style = gripStyle;
            if (style == GripStyle.Auto) style = Bow != null ? GripStyle.Bow : GetComponent<VRRevolver>() != null ? GripStyle.Pistol : GripStyle.Axe;
            Style = style == GripStyle.Pistol ? HandPoseStyle.Pistol : style == GripStyle.Bow ? HandPoseStyle.BowRiser : HandPoseStyle.Axe;
            restScale = transform.localScale;
            // The weapon collider and the table are a few centimeters thick: discrete checks let a falling weapon pass through.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            spawnPosition = transform.position; spawnRotation = transform.rotation;
            if (heldScale <= 0) heldScale = 1;
            MakeSolid();
        }
        // The prefab only has a small box around the handle: the weapon sank into the table and could only be grabbed
        // there. Every mesh of the model gets a box collider around it (the whole weapon rests on surfaces and can be
        // grabbed anywhere; it still snaps to the grip), and its faces are drawn from both sides: the generated models
        // have one-sided faces that looked see-through from some angles.
        // Boxes, not convex mesh colliders: the generated models have millions of triangles and cooking their hull
        // froze the Quest for minutes while ForestScene loaded.
        void MakeSolid()
        {
            var model = Grab.predictedVisualsTransform;
            if (model == null || model == transform) return;
            bool added = false;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || filter.GetComponent<Collider>() != null) continue;
                filter.gameObject.layer = gameObject.layer;
                var collider = filter.gameObject.AddComponent<BoxCollider>();
                collider.center = mesh.bounds.center;
                collider.size = mesh.bounds.size;
                Grab.colliders.Add(collider);
                added = true;
            }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.materials)
                {
                    MakeUnlit(material);
                    if (material.HasProperty(CullId)) material.SetFloat(CullId, (float)UnityEngine.Rendering.CullMode.Off);
                }
                LimitDetail(renderer);
            }
            // The interactable registered its colliders when it was enabled; register it again with the new ones.
            if (added && Grab.enabled) { Grab.enabled = false; Grab.enabled = true; }
        }
        static readonly int CullId = Shader.PropertyToID("_Cull");
        static Shader unlit;
        // The weapons keep their own colors whatever the light: the night, the flashlight or the blood moon do not
        // darken or tint them. Same texture and color, on the URP Unlit shader (also used by the bow string).
        static void MakeUnlit(Material material)
        {
            if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null || material.shader == unlit) return;
            var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap")
                : material.HasProperty("_MainTex") ? material.mainTexture : null;
            var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color") ? material.color : Color.white;
            material.shader = unlit;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
        }
        const int MaxTriangles = 40000;
        // The generated axe has almost a million triangles, more than the rest of the forest together: on the Quest the
        // frame rate fell to 20-30 FPS and the view flickered while moving. Its model imports Mesh LODs (simplified
        // versions); the weapon always draws the most detailed one within budget, on the table and in the hand.
        static void LimitDetail(Renderer renderer)
        {
            var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                : renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
            if (mesh == null || mesh.lodCount <= 1) return;
            for (int lod = 0; lod < mesh.lodCount; lod++)
            {
                long triangles = 0;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += mesh.GetLod(sub, lod).indexCount / 3;
                if (triangles <= MaxTriangles || lod == mesh.lodCount - 1) { renderer.forceMeshLod = (short)lod; return; }
            }
        }
        public bool canProcess => isActiveAndEnabled;
        // Interactors without handedness (scripted or test hands) can hold any weapon. The other hand may pick up
        // a free bow, which is then handed over to the required hand; it cannot take the bow from that hand.
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) =>
            RequiredHand == InteractorHandedness.None || interactor.handedness == InteractorHandedness.None
            || interactor.handedness == RequiredHand || !Grab.isSelected;
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
            if (RequiredHand != InteractorHandedness.None && HeldBy != InteractorHandedness.None && HeldBy != RequiredHand)
                handOverFrom = args.interactorObject;
            // Sticky grip: the weapon stays in the hand after releasing Grip and drops on the next Grip press,
            // so the trigger can be used without holding Grip (the simulator cannot hold G and press T comfortably).
            if (args.interactorObject is XRBaseInputInteractor input && input != stickyInteractor)
            {
                RestoreTrigger();
                stickyInteractor = input; previousTrigger = input.selectActionTrigger;
                input.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;
            }
            if (!held.Contains(this)) held.Add(this);
            // Scaled around the grip (the attach point is the weapon origin), so the handle stays in the hand.
            transform.localScale = restScale * heldScale;
            var owner = args.interactorObject.transform.GetComponentInParent<Health>();
            if (owner != null) Configure(owner, home);
        }
        void OnRelease(SelectExitEventArgs args)
        {
            if (ReferenceEquals(args.interactorObject, stickyInteractor)) RestoreTrigger();
            if (!Grab.isSelected) { held.Remove(this); transform.localScale = restScale; SetTracking(true); }
            if (home != null) returnAt = Time.time + 0.75f;
            else { body.isKinematic = false; body.useGravity = true; }
        }
        void RestoreTrigger()
        {
            if (stickyInteractor != null) stickyInteractor.selectActionTrigger = previousTrigger;
            stickyInteractor = null;
        }
        // Selection cannot change inside the select event, so the bow moves to the left hand on the next frame.
        void Update()
        {
            if (handOverFrom == null) return;
            var from = handOverFrom; handOverFrom = null;
            var manager = Grab.interactionManager;
            if (manager == null || !Grab.interactorsSelecting.Contains(from)) return;
            var to = FindHand(from, RequiredHand);
            if (to == null) return; // No free hand of that side: keep it where it was grabbed.
            manager.SelectExit(from, Grab);
            manager.SelectEnter(to, (IXRSelectInteractable)Grab);
        }
        static IXRSelectInteractor FindHand(IXRSelectInteractor from, InteractorHandedness hand)
        {
            foreach (var interactor in from.transform.root.GetComponentsInChildren<XRBaseInputInteractor>())
                if (interactor.isActiveAndEnabled && interactor.handedness == hand && !interactor.hasSelection
                    && (interactor is NearFarInteractor || interactor is XRDirectInteractor))
                    return interactor;
            return null;
        }
        void OnEnable() => Application.onBeforeRender += FollowPalm;
        void OnDisable() => Application.onBeforeRender -= FollowPalm;
        // With tracked hands the bow sits in the palm of the holding hand instead of at the pinch point in front of it:
        // riser inside the fist, thumb side up, shooting along the hand. Controllers keep the normal XR attach.
        void FollowPalm()
        {
            if (Bow == null || !IsHeld || !WeaponHandPoses.TryGetPalm(HeldBy, out var palm)) { SetTracking(true); return; }
            SetTracking(false);
            var thumbSide = HeldBy == InteractorHandedness.Left ? palm.right : -palm.right;
            transform.SetPositionAndRotation(palm.position + palm.rotation * PalmGripOffset, Quaternion.LookRotation(palm.forward, thumbSide));
        }
        void SetTracking(bool on)
        {
            if (Grab.trackPosition == on && Grab.trackRotation == on) return;
            Grab.trackPosition = on; Grab.trackRotation = on;
        }
        void LateUpdate()
        {
            if (IsHeld) { FollowPalm(); return; }
            // Dropped off the map or through the ground: put it back where it started (the table).
            if (!body.isKinematic && GroundSafety.IsLost(transform.position))
            {
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.isKinematic = true; body.useGravity = false;
                transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                return;
            }
            if (home == null || Time.time < returnAt) return;
            body.isKinematic = true;
            transform.SetPositionAndRotation(home.position, home.rotation);
        }
        void OnDestroy()
        {
            held.Remove(this);
            RestoreTrigger();
            if (Grab == null) return;
            Grab.selectFilters.Remove(this);
            Grab.selectEntered.RemoveListener(OnGrab); Grab.selectExited.RemoveListener(OnRelease);
        }
    }
}
