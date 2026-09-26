using Unity.XR.CoreUtils;
using UnityEngine;

namespace ForestVR
{
    // Walking with the headset (or the simulator's WASD) moves the camera without physics, so it could step into a
    // tree. When the head gets inside a trunk, the whole rig is pushed back out horizontally, like a wall would.
    public sealed class VRBodyBlocker : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] float bodyRadius = .25f;
        XROrigin origin;

        void Awake()
        {
            origin = GetComponentInChildren<XROrigin>();
            if (origin == null) origin = GetComponentInParent<XROrigin>();
        }

        void LateUpdate()
        {
            if (origin == null || origin.Camera == null) return;
            var head = origin.Camera.transform.position;
            var push = Vector3.zero;
            foreach (var trunk in ForestSceneSetup.Trunks)
            {
                if (trunk == null || !trunk.enabled || !trunk.gameObject.activeInHierarchy) continue;
                var t = trunk.transform;
                var scale = t.lossyScale;
                float radius = trunk.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                var center = t.TransformPoint(trunk.center);
                float halfHeight = trunk.height * .5f * Mathf.Abs(scale.y);
                // Only while the body is beside the trunk (from its base to a little above its top).
                if (head.y < center.y - halfHeight - .5f || head.y > center.y + halfHeight + 1.5f) continue;
                var offset = head + push - center; offset.y = 0;
                float distance = offset.magnitude, limit = radius + bodyRadius;
                if (distance >= limit) continue;
                var direction = distance > .0001f ? offset / distance : Vector3.Cross(Vector3.up, t.right).normalized;
                push += direction * (limit - distance);
            }
            if (push.sqrMagnitude > 0) origin.transform.position += push;
        }
    }
}
