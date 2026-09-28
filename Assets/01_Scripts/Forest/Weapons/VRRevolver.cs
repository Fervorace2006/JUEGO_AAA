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
        [Tooltip("Red laser from the muzzle with a dot where the bullet will hit, while the revolver is held.")]
        public bool aimGuide = true;
        [Min(1)] public float aimGuideLength = 60;
        WeaponGrip grip;
        AimGuide guide;
        Health guideOwner;
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
        // After the grab has moved the revolver this frame. Bullets fly straight, so the laser is their exact path.
        void LateUpdate()
        {
            if (!aimGuide || grip == null || muzzle == null || !grip.CanUse) { if (guide != null) guide.Hide(); return; }
            if (guide == null) { guide = AimGuide.Create(transform, new Color(1f, .12f, .08f), .004f); guideOwner = null; guide.Ignore(transform); }
            if (guideOwner != grip.Owner) { guideOwner = grip.Owner; guide.Ignore(transform, guideOwner != null ? guideOwner.transform : null); }
            guide.ShowStraight(muzzle.position, muzzle.forward, aimGuideLength);
        }
        void Fire(ActivateEventArgs args) => TryFire();
        public bool TryFire()
        {
            if (grip == null || !grip.CanUse || Time.time < readyAt || projectilePrefab == null || muzzle == null) return false;
            readyAt = Time.time + grip.settings.cooldown;
            var shot = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
            shot.Launch(grip.settings, grip.Owner, transform, muzzle.forward, 1, muzzle.position);
            var audio = GameAudio.Get;
            if (audio != null) GameAudio.PlayAt(audio.gunshot, muzzle.position, 1, Random.Range(.95f, 1.05f), 80);
            return true;
        }
        void OnDestroy()
        {
            if (grip != null && grip.Grab != null) grip.Grab.activated.RemoveListener(Fire);
            leftTrigger?.Dispose(); rightTrigger?.Dispose();
        }
    }
}
