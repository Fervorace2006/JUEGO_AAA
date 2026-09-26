using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ForestVR
{
    public enum HandPoseStyle { None, Pistol, Axe, BowRiser, BowString, ArrowHold }

    // What a hand should do this frame. When snapped, the hand's grip frame is placed on the anchor:
    // anchor forward = direction the straight fingers point, anchor up = index side of the fist (the handle axis).
    public struct HandPoseRequest
    {
        public HandPoseStyle style;
        public bool snap;
        public Pose anchor;
        public float Curl(int finger) => WeaponHandPoses.Curl(style, finger);
        public bool IndexOnTrigger => style == HandPoseStyle.Pistol;
    }

    // Grip poses for every weapon and the hand that needs them, shared by controller hands and tracked hands.
    public static class WeaponHandPoses
    {
        // Finger curl 0 (open) .. 1 (fist), per style: thumb, index, middle, ring, little.
        static readonly float[][] Curls =
        {
            new[] { 0f, 0f, 0f, 0f, 0f },            // None: the hand follows the controller buttons
            new[] { .75f, .35f, .95f, .95f, .95f },  // Pistol: index resting on the trigger
            new[] { .9f, 1f, 1f, 1f, 1f },           // Axe: full power grip on the handle
            new[] { .8f, .85f, .95f, .95f, .95f },   // BowRiser: fist around the riser
            new[] { .55f, .55f, .6f, .85f, .9f },    // BowString: index and middle hooked on the string
            new[] { .6f, .5f, .7f, .9f, .9f },       // ArrowHold: arrow nock pinched by thumb and index
        };
        public const float TriggerPulledIndex = .62f;

        // Right-hand grip anchor in weapon space: position and yaw (palm turned toward the back of the handle).
        // The left hand uses the mirror image across the weapon's YZ plane.
        static readonly Vector3[] AnchorPositions = { Vector3.zero, new Vector3(0, -.01f, 0), new Vector3(0, -.03f, 0), Vector3.zero, new Vector3(0, -.012f, -.02f), Vector3.zero };
        static readonly float[] AnchorYaw = { 0, 15, 0, 20, 0, 0 };

        static readonly Transform[] arrowPoints = new Transform[3];

        public static float Curl(HandPoseStyle style, int finger) => Curls[(int)style][finger];

        public static InteractorHandedness Other(InteractorHandedness hand) =>
            hand == InteractorHandedness.Left ? InteractorHandedness.Right
            : hand == InteractorHandedness.Right ? InteractorHandedness.Left : InteractorHandedness.None;

        public static HandPoseRequest Resolve(InteractorHandedness hand)
        {
            var request = new HandPoseRequest();
            if (hand == InteractorHandedness.None) return request;
            var grip = WeaponGrip.HeldIn(hand);
            if (grip != null)
            {
                request.style = grip.Style;
                request.snap = true;
                request.anchor = Anchor(new Pose(grip.transform.position, grip.transform.rotation), grip.transform.lossyScale.x, grip.Style, hand, grip.handPositionOffset, grip.handYawOffset);
                return request;
            }
            var other = WeaponGrip.HeldIn(Other(hand));
            var bow = other != null ? other.Bow : null;
            if (bow == null) return request;
            if (bow.IsDrawnBy(hand))
            {
                request.style = HandPoseStyle.BowString;
                request.snap = true;
                request.anchor = Anchor(bow.NockFrame, 1, HandPoseStyle.BowString, hand, Vector3.zero, 0);
            }
            else if (bow.HasArrowInHand) request.style = HandPoseStyle.ArrowHold;
            return request;
        }

        // Offsets are in the weapon's unscaled space: the table weapons are scaled down in the scene.
        static Pose Anchor(Pose frame, float scale, HandPoseStyle style, InteractorHandedness hand, Vector3 extraPosition, float extraYaw)
        {
            var position = AnchorPositions[(int)style] + extraPosition;
            float yaw = AnchorYaw[(int)style] + extraYaw;
            if (hand == InteractorHandedness.Left) { position.x = -position.x; yaw = -yaw; }
            return new Pose(frame.position + frame.rotation * (position * scale), frame.rotation * Quaternion.Euler(0, yaw, 0));
        }

        // Where a hand pinches an arrow: position at the nock, forward along the shaft.
        public static void SetArrowPoint(InteractorHandedness hand, Transform point) => arrowPoints[(int)hand] = point;
        public static void ClearArrowPoint(InteractorHandedness hand, Transform point)
        {
            if (arrowPoints[(int)hand] == point) arrowPoints[(int)hand] = null;
        }
        // Palm joint of a hand-tracked (XR Hands) hand: +Z toward the fingers, +Y on the back of the hand.
        // Only tracked hands register it; controller hands snap themselves onto the weapon instead,
        // so a weapon following them would chase its own hand.
        static readonly Transform[] palms = new Transform[3];
        public static void SetPalm(InteractorHandedness hand, Transform palm) => palms[(int)hand] = palm;
        public static void ClearPalm(InteractorHandedness hand, Transform palm)
        {
            if (palms[(int)hand] == palm) palms[(int)hand] = null;
        }
        public static bool TryGetPalm(InteractorHandedness hand, out Transform palm)
        {
            palm = hand != InteractorHandedness.None ? palms[(int)hand] : null;
            return palm != null && palm.gameObject.activeInHierarchy;
        }
        public static bool TryGetArrowPoint(InteractorHandedness hand, out Transform point)
        {
            point = arrowPoints[(int)hand];
            return point != null && point.gameObject.activeInHierarchy;
        }
    }
}
