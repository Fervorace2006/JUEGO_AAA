using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
namespace ForestVR
{
    // Prepares the player to grab weapons placed directly in the scene.
    public sealed class PlayerWeaponLoadout : MonoBehaviour
    {
        void Start()
        {
            int weaponLayer = LayerMask.NameToLayer("Weapons");
            if (weaponLayer >= 0)
            {
                foreach (var caster in GetComponentsInChildren<SphereInteractionCaster>(true)) caster.physicsLayerMask |= 1 << weaponLayer;
                foreach (var caster in GetComponentsInChildren<CurveInteractionCaster>(true)) caster.raycastMask |= 1 << weaponLayer;
            }
            foreach (var skeleton in GetComponentsInChildren<XRHandSkeletonDriver>(true))
                if (skeleton.GetComponent<HandGripPose>() == null) skeleton.gameObject.AddComponent<HandGripPose>();
            var health = GetComponent<Health>();
            if (health == null) health = gameObject.AddComponent<Health>();
            var camera = GetComponentInChildren<Camera>(true);
            if (camera == null) { Debug.LogError("PlayerWeaponLoadout necesita la camara del jugador.", this); return; }
            var status = GetComponent<PlayerCombatStatus>();
            if (status == null) status = gameObject.AddComponent<PlayerCombatStatus>();
            status.Initialize(health, camera.transform);
        }
    }
}
