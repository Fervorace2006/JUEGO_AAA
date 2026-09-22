using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace JuegoAAA.VR
{
    /// <summary>Reports virtual ground depth; XRI GravityProvider owns player movement.</summary>
    [RequireComponent(typeof(XROrigin), typeof(CharacterController))]
    public sealed class VRGroundProbe : MonoBehaviour
    {
        [SerializeField] bool configurePlayer;
        [SerializeField] Transform environmentRoot;
        [SerializeField] Material handMaterial;
        [SerializeField] GameObject leftHandModel;
        [SerializeField] GameObject rightHandModel;
        [SerializeField] bool useControllerHands;
        [SerializeField] LayerMask groundLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.1f)] float probeDepth = 20f;
        [SerializeField, Min(0f)] float groundedTolerance = 0.12f;

        public bool HasGround { get; private set; }
        public bool IsGrounded { get; private set; }
        public float GroundDistance { get; private set; } = float.PositiveInfinity;
        public float GroundSlope { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;

        XROrigin origin;
        CharacterController body;
        Vector3 safePosition;
        bool hasSafePosition;
        Collider floor;

        void Start()
        {
            if (!configurePlayer) return;
            if (useControllerHands)
            {
                var modality = GetComponent<XRInputModalityManager>();
                if (modality != null)
                {
                    AddHand(modality.leftController, true);
                    AddHand(modality.rightController, false);
                }
            }
            floor = environmentRoot != null ? environmentRoot.GetComponent<Collider>() : null;
            Physics.SyncTransforms();
            if (floor == null)
            {
                Debug.LogWarning("Assign FloorWithLake to Environment Root to place the VR player on the ground.", this);
                return;
            }
            // Search outwards from the requested spawn, rejecting trees, rocks and steep slopes.
            for (int ring = 0; ring <= 20; ring++)
            {
                int count = ring == 0 ? 1 : 24;
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    var point = transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ring;
                    point.y = floor.bounds.max.y + 2f;
                    if (!floor.Raycast(new Ray(point, Vector3.down), out var hit, floor.bounds.size.y + 4f)
                        || Vector3.Angle(hit.normal, Vector3.up) > body.slopeLimit)
                        continue;
                    var feet = hit.point + Vector3.up * 0.04f;
                    float height = Mathf.Max(1.65f, origin.CameraInOriginSpaceHeight);
                    if (Physics.CheckCapsule(feet + Vector3.up * (body.radius + 0.03f),
                        feet + Vector3.up * (height - body.radius), body.radius, groundLayers,
                        QueryTriggerInteraction.Ignore)) continue;
                    PlaceAt(feet);
                    safePosition = feet;
                    hasSafePosition = true;
                    return;
                }
            }
            Debug.LogWarning("No clear walkable spawn found on FloorWithLake. Check its collider and spawn position.", this);
        }

        void AddHand(GameObject controller, bool left)
        {
            if (controller == null) return;
            foreach (var renderer in controller.GetComponentsInChildren<Renderer>(true))
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    renderer.enabled = false;
            var hand = new GameObject(left ? "Left Controller Hand" : "Right Controller Hand");
            hand.SetActive(false);
            hand.transform.SetParent(controller.transform, false);
            hand.AddComponent<VRControllerHand>().Configure(left, handMaterial, left ? leftHandModel : rightHandModel);
            hand.SetActive(true);
        }

        void PlaceAt(Vector3 feet)
        {
            var headOffset = origin.Camera != null ? origin.Camera.transform.position - transform.position : Vector3.zero;
            headOffset.y = 0;
            body.enabled = false;
            transform.position = feet - headOffset;
            body.enabled = true;
            Physics.SyncTransforms();
        }

        void Awake()
        {
            origin = GetComponent<XROrigin>();
            body = GetComponent<CharacterController>();
            if (configurePlayer)
            {
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                origin.CameraYOffset = 1.65f;
                body.radius = 0.2f;
                body.stepOffset = 0.25f;
                body.skinWidth = 0.02f;
                body.slopeLimit = 45f;
                foreach (var gravity in GetComponentsInChildren<GravityProvider>(true))
                {
                    gravity.useGravity = true;
                    gravity.sphereCastRadius = 0.18f;
                    gravity.sphereCastDistanceBuffer = -0.16f;
                    gravity.sphereCastLayerMask = groundLayers;
                }
            }
        }

        void LateUpdate()
        {
            if (origin.Camera == null)
                return;

            // The rig uses Ignore Raycast, so neither its body nor its hands are ground.
            var up = transform.up;
            var head = origin.Camera.transform.position;
            var feet = head - up * Vector3.Dot(head - transform.position, up);
            const float lift = 0.2f;
            HasGround = Physics.Raycast(feet + up * lift, -up, out var hit,
                probeDepth + lift, groundLayers, QueryTriggerInteraction.Ignore);
            GroundDistance = HasGround ? Mathf.Max(0f, hit.distance - lift) : float.PositiveInfinity;
            GroundNormal = HasGround ? hit.normal : up;
            GroundSlope = HasGround ? Vector3.Angle(up, hit.normal) : 0f;
            IsGrounded = body.isGrounded || (HasGround && GroundDistance <= groundedTolerance
                && GroundSlope <= body.slopeLimit);
            if (configurePlayer && hasSafePosition)
            {
                var floorRayStart = new Vector3(feet.x, floor.bounds.max.y + 2f, feet.z);
                bool overFloor = floor.Raycast(new Ray(floorRayStart, Vector3.down),
                    out var floorHit, floor.bounds.size.y + 4f);
                // Keep locomotion inside the playable ground and recover if a teleport or
                // tracking change places the player below the terrain.
                if (!overFloor || feet.y < floorHit.point.y - 0.15f
                    || transform.position.y < safePosition.y - 3f)
                {
                    PlaceAt(safePosition);
                }
                else if (IsGrounded && HasGround && GroundSlope <= body.slopeLimit)
                    safePosition = feet;
            }
        }

        void OnDrawGizmosSelected()
        {
            var xr = GetComponent<XROrigin>();
            if (xr == null || xr.Camera == null)
                return;
            var head = xr.Camera.transform.position;
            var feet = head - transform.up * Vector3.Dot(head - transform.position, transform.up);
            Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawLine(feet, feet - transform.up * probeDepth);
        }
    }
}
