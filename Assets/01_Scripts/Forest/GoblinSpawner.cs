using System.Collections.Generic;
using UnityEngine;

namespace ForestVR
{
    [DisallowMultipleComponent]
    public sealed class GoblinSpawner : MonoBehaviour
    {
        public GameObject goblinPrefab;
        public GoblinSettings settings;
        public Transform player;
        [Tooltip("Emptys hijos directos de este objeto. No aparece nada mientras este vacio.")]
        public Transform spawnPointsRoot;
        [Tooltip("Puntos explicitos; pueden estar en cualquier parte de la jerarquia.")]
        public Transform[] spawnPoints = new Transform[0];
        GoblinActor current;
        Health playerHealth;
        Transform head;
        double readyAt;
        bool waitingForDeath;
        readonly List<Transform> candidates = new List<Transform>();
        public double RemainingSeconds => System.Math.Max(0, readyAt - Time.timeAsDouble);
        void Start()
        {
            if (player == null || settings == null || goblinPrefab == null)
            { Debug.LogError("GoblinSpawner: faltan jugador, prefab o configuracion.", this); enabled = false; return; }
            playerHealth = player.GetComponent<Health>();
            if (playerHealth == null) playerHealth = player.gameObject.AddComponent<Health>();
            var camera = player.GetComponentInChildren<Camera>(true);
            head = camera != null ? camera.transform : player;
        }
        void Update()
        {
            if (playerHealth == null || playerHealth.IsDead || !player.gameObject.activeInHierarchy) return;
            // Unexpected removal also starts the cooldown rather than spawning immediately.
            if (waitingForDeath)
            {
                if (current != null) return;
                BeginCooldown();
            }
            if (current != null || Time.timeAsDouble < readyAt) return;
            candidates.Clear();
            if (spawnPointsRoot != null) foreach (Transform point in spawnPointsRoot) AddCandidate(point);
            foreach (var point in spawnPoints) AddCandidate(point);
            if (candidates.Count == 0) return;
            var chosen = candidates[Random.Range(0, candidates.Count)];
            var instance = Instantiate(goblinPrefab, chosen.position, chosen.rotation);
            current = instance.GetComponent<GoblinActor>();
            current.Initialize(settings, head, playerHealth);
            waitingForDeath = true;
            current.Health.Died += BeginCooldown;
        }
        void AddCandidate(Transform point)
        {
            if (point != null && point.gameObject.activeInHierarchy && !candidates.Contains(point)
                && Vector3.Distance(head.position, point.position) <= settings.zoneRadius) candidates.Add(point);
        }
        void BeginCooldown()
        {
            if (!waitingForDeath) return;
            waitingForDeath = false;
            readyAt = Time.timeAsDouble + settings.respawnSeconds;
            if (current != null) current.Health.Died -= BeginCooldown;
        }
        void OnDestroy() { if (current != null) current.Health.Died -= BeginCooldown; }
        void OnDrawGizmosSelected()
        {
            if (settings == null) return;
            Gizmos.color = Color.green;
            if (spawnPointsRoot != null) foreach (Transform point in spawnPointsRoot) DrawPoint(point);
            foreach (var point in spawnPoints) if (point != null) DrawPoint(point);
        }
        void DrawPoint(Transform point)
        {
            Gizmos.color = Color.green; Gizmos.DrawWireSphere(point.position, settings.zoneRadius);
            Gizmos.DrawSphere(point.position, 0.2f);
            Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(point.position, settings.wakeRadius);
        }
    }
}
