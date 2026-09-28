using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace ForestVR
{
    // Sprinting with stamina. Click the left thumbstick while moving to run (holding a stick pressed while pushing it is
    // tiring, so the click toggles); it stops when you stop, when stamina runs out or with another click.
    // Running spends stamina; it comes back after a short rest. Empty stamina leaves you exhausted: no running until
    // it has recovered part of the bar. In the editor, Left Shift also runs while held.
    [DisallowMultipleComponent]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [Min(1)] public float maximum = 100;
        [Tooltip("Speed multiplier while running.")]
        [Min(1)] public float sprintMultiplier = 1.8f;
        [Tooltip("Stamina spent per second of running.")]
        [Min(0)] public float drainPerSecond = 18;
        [Tooltip("Stamina recovered per second when not running.")]
        [Min(0)] public float regenPerSecond = 14;
        [Tooltip("Seconds without running before stamina starts to recover.")]
        [Min(0)] public float regenDelay = 1;
        [Tooltip("After running out, stamina must reach this fraction before you can run again.")]
        [Range(0, 1)] public float recoverFraction = .3f;

        public float Current { get; private set; }
        public float Fraction => Current / maximum;
        public bool IsSprinting { get; private set; }
        public bool IsExhausted { get; private set; }
        public event System.Action Exhausted;

        ContinuousMoveProvider move;
        Health health;
        float baseSpeed, lastRunAt = float.NegativeInfinity;
        bool sprintToggled;
        InputAction click;

        void Awake()
        {
            Current = maximum;
            health = GetComponent<Health>();
            click = new InputAction("Sprint", InputActionType.Button);
            click.AddBinding("<XRController>{LeftHand}/{Primary2DAxisClick}");
            click.performed += _ => sprintToggled = !sprintToggled;
            click.Enable();
        }

        void Start()
        {
            move = GetComponentInChildren<ContinuousMoveProvider>(true);
            if (move != null) baseSpeed = move.moveSpeed;
            else Debug.LogWarning("PlayerStamina: el jugador no tiene ContinuousMoveProvider; no se puede correr.", this);
        }

        void OnEnable() => click?.Enable();
        void OnDisable() { click?.Disable(); SetSprint(false); }
        void OnDestroy() => click?.Dispose();

        void Update()
        {
            if (move == null) return;
            bool moving = move.leftHandMoveInput.ReadValue().sqrMagnitude > .04f || move.rightHandMoveInput.ReadValue().sqrMagnitude > .04f;
            bool wants = sprintToggled;
#if UNITY_EDITOR
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.leftShiftKey.isPressed) wants = true;
#endif
            // Standing still ends the run: the next time you move you walk until you click again.
            if (!moving) sprintToggled = false;
            bool dead = health != null && health.IsDead;
            bool running = wants && moving && !IsExhausted && !dead && Current > 0;
            if (running)
            {
                Current = Mathf.Max(0, Current - drainPerSecond * Time.deltaTime);
                lastRunAt = Time.time;
                if (Current <= 0) { IsExhausted = true; sprintToggled = false; running = false; Exhausted?.Invoke(); }
            }
            else if (Time.time - lastRunAt >= regenDelay)
            {
                Current = Mathf.Min(maximum, Current + regenPerSecond * Time.deltaTime);
                if (IsExhausted && Current >= maximum * recoverFraction) IsExhausted = false;
            }
            SetSprint(running);
        }

        void SetSprint(bool running)
        {
            IsSprinting = running;
            if (move != null && baseSpeed > 0) move.moveSpeed = baseSpeed * (running ? sprintMultiplier : 1);
        }
    }
}
