using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestVR
{
    [DisallowMultipleComponent]
    public sealed class PlayerCombatStatus : MonoBehaviour
    {
        Health health;
        VRWorldLabel hint;
        PlayerHud bar;
        PlayerStamina stamina;
        BloodScreen blood;
        InputAction recover;
        public void Initialize(Health playerHealth, Transform head)
        {
            if (health != null) return;
            health = playerHealth;
            hint = VRWorldLabel.Create(head, "Player Health", new Vector3(0, .18f, .85f));
            // Running with stamina, and the health and stamina bars low in the middle of the view.
            stamina = GetComponent<PlayerStamina>();
            if (stamina == null) stamina = gameObject.AddComponent<PlayerStamina>();
            bar = PlayerHud.Create(head, health, stamina);
            blood = BloodScreen.Create(head, health);
            PlayerSounds.Create(health, head);
            health.onHealthChanged.AddListener(Refresh);
            recover = new InputAction("Recover player health", InputActionType.Button);
            recover.AddBinding("<XRController>{RightHand}/primaryButton");
            recover.AddBinding("<XRController>{LeftHand}/primaryButton");
#if UNITY_EDITOR
            recover.AddBinding("<Keyboard>/f8");
#endif
            recover.performed += OnRecover; recover.Enable();
            Refresh(health.Current);
        }
        public bool Recover()
        {
            if (health == null || !health.IsDead) return false;
            health.Restore(); return true;
        }
        void OnRecover(InputAction.CallbackContext context) => Recover();
        void Refresh(float value)
        {
            hint.gameObject.SetActive(health.IsDead);
            if (!health.IsDead) return;
            string message = "Sin vida\nA / X: recuperar vida";
#if UNITY_EDITOR
            message += "\nSimulador: F8";
#endif
            hint.Show(message, .43f, .14f);
        }
        void OnEnable()
        {
            recover?.Enable();
            if (bar != null) bar.gameObject.SetActive(true);
            if (health != null) Refresh(health.Current);
        }
        void OnDisable()
        {
            recover?.Disable();
            if (hint != null) hint.gameObject.SetActive(false);
            if (bar != null) bar.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            if (health != null) health.onHealthChanged.RemoveListener(Refresh);
            recover?.Dispose();
            if (hint != null) Destroy(hint.gameObject);
            if (bar != null) Destroy(bar.gameObject);
            if (blood != null) Destroy(blood.gameObject);
        }
    }
}
