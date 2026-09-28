using System;
using UnityEngine;
using UnityEngine.Events;

namespace ForestVR
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] float maximum = 100;
        public UnityEvent onDeath = new UnityEvent();
        public UnityEvent<float> onHealthChanged = new UnityEvent<float>();
        public event Action Died;
        public event Action<Vector3> Damaged;
        public float Current { get; private set; }
        public float Maximum => maximum;
        public bool IsDead => Current <= 0;
        void Awake() => Current = maximum;
        public void Initialize(float value) { maximum = Mathf.Max(1, value); Restore(); }
        public void TakeDamage(float amount) => TakeHit(amount, transform.position + transform.forward);
        public void TakeHit(float amount, Vector3 sourcePosition)
        {
            if (IsDead || amount <= 0 || float.IsNaN(amount)) return;
            Current = Mathf.Max(0, Current - amount);
            onHealthChanged.Invoke(Current);
            Damaged?.Invoke(sourcePosition);
            if (IsDead) { Died?.Invoke(); onDeath.Invoke(); }
        }
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0 || float.IsNaN(amount)) return;
            Current = Mathf.Min(maximum, Current + amount);
            onHealthChanged.Invoke(Current);
        }
        public void Restore() { Current = maximum; onHealthChanged.Invoke(Current); }
        [ContextMenu("Debug/Recibir 25 de dano")]
        void DebugDamage() { if (Application.isPlaying) TakeDamage(25); }
    }
}
