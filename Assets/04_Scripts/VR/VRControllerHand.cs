using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JuegoAAA.VR
{
    /// <summary>Skinned anatomical hand aligned to the controller using its own skeleton.</summary>
    public sealed class VRControllerHand : MonoBehaviour
    {
        [SerializeField] bool leftHand;
        [SerializeField] Material handMaterial;
        [SerializeField] GameObject handModel;
        [SerializeField] Vector3 localOffset = new Vector3(0f, -0.015f, -0.06f);
        [SerializeField] Vector3 localEulerAngles = Vector3.zero;
        [SerializeField, Min(1f)] float animationSpeed = 14f;
        Transform visual;
        InputAction grip, trigger, tracked;
        float gripValue, triggerValue;
        readonly List<Joint> joints = new List<Joint>();

        struct Joint
        {
            public Transform bone;
            public Quaternion rest;
            public Vector3 axis;
            public float angle;
            public bool index;
        }

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
            foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var item in visual.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 2;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterial = handMaterial;
                renderer.updateWhenOffscreen = true;
            }

            string prefix = leftHand ? "L_" : "R_";
            var bones = new Dictionary<string, Transform>();
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

            Vector3 palmDown = -(desiredFrame * Vector3.up);
            foreach (var finger in new[] { "Index", "Middle", "Ring", "Little", "Thumb" })
            {
                foreach (var segment in new[] { "Proximal", "Intermediate", "Distal" })
                {
                    if (!bones.TryGetValue(prefix + finger + segment, out var bone) || bone.childCount == 0) continue;
                    Vector3 direction = (bone.GetChild(0).position - bone.position).normalized;
                    Vector3 target = finger == "Thumb" ? (middle.position - bone.position).normalized : palmDown;
                    Vector3 axis = Vector3.Cross(direction, target).normalized;
                    if (axis.sqrMagnitude < 0.01f) continue;
                    joints.Add(new Joint
                    {
                        bone = bone, rest = bone.localRotation,
                        axis = bone.InverseTransformDirection(axis),
                        angle = finger == "Thumb" ? 35f : segment == "Intermediate" ? 85f : segment == "Distal" ? 55f : 65f,
                        index = finger == "Index"
                    });
                }
            }
            visual.gameObject.SetActive(false);
        }

        void OnEnable() { grip?.Enable(); trigger?.Enable(); tracked?.Enable(); }
        void OnDisable()
        {
            grip?.Disable(); trigger?.Disable(); tracked?.Disable();
            if (visual != null) visual.gameObject.SetActive(false);
        }

        void Update()
        {
            if (visual == null) return;
            visual.gameObject.SetActive(tracked.ReadValue<float>() > 0.5f);
            float step = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);
            gripValue = Mathf.Lerp(gripValue, Mathf.Clamp01(grip.ReadValue<float>()), step);
            triggerValue = Mathf.Lerp(triggerValue, Mathf.Clamp01(trigger.ReadValue<float>()), step);
            foreach (var joint in joints)
            {
                float curl = joint.index ? triggerValue : gripValue;
                joint.bone.localRotation = joint.rest * Quaternion.AngleAxis(Mathf.Lerp(3f, joint.angle, curl), joint.axis);
            }
        }

        void OnDestroy() { grip?.Dispose(); trigger?.Dispose(); tracked?.Dispose(); }
    }
}
