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
        [Tooltip("Bullets in the cylinder. Empty, it does not fire and the laser goes out.")]
        [Min(1)] public int capacity = 6;
        [Tooltip("Downward speed of the hand, in m/s, that reloads it: a quick flick down.")]
        [Min(0.5f)] public float reloadFlickSpeed = 2f;
        [Min(0)] public float reloadSeconds = 0.4f;
        public int Rounds { get; private set; }
        WeaponGrip grip;
        AimGuide guide;
        Health guideOwner;
        float readyAt, reloadedAt = -1;
        Vector3 lastHandOffset;
        bool trackingHand;
        InputAction leftTrigger, rightTrigger;
        void Start()
        {
            Rounds = capacity;
            grip = GetComponent<WeaponGrip>(); grip.Grab.activated.AddListener(Fire);
            // Backup for interactors without an Activate input (hand rigs): read the holding controller's trigger.
            leftTrigger = new InputAction("Revolver Left Trigger", InputActionType.Button, "<XRController>{LeftHand}/{TriggerButton}");
            rightTrigger = new InputAction("Revolver Right Trigger", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}");
            leftTrigger.Enable(); rightTrigger.Enable();
        }
        void Update()
        {
            if (grip == null || !grip.IsHeld) { trackingHand = false; return; }
            UpdateReload();
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
            UpdateAmmoCounter();
            if (!aimGuide || grip == null || muzzle == null || !grip.CanUse || Rounds <= 0) { if (guide != null) guide.Hide(); return; }
            if (guide == null) { guide = AimGuide.Create(transform, new Color(1f, .12f, .08f), .004f); guideOwner = null; guide.Ignore(transform); }
            if (guideOwner != grip.Owner) { guideOwner = grip.Owner; guide.Ignore(transform, guideOwner != null ? guideOwner.transform : null); }
            guide.ShowStraight(muzzle.position, muzzle.forward, aimGuideLength);
        }
        void Fire(ActivateEventArgs args) => TryFire();
        public bool TryFire()
        {
            if (grip == null || !grip.CanUse || Time.time < readyAt || projectilePrefab == null || muzzle == null) return false;
            // Paused: the trigger presses the menu buttons, it does not shoot.
            if (Rounds <= 0 || reloadedAt > Time.time || PauseMenu.Paused) return false;
            Rounds--;
            readyAt = Time.time + grip.settings.cooldown;
            var shot = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
            shot.Launch(grip.settings, grip.Owner, transform, muzzle.forward, 1, muzzle.position);
            var audio = GameAudio.Get;
            if (audio != null) GameAudio.PlayAt(audio.gunshot, muzzle.position, 1, Random.Range(.95f, 1.05f), 80);
            return true;
        }
        // ---------- Bullet counter ----------
        // While held, a small label just above the revolver shows the bullets left (● full, ○ spent) and, once empty,
        // how to reload. It faces the eyes and is drawn over everything, so it stays readable while aiming.
        UnityEngine.UI.Text ammoText;
        Canvas ammoCanvas;
        Material ammoOverlay;
        int shownRounds = -1;
        void UpdateAmmoCounter()
        {
            bool show = grip != null && grip.CanUse;
            if (!show) { if (ammoCanvas != null && ammoCanvas.gameObject.activeSelf) ammoCanvas.gameObject.SetActive(false); return; }
            var eyes = Camera.main;
            if (eyes == null) return;
            if (ammoCanvas == null) BuildAmmoCounter();
            if (!ammoCanvas.gameObject.activeSelf) ammoCanvas.gameObject.SetActive(true);
            var position = transform.position + Vector3.up * .11f;
            var away = position - eyes.transform.position;
            if (away.sqrMagnitude > .0001f) ammoCanvas.transform.SetPositionAndRotation(position, Quaternion.LookRotation(away, Vector3.up));
            if (shownRounds == Rounds) return;
            shownRounds = Rounds;
            if (Rounds <= 0) { ammoText.text = "SIN BALAS\n<size=30>RECARGA: baja la mano rápido ↓</size>"; ammoText.color = new Color(1f, .25f, .15f); return; }
            ammoText.text = new string('●', Rounds) + new string('○', capacity - Rounds) + $"\n<size=34>{Rounds} / {capacity}</size>";
            ammoText.color = Rounds <= 2 ? new Color(1f, .6f, .2f) : new Color(.95f, .9f, .8f);
        }
        void BuildAmmoCounter()
        {
            var go = new GameObject("Revolver Ammo");
            ammoCanvas = go.AddComponent<Canvas>();
            ammoCanvas.renderMode = RenderMode.WorldSpace;
            ammoCanvas.sortingOrder = 60;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(420, 130);
            // 0.1 m wide in the world.
            rect.localScale = Vector3.one * (.1f / 420);
            ammoOverlay = new Material(Canvas.GetDefaultCanvasMaterial());
            ammoOverlay.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            ammoText = new GameObject("Text", typeof(RectTransform)).AddComponent<UnityEngine.UI.Text>();
            ammoText.rectTransform.SetParent(rect, false);
            ammoText.rectTransform.sizeDelta = rect.sizeDelta;
            ammoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ammoText.fontSize = 48; ammoText.fontStyle = FontStyle.Bold; ammoText.supportRichText = true;
            ammoText.alignment = TextAnchor.MiddleCenter; ammoText.lineSpacing = .9f;
            ammoText.horizontalOverflow = HorizontalWrapMode.Overflow; ammoText.verticalOverflow = VerticalWrapMode.Overflow;
            ammoText.material = ammoOverlay; ammoText.raycastTarget = false;
            var shadow = ammoText.gameObject.AddComponent<UnityEngine.UI.Outline>();
            shadow.effectColor = new Color(0, 0, 0, .9f); shadow.effectDistance = new Vector2(2, -2);
            shownRounds = -1;
        }
        // A quick flick of the holding hand downward refills the cylinder, ready after a short moment. The speed is
        // measured relative to the player's rig, so walking, turning or falling does not count as a flick.
        void UpdateReload()
        {
            var rig = grip.Owner != null ? grip.Owner.transform.position : Vector3.zero;
            var offset = transform.position - rig;
            if (trackingHand && Time.deltaTime > 0 && Rounds < capacity && reloadedAt <= Time.time)
            {
                float downSpeed = -(offset.y - lastHandOffset.y) / Time.deltaTime;
                if (downSpeed >= reloadFlickSpeed)
                {
                    Rounds = capacity;
                    reloadedAt = Time.time + reloadSeconds;
                }
            }
            lastHandOffset = offset;
            trackingHand = true;
        }
        void OnDestroy()
        {
            if (grip != null && grip.Grab != null) grip.Grab.activated.RemoveListener(Fire);
            leftTrigger?.Dispose(); rightTrigger?.Dispose();
            if (ammoCanvas != null) Destroy(ammoCanvas.gameObject);
            if (ammoOverlay != null) Destroy(ammoOverlay);
        }
    }
}
