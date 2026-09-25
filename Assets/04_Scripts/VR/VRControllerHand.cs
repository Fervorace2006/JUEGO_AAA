using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using ForestVR;

namespace JuegoAAA.VR
{
    /// <summary>Skinned anatomical hand aligned to the controller using its own skeleton.
    /// Holding a weapon snaps the hand onto its grip with that weapon's finger pose.</summary>
    // After the weapons (VRBow moves its nock in LateUpdate) so the hand lands on this frame's grip.
    [DefaultExecutionOrder(100)]
    public sealed class VRControllerHand : MonoBehaviour
    {
        const int Thumb = 0, Index = 1;
        static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };
        static readonly string[] Segments = { "Proximal", "Intermediate", "Distal" };

        [SerializeField] bool leftHand;
        [SerializeField] Material handMaterial;
        [SerializeField] GameObject handModel;
        [SerializeField] Vector3 localOffset = new Vector3(0f, -0.015f, -0.06f);
        [SerializeField] Vector3 localEulerAngles = Vector3.zero;
        [SerializeField, Min(1f)] float animationSpeed = 14f;
        [SerializeField, Min(1f)] float snapSpeed = 20f;
        [Tooltip("Depth of the handle's center below the middle knuckle, toward the palm.")]
        [SerializeField, Min(0f)] float gripDepth = 0.03f;
        [Tooltip("Seconds the controller may lose tracking before its hand hides. Short dropouts no longer blink the hand.")]
        [SerializeField, Min(0f)] float trackingLostGrace = 2f;

        Transform visual, arrowPoint;
        InputAction grip, trigger, tracked;
        float gripValue, triggerValue, lastTrackedTime = float.NegativeInfinity;
        bool visible;
        Vector3 restPosition;
        Quaternion restRotation, gripFrame;
        Vector3 gripCenter;
        readonly float[] fingerCurl = new float[5];
        Joint[] joints;

        struct Joint
        {
            public Transform bone;
            public Quaternion rest;
            public Vector3 axis;
            public float angle;
            public int finger;
        }

        InteractorHandedness Side => leftHand ? InteractorHandedness.Left : InteractorHandedness.Right;

        public void Configure(bool isLeft, Material material, GameObject model)
        {
            leftHand = isLeft;
            handMaterial = material;
            handModel = model;
        }

        void Awake()
        {
            string device = leftHand ? "<XRController>{LeftHand}" : "<XRController>{RightHand}";
            grip = new InputAction("Hand Grip", InputActionType.Value, device + "/grip");
            trigger = new InputAction("Hand Trigger", InputActionType.Value, device + "/trigger");
            tracked = new InputAction("Hand Tracked", InputActionType.Value, device + "/isTracked");
            if (handModel == null)
            {
                Debug.LogError("Assign the left/right XR hand model to VRControllerHand.", this);
                enabled = false;
                return;
            }
            visual = Instantiate(handModel, transform, false).transform;
            visual.name = leftHand ? "Left Anatomical Hand" : "Right Anatomical Hand";
            foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) Destroy(animator);
            foreach (var item in visual.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 2;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Destroy(collider);

            string prefix = leftHand ? "L_" : "R_";
            var bones = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var bone in visual.GetComponentsInChildren<Transform>(true)) bones[bone.name] = bone;
            if (!bones.TryGetValue(prefix + "Wrist", out var wrist)
                || !bones.TryGetValue(prefix + "MiddleProximal", out var middle)
                || !bones.TryGetValue(prefix + "IndexProximal", out var index)
                || !bones.TryGetValue(prefix + "LittleProximal", out var little))
            {
                Debug.LogError("The XR hand skeleton is missing its wrist or finger landmarks.", this);
                visual.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            // Map wrist -> fingers to controller forward, and the dorsal side to up.
            // The index sits on +X for the left hand and -X for the right hand.
            // No negative scale or guessed 180-degree mirror is used.
            Vector3 forward = (middle.position - wrist.position).normalized;
            Vector3 right = (leftHand ? index.position - little.position : little.position - index.position).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            var desiredFrame = transform.rotation * Quaternion.Euler(localEulerAngles);
            visual.rotation = desiredFrame * Quaternion.Inverse(Quaternion.LookRotation(forward, up)) * visual.rotation;
            visual.position += transform.TransformPoint(localOffset) - wrist.position;
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;

            // Grip frame in the hand's own space: forward along the straight fingers, up along the knuckles toward the index,
            // centered where a handle sits inside the closed fist.
            Vector3 palmDown = -(desiredFrame * Vector3.up);
            forward = (middle.position - wrist.position).normalized;
            Vector3 knuckles = (index.position - little.position).normalized;
            gripFrame = Quaternion.Inverse(visual.rotation) * Quaternion.LookRotation(forward, knuckles);
            gripCenter = visual.InverseTransformPoint(middle.position + palmDown * gripDepth);

            // Arrow nock pinched between the thumb and the index, the shaft along the fingers.
            arrowPoint = new GameObject("Arrow Hold Point").transform;
            arrowPoint.SetParent(visual, false);
            arrowPoint.SetPositionAndRotation(index.position + palmDown * 0.02f + forward * 0.01f, Quaternion.LookRotation(forward, -palmDown));

            var list = new System.Collections.Generic.List<Joint>();
            for (int f = 0; f < FingerNames.Length; f++)
            {
                foreach (var segment in Segments)
                {
                    if (!bones.TryGetValue(prefix + FingerNames[f] + segment, out var bone) || bone.childCount == 0) continue;
                    Vector3 direction = (bone.GetChild(0).position - bone.position).normalized;
                    Vector3 target = f == Thumb ? (middle.position - bone.position).normalized : palmDown;
                    Vector3 axis = Vector3.Cross(direction, target).normalized;
                    if (axis.sqrMagnitude < 0.01f) continue;
                    list.Add(new Joint
                    {
                        bone = bone, rest = bone.localRotation,
                        axis = bone.InverseTransformDirection(axis),
                        angle = f == Thumb ? 35f : segment == "Intermediate" ? 85f : segment == "Distal" ? 55f : 65f,
                        finger = f
                    });
                }
            }
            joints = list.ToArray();

            // Hands are always near the camera: skip per-frame bound recomputation, shadows and motion vectors.
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterial = handMaterial;
                renderer.updateWhenOffscreen = false;
                renderer.localBounds = new Bounds(renderer.localBounds.center, renderer.localBounds.size * 1.5f);
                renderer.quality = SkinQuality.Bone2;
                renderer.skinnedMotionVectors = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            visual.gameObject.SetActive(false);
        }

        void OnEnable() { grip?.Enable(); trigger?.Enable(); tracked?.Enable(); }
        void OnDisable()
        {
            grip?.Disable(); trigger?.Disable(); tracked?.Disable();
            SetVisible(false);
        }

        void SetVisible(bool value)
        {
            if (visual == null || visible == value) return;
            visible = value;
            visual.gameObject.SetActive(value);
            if (value) WeaponHandPoses.SetArrowPoint(Side, arrowPoint);
            else WeaponHandPoses.ClearArrowPoint(Side, arrowPoint);
        }

        void LateUpdate()
        {
            if (visual == null) return;
            var pose = WeaponHandPoses.Resolve(Side);
            // A controller blinks out of tracking when occluded or close to the face; keep its hand for a grace period,
            // and always while it holds something.
            if (tracked.ReadValue<float>() > 0.5f) lastTrackedTime = Time.time;
            SetVisible(pose.style != HandPoseStyle.None || Time.time - lastTrackedTime <= trackingLostGrace);
            if (!visible) return;

            float step = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);
            gripValue = Mathf.Lerp(gripValue, Mathf.Clamp01(grip.ReadValue<float>()), step);
            triggerValue = Mathf.Lerp(triggerValue, Mathf.Clamp01(trigger.ReadValue<float>()), step);
            for (int f = 0; f < fingerCurl.Length; f++)
            {
                float target;
                if (pose.style == HandPoseStyle.None) target = f == Index ? Mathf.Max(triggerValue, gripValue * 0.65f) : gripValue;
                else target = pose.Curl(f);
                if (f == Index && pose.IndexOnTrigger) target = Mathf.Lerp(target, WeaponHandPoses.TriggerPulledIndex, triggerValue);
                fingerCurl[f] = Mathf.Lerp(fingerCurl[f], target, step);
            }
            for (int i = 0; i < joints.Length; i++)
            {
                ref var joint = ref joints[i];
                float openAngle = joint.finger == Index ? -18f : 0f;
                joint.bone.localRotation = joint.rest * Quaternion.AngleAxis(Mathf.Lerp(openAngle, joint.angle, fingerCurl[joint.finger]), joint.axis);
            }

            // Snap the hand's grip frame onto the weapon; otherwise return to the controller rest pose.
            Vector3 targetPosition = restPosition;
            Quaternion targetRotation = restRotation;
            if (pose.snap)
            {
                var worldRotation = pose.anchor.rotation * Quaternion.Inverse(gripFrame);
                targetRotation = Quaternion.Inverse(transform.rotation) * worldRotation;
                var worldPosition = pose.anchor.position - worldRotation * Vector3.Scale(visual.lossyScale, gripCenter);
                targetPosition = transform.InverseTransformPoint(worldPosition);
            }
            float snapStep = 1f - Mathf.Exp(-snapSpeed * Time.deltaTime);
            visual.localPosition = Vector3.Lerp(visual.localPosition, targetPosition, snapStep);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, targetRotation, snapStep);
        }

        void OnDestroy() { grip?.Dispose(); trigger?.Dispose(); tracked?.Dispose(); }
    }
}
