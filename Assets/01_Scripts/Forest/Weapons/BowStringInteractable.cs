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
            // Grabbed like any object (near or with the ray); the bow decides which hand may draw.
            return base.IsSelectableBy(interactor) && bow != null && bow.CanDraw(interactor);
        }
    }
}
