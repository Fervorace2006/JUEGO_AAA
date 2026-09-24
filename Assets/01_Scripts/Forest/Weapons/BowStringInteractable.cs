using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace ForestVR
{
    public sealed class BowStringInteractable : XRBaseInteractable
    {
        public VRBow bow;
        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (!base.IsSelectableBy(interactor) || bow == null || !bow.CanDraw(interactor)) return false;
            // Keep selection while pulling, but require reaching the string to start.
            return isSelected || Vector3.Distance(interactor.transform.position, transform.position) < 0.22f;
        }
    }
}
