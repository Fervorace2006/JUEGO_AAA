using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ForestVR
{
    // Pull the handle with Grip. The drawer only slides along its parent's local X axis.
    public sealed class VRDrawer : MonoBehaviour
    {
        public XRSimpleInteractable handle;
        public float closedX = .141f;
        public float openX = .664f;
        IXRSelectInteractor hand;
        float grabbedAtX, drawerAtGrab;

        void OnEnable()
        {
            if (handle != null)
            {
                handle.selectEntered.AddListener(Grab);
                handle.selectExited.AddListener(Release);
            }
        }

        void OnDisable()
        {
            if (handle != null)
            {
                handle.selectEntered.RemoveListener(Grab);
                handle.selectExited.RemoveListener(Release);
            }
            hand = null;
        }

        void Grab(SelectEnterEventArgs args)
        {
            hand = args.interactorObject;
            grabbedAtX = HandX();
            drawerAtGrab = transform.localPosition.x;
        }

        void Release(SelectExitEventArgs args)
        {
            if (hand == args.interactorObject) hand = null;
        }

        void LateUpdate()
        {
            if (hand == null || transform.parent == null) return;
            var p = transform.localPosition;
            p.x = Mathf.Clamp(drawerAtGrab + HandX() - grabbedAtX, closedX, openX);
            transform.localPosition = p;
        }

        float HandX() => transform.parent.InverseTransformPoint(hand.transform.position).x;
    }
}
