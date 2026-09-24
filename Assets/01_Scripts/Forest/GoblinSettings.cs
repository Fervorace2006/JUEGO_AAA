using UnityEngine;

namespace ForestVR
{
    [CreateAssetMenu(menuName = "Forest VR/Goblin Settings")]
    public sealed class GoblinSettings : ScriptableObject
    {
        [Min(1)] public float health = 100;
        [Min(0)] public float damage = 20;
        [Min(0)] public float speed = 1.6f;
        [Min(0.1f)] public float detectionRadius = 15;
        [Min(0.1f)] public float wakeRadius = 8;
        [Range(10, 180)] public float visionAngle = 110;
        [Min(0)] public float memorySeconds = 4;
        [Min(0.1f)] public float turnSpeed = 180;
        [Min(0)] public float restAfterSeconds = 10;
        [Min(0.1f)] public float attackRange = 1.5f;
        [Min(0.1f)] public float attackCooldown = 2;
        [Min(0)] public float attackImpactDelay = 0.45f;
        [Min(0)] public float slowAttackImpactDelay = 0.9f;
        [Min(0)] public float slowAttackDamage = 30;
        [Min(0)] public float respawnSeconds = 1200;
        [Min(0.1f)] public float zoneRadius = 25;
        [Min(1)] public float corpseSeconds = 8;
        public AnimationClip idle, walk, attack, death;
        public AnimationClip sleep, relaxing, gettingUp, slowAttack, attackedFromBack;
    }
}
