using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace ForestVR
{
    // Bow built after the "Bow and Arrow" reference project (Fist Full of Shrimp): the string is pulled from
    // restingNock (reference "Start") toward pullEnd (reference "End"), an arrow waits on the notch in the middle
    // of the bow while it is held, and releasing the string shoots that same arrow with strength = pull amount.
    [RequireComponent(typeof(WeaponGrip))]
    public sealed class VRBow : MonoBehaviour
    {
        public Transform upperTip, lowerTip, restingNock, pullEnd;
        public BowStringInteractable stringGrip;
        public LineRenderer stringLine;
        public WeaponProjectile arrowPrefab;
        [Tooltip("Minimum pull (0 = rest, 1 = full draw) for a release to shoot.")]
        [Range(0, 1)] public float minimumPull = 0.2f;
        // 0 at rest, 1 at full draw.
        public float PullAmount { get; private set; }
        // Pull in meters (world space).
        public float DrawDistance => PullAmount * Vector3.Distance(restingNock.position, pullEnd.position);
        WeaponGrip grip;
        IXRSelectInteractor drawingHand;
        WeaponProjectile nockedArrow;
        Vector3 nockPosition;
        float readyAt;
        bool cancelRelease;
        // Hand offset from the nock when the string was grabbed: pulling is relative, like dragging a grabbed object.
        Vector3 grabOffset;
        void Start()
        {
            grip = GetComponent<WeaponGrip>();
            stringGrip.selectEntered.AddListener(BeginDraw); stringGrip.selectExited.AddListener(Release);
            grip.Grab.selectExited.AddListener(BowReleased);
        }
        Vector3 ShotDirection => (restingNock.position - pullEnd.position).normalized;
        // Nock frame: forward along the shot, up along the riser.
        public Pose NockFrame => new Pose(nockPosition, Quaternion.LookRotation(ShotDirection, transform.up));
        public bool IsDrawnBy(InteractorHandedness hand) => drawingHand != null && drawingHand.handedness == hand;
        // Arrows are never carried in the hand: the arrow waits on the notch of the bow.
        public bool HasArrowInHand => false;
        public bool CanDraw(IXRSelectInteractor hand)
        {
            return grip != null && grip.CanUse && !grip.Grab.interactorsSelecting.Contains(hand)
                && (hand.handedness == InteractorHandedness.None || hand.handedness != grip.HeldBy);
        }
        void BeginDraw(SelectEnterEventArgs args)
        {
            drawingHand = args.interactorObject; cancelRelease = false;
            grabOffset = drawingHand.transform.position - restingNock.position;
        }
        Vector3 PullPoint => drawingHand.transform.position - grabOffset;
        void LateUpdate()
        {
            if (grip == null) return;
            PullAmount = 0;
            if (drawingHand != null)
            {
                if (!grip.CanUse) { CancelDraw(); return; }
                // Use the controller itself: a near/far attach point can stay on the selected object.
                PullAmount = Pull(PullPoint);
            }
            nockPosition = Vector3.Lerp(restingNock.position, pullEnd.position, PullAmount);
            // Pulled far beyond full draw or yanked sideways: let go of the string.
            if (drawingHand != null && Vector3.Distance(PullPoint, nockPosition) > 0.8f) { CancelDraw(); return; }
            // The string's middle point follows the hand, like the reference LineRenderer.
            stringGrip.transform.position = nockPosition;
            stringLine.SetPosition(0, upperTip.position); stringLine.SetPosition(1, nockPosition); stringLine.SetPosition(2, lowerTip.position);
            // Arrow spawner: while the bow is in the hand and ready, an arrow sits on the notch and moves with the string.
            if (grip.CanUse && Time.time >= readyAt && arrowPrefab != null)
            {
                if (nockedArrow == null) nockedArrow = Instantiate(arrowPrefab);
                nockedArrow.transform.SetPositionAndRotation(nockPosition, NockFrame.rotation);
            }
            else RemoveArrow();
        }
        // Reference PullInteraction: projection of the hand on the Start -> End segment, 0..1.
        float Pull(Vector3 handPosition)
        {
            var pullDirection = pullEnd.position - restingNock.position;
            float length = pullDirection.sqrMagnitude;
            if (length < 0.000001f) return 0;
            return Mathf.Clamp01(Vector3.Dot(handPosition - restingNock.position, pullDirection) / length);
        }
        void Release(SelectExitEventArgs args)
        {
            // Selection can end before LateUpdate in the same frame as the hand moves.
            if (drawingHand != null && grip != null) PullAmount = Pull(PullPoint);
            if (!cancelRelease && !args.isCanceled && grip.CanUse && PullAmount >= minimumPull && Time.time >= readyAt && nockedArrow != null)
            {
                readyAt = Time.time + grip.settings.cooldown;
                var start = Vector3.Lerp(restingNock.position, pullEnd.position, PullAmount);
                var arrow = nockedArrow; nockedArrow = null;
                arrow.transform.SetPositionAndRotation(start, Quaternion.LookRotation(ShotDirection, transform.up));
                arrow.Launch(grip.settings, grip.Owner, transform, ShotDirection, PullAmount, start);
            }
            ClearDraw();
        }
        void BowReleased(SelectExitEventArgs args) => CancelDraw();
        void CancelDraw()
        {
            cancelRelease = true;
            if (drawingHand != null && stringGrip.interactionManager != null)
                stringGrip.interactionManager.SelectExit(drawingHand, stringGrip);
            ClearDraw();
        }
        void ClearDraw() { drawingHand = null; PullAmount = 0; }
        void RemoveArrow()
        {
            if (nockedArrow != null) Destroy(nockedArrow.gameObject);
            nockedArrow = null;
        }
        void OnDisable() { if (grip != null) CancelDraw(); RemoveArrow(); }
        void OnDestroy()
        {
            if (stringGrip != null) { stringGrip.selectEntered.RemoveListener(BeginDraw); stringGrip.selectExited.RemoveListener(Release); }
            if (grip != null && grip.Grab != null) grip.Grab.selectExited.RemoveListener(BowReleased);
            RemoveArrow();
        }
    }
}
