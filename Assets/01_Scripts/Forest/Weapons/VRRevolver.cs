using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace ForestVR
{
    [RequireComponent(typeof(WeaponGrip))]
    public sealed class VRRevolver : MonoBehaviour
    {
        public Transform muzzle;
        public WeaponProjectile projectilePrefab;
        WeaponGrip grip;
        float readyAt;
        InputAction leftTrigger, rightTrigger;
        void Start()
        {
            grip = GetComponent<WeaponGrip>(); grip.Grab.activated.AddListener(Fire);
            // Backup for interactors without an Activate input (hand rigs): read the holding controller's trigger.
            leftTrigger = new InputAction("Revolver Left Trigger", InputActionType.Button, "<XRController>{LeftHand}/{TriggerButton}");
            rightTrigger = new InputAction("Revolver Right Trigger", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}");
            leftTrigger.Enable(); rightTrigger.Enable();
        }
        void Update()
        {
            if (grip == null || !grip.IsHeld) return;
            var trigger = grip.HeldBy == InteractorHandedness.Left ? leftTrigger : rightTrigger;
            bool pressed = trigger.WasPressedThisFrame();
#if UNITY_EDITOR
            pressed |= Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#endif
            // The cooldown discards the duplicate when Activate fired for the same press.
            if (pressed) TryFire();
        }
        void Fire(ActivateEventArgs args) => TryFire();
        public bool TryFire()
        {
            if (grip == null || !grip.CanUse || Time.time < readyAt || projectilePrefab == null || muzzle == null) return false;
            readyAt = Time.time + grip.settings.cooldown;
            var shot = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
            shot.Launch(grip.settings, grip.Owner, transform, muzzle.forward, 1, muzzle.position);
            return true;
        }
        void OnDestroy()
        {
            if (grip != null && grip.Grab != null) grip.Grab.activated.RemoveListener(Fire);
            leftTrigger?.Dispose(); rightTrigger?.Dispose();
        }
    }
}
