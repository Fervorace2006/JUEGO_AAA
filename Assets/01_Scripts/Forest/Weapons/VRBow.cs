using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace ForestVR
{
    [RequireComponent(typeof(WeaponGrip))]
    public sealed class VRBow : MonoBehaviour
    {
        public Transform upperTip, lowerTip, restingNock;
        public BowStringInteractable stringGrip;
        public LineRenderer stringLine;
        public WeaponProjectile arrowPrefab;
        public float DrawDistance { get; private set; }
        WeaponGrip grip;
        IXRSelectInteractor drawingHand;
        WeaponProjectile preview;
        Vector3 nockPosition;
        float readyAt;
        bool cancelRelease;
        void Start()
        {
            grip = GetComponent<WeaponGrip>();
            stringGrip.selectEntered.AddListener(BeginDraw); stringGrip.selectExited.AddListener(Release);
            grip.Grab.selectExited.AddListener(BowReleased);
        }
        public bool CanDraw(IXRSelectInteractor hand)
        {
            return grip != null && grip.CanUse && Time.time >= readyAt && !grip.Grab.interactorsSelecting.Contains(hand);
        }
        void BeginDraw(SelectEnterEventArgs args)
        {
            drawingHand = args.interactorObject; cancelRelease = false;
            if (preview == null) preview = Instantiate(arrowPrefab);
        }
        void LateUpdate()
        {
            if (grip == null) return;
            nockPosition = restingNock.position;
            if (drawingHand != null)
            {
                if (!grip.CanUse) { CancelDraw(); return; }
                // Use the controller itself: a near/far attach point can stay on the selected object.
                Vector3 handPosition = drawingHand.transform.position;
                float pull = Vector3.Dot(restingNock.position - handPosition, transform.forward);
                DrawDistance = Mathf.Clamp(pull, 0, grip.settings.maximumDraw);
                nockPosition -= transform.forward * DrawDistance;
                if (Vector3.Distance(handPosition, nockPosition) > 0.45f) { CancelDraw(); return; }
            }
            else DrawDistance = 0;
            stringGrip.transform.position = nockPosition;
            stringLine.SetPosition(0, upperTip.position); stringLine.SetPosition(1, nockPosition); stringLine.SetPosition(2, lowerTip.position);
            if (preview != null) preview.transform.SetPositionAndRotation(nockPosition, Quaternion.LookRotation(transform.forward, transform.up));
        }
        void Release(SelectExitEventArgs args)
        {
            if (!cancelRelease && !args.isCanceled && grip.CanUse && DrawDistance >= grip.settings.minimumDraw && Time.time >= readyAt)
            {
                readyAt = Time.time + grip.settings.cooldown;
                var arrow = Instantiate(arrowPrefab, nockPosition, Quaternion.LookRotation(transform.forward, transform.up));
                arrow.Launch(grip.settings, grip.Owner, transform, transform.forward,
                    Mathf.Clamp01(DrawDistance / grip.settings.maximumDraw), nockPosition);
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
        void ClearDraw()
        {
            drawingHand = null; DrawDistance = 0;
            if (preview != null) Destroy(preview.gameObject);
        }
        void OnDisable() { if (grip != null) CancelDraw(); }
        void OnDestroy()
        {
            if (stringGrip != null) { stringGrip.selectEntered.RemoveListener(BeginDraw); stringGrip.selectExited.RemoveListener(Release); }
            if (grip != null && grip.Grab != null) grip.Grab.selectExited.RemoveListener(BowReleased);
            if (preview != null) Destroy(preview.gameObject);
        }
    }
}
