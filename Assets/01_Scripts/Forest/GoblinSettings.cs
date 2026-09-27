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
        [Tooltip("A sleeping or resting goblin only gets up when the player comes this close (or hits it).")]
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
        [Tooltip("Enemies of this type alive at once at the spawner's points. 1 = the next one appears Respawn Seconds after the previous dies; "
            + "more = a new one appears every Respawn Seconds until this many are alive.")]
        [Min(1)] public int maxAlive = 1;
        [Tooltip("The spawn points in the spawner only produce a goblin while the player is within this distance.")]
        [Min(0.1f)] public float zoneRadius = 25;
        [Tooltip("Extra goblins placed asleep on random free spots all over the ground. 0 = only the spawner points.")]
        [Min(0)] public int mapPopulation = 0;
        [Min(1)] public float populationSpacing = 12;
        [Tooltip("A map goblin never appears (or respawns) closer than this to the player.")]
        [Min(0)] public float populationMinPlayerDistance = 20;
        [Min(1)] public float corpseSeconds = 8;
        public AnimationClip idle, walk, attack, death;
        public AnimationClip sleep, relaxing, gettingUp, slowAttack, attackedFromBack;
        [Header("Carrera (opcional)")]
        [Tooltip("Clip used while chasing from farther than Run Distance. Empty = always walks.")]
        public AnimationClip run;
        [Min(0)] public float runSpeed = 0;
        [Min(0)] public float runDistance = 4;
        [Header("Herido (opcional)")]
        [Tooltip("Animaciones de desplazamiento usadas al bajar de este porcentaje de vida. Si no hay clip, conserva caminar y correr.")]
        [Range(0, 1)] public float woundedThreshold;
        public AnimationClip woundedWalk, woundedRun;
        [Min(0)] public float woundedSpeed = 0.8f;
        [Min(0)] public float woundedRunSpeed = 1.5f;
        [Tooltip("For clips that move the hips forward instead of animating in place: keeps the body over the character.")]
        public bool lockHipsInPlace;
        [Header("Nombre y barra de vida sobre la cabeza")]
        public string displayName = "Duende";
        public Color healthBarColor = new Color(.16f, .85f, .25f);
        [Min(0.1f)] public float nameplateScale = 1;
        [Header("Sonidos")]
        [Tooltip("Loops while it sleeps or rests (snoring, breathing).")]
        public AudioClip sleepLoop;
        [Range(0, 1)] public float sleepVolume = .6f;
        [Tooltip("Once, when it wakes up and goes for the player.")]
        public AudioClip alertSound;
        [Tooltip("Now and then while it chases the player.")]
        public AudioClip[] chaseSounds = new AudioClip[0];
        public Vector2 chaseSoundInterval = new Vector2(5, 10);
        public AudioClip attackSound;
        [Range(0, 1)] public float attackSoundChance = .6f;
        [Tooltip("When one of its blows hurts the player.")]
        public AudioClip hitPlayerSound;
        [Range(0, 1)] public float hitPlayerSoundChance = .5f;
        public AudioClip deathSound;
        [Tooltip("Pitch multiplier for the death sound (below 1 sounds deeper).")]
        [Min(0.1f)] public float deathPitch = 1;
        [Range(0, 1)] public float voiceVolume = 1;
        [Tooltip("Random pitch range: each enemy gets its own voice.")]
        public Vector2 voicePitch = new Vector2(.95f, 1.05f);
        [Min(1)] public float hearingDistance = 25;
    }
}
