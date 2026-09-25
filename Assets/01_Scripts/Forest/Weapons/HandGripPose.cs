using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ForestVR
{
    // Bends the XR Hands skeleton right after XRHandSkeletonDriver writes the tracked joints:
    // closed around a held weapon with that weapon's grip (revolver, axe, bow, string, arrow),
    // and relaxed in the XR Interaction Simulator, whose captured hands point with the index.
    // With real hand tracking and nothing held, the tracked fingers are left untouched.
    [RequireComponent(typeof(XRHandSkeletonDriver))]
    public sealed class HandGripPose : MonoBehaviour
    {
        static readonly XRHandFingerID[] Fingers = { XRHandFingerID.Thumb, XRHandFingerID.Index, XRHandFingerID.Middle, XRHandFingerID.Ring, XRHandFingerID.Little };
        // Flexion in degrees: thumb proximal, thumb distal, then finger proximal, intermediate, distal.
        static readonly float[] Relaxed = { 8, 8, 12, 18, 10 };
        static readonly float[] Fist = { 30, 35, 70, 90, 55 };

        [SerializeField, Min(1)] float blendSpeed = 12;
        XRHandSkeletonDriver driver;
        XRHandTrackingEvents events;
        Transform arrowPoint;
        readonly Transform[] joints = new Transform[XRHandJointID.EndMarker.ToIndex()];
        readonly float[] angles = new float[XRHandJointID.EndMarker.ToIndex()];
        readonly float[] targets = new float[XRHandJointID.EndMarker.ToIndex()];
        float weight;

        InteractorHandedness Side => events != null && events.handedness == Handedness.Left ? InteractorHandedness.Left : InteractorHandedness.Right;

        void Awake()
        {
            driver = GetComponent<XRHandSkeletonDriver>();
            foreach (var reference in driver.jointTransformReferences)
            {
                int index = reference.xrHandJointID.ToIndex();
                if (index >= 0 && index < joints.Length) joints[index] = reference.jointTransform;
            }
            // Palm joint frame: +Z toward the fingers, +Y on the back of the hand.
            var palm = joints[XRHandJointID.Palm.ToIndex()];
            if (palm != null)
            {
                arrowPoint = new GameObject("Arrow Hold Point").transform;
                arrowPoint.SetParent(palm, false);
                arrowPoint.localPosition = new Vector3(0, -0.02f, 0.05f);
            }
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.skinnedMotionVectors = false;
            }
        }
        // Enabled after the driver on this GameObject, so this listener runs after its joint update.
        void OnEnable()
        {
            events = driver.handTrackingEvents;
            if (events != null) events.jointsUpdated.AddListener(OnJointsUpdated);
        }
        void OnDisable()
        {
            if (events != null) events.jointsUpdated.RemoveListener(OnJointsUpdated);
            WeaponHandPoses.ClearArrowPoint(Side, arrowPoint);
            weight = 0;
        }
        void Update()
        {
            if (events == null) return;
            if (arrowPoint != null)
            {
                if (events.handIsTracked) WeaponHandPoses.SetArrowPoint(Side, arrowPoint);
                else WeaponHandPoses.ClearArrowPoint(Side, arrowPoint);
            }
            var pose = WeaponHandPoses.Resolve(Side);
            bool simulated = !XRSettings.isDeviceActive;
            float targetWeight = pose.style != HandPoseStyle.None || simulated ? 1 : 0;
            for (int f = 0; f < Fingers.Length; f++)
            {
                float curl = pose.style != HandPoseStyle.None ? pose.Curl(f) : 0;
                SetTargets(Fingers[f], curl);
            }
            float step = 1 - Mathf.Exp(-blendSpeed * Time.deltaTime);
            weight = Mathf.Lerp(weight, targetWeight, step);
            for (int i = 0; i < angles.Length; i++) angles[i] = Mathf.Lerp(angles[i], targets[i], step);
        }
        void SetTargets(XRHandFingerID finger, float curl)
        {
            int start = finger == XRHandFingerID.Thumb ? 0 : 2;
            // Skip the metacarpal (the thumb's metacarpal keeps its tracked opposition) and the tip.
            var first = finger == XRHandFingerID.Thumb ? XRHandJointID.ThumbProximal : finger.GetFrontJointID() + 1;
            int count = finger == XRHandFingerID.Thumb ? 2 : 3;
            for (int i = 0; i < count; i++) targets[(first + i).ToIndex()] = Mathf.Lerp(Relaxed[start + i], Fist[start + i], curl);
        }
        void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            if (weight < 0.001f) return;
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null || targets[i] == 0 && angles[i] == 0) continue;
                // Joint frames have +Z toward the fingertip and +Y on the back of the hand: +X flexes toward the palm.
                joints[i].localRotation = Quaternion.Slerp(joints[i].localRotation, Quaternion.Euler(angles[i], 0, 0), weight);
            }
        }
    }
}
