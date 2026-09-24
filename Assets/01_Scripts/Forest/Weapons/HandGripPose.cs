using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ForestVR
{
    // Bends the XR Hands skeleton right after XRHandSkeletonDriver writes the tracked joints:
    // closed around a held weapon, and relaxed in the XR Interaction Simulator, whose captured hands point with the index.
    // With real hand tracking and nothing held, the tracked fingers are left untouched.
    [RequireComponent(typeof(XRHandSkeletonDriver))]
    public sealed class HandGripPose : MonoBehaviour
    {
        static readonly XRHandFingerID[] Fingers = { XRHandFingerID.Thumb, XRHandFingerID.Index, XRHandFingerID.Middle, XRHandFingerID.Ring, XRHandFingerID.Little };
        // Flexion in degrees per finger: proximal, intermediate, distal (thumb: proximal, distal).
        static readonly float[] Relaxed = { 8, 8, 12, 18, 10 };
        static readonly float[] Fist = { 30, 35, 70, 90, 55 };
        static readonly float[] TriggerIndex = { 30, 45, 25 };

        [SerializeField, Min(1)] float blendSpeed = 12;
        XRHandSkeletonDriver driver;
        XRHandTrackingEvents events;
        readonly Transform[] joints = new Transform[XRHandJointID.EndMarker.ToIndex()];
        readonly float[] angles = new float[XRHandJointID.EndMarker.ToIndex()];
        readonly float[] targets = new float[XRHandJointID.EndMarker.ToIndex()];
        float weight;

        void Awake()
        {
            driver = GetComponent<XRHandSkeletonDriver>();
            foreach (var reference in driver.jointTransformReferences)
            {
                int index = reference.xrHandJointID.ToIndex();
                if (index >= 0 && index < joints.Length) joints[index] = reference.jointTransform;
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
            weight = 0;
        }
        void Update()
        {
            if (events == null) return;
            var hand = events.handedness == Handedness.Left ? InteractorHandedness.Left : InteractorHandedness.Right;
            var weapon = WeaponGrip.HeldIn(hand);
            bool simulated = !XRSettings.isDeviceActive;
            float targetWeight = weapon != null || simulated ? 1 : 0;
            foreach (var finger in Fingers) SetTargets(finger, weapon != null ? Fist : Relaxed);
            if (weapon != null && weapon.GetComponent<VRRevolver>() != null) SetTargets(XRHandFingerID.Index, TriggerIndex, 0);
            float step = 1 - Mathf.Exp(-blendSpeed * Time.deltaTime);
            weight = Mathf.Lerp(weight, targetWeight, step);
            for (int i = 0; i < angles.Length; i++) angles[i] = Mathf.Lerp(angles[i], targets[i], step);
        }
        void SetTargets(XRHandFingerID finger, float[] pose, int start = -1)
        {
            if (start < 0) start = finger == XRHandFingerID.Thumb ? 0 : 2;
            var front = finger.GetFrontJointID();
            // Skip the metacarpal (the thumb's metacarpal keeps its tracked opposition) and the tip.
            var first = finger == XRHandFingerID.Thumb ? XRHandJointID.ThumbProximal : front + 1;
            int count = finger == XRHandFingerID.Thumb ? 2 : 3;
            for (int i = 0; i < count; i++) targets[(first + i).ToIndex()] = pose[start + i];
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
