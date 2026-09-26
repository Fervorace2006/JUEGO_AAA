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
        sealed class MapSlot { public Vector3 position; public Quaternion rotation; public GoblinActor actor; public bool spawned; public double readyAt; }
        readonly List<MapSlot> mapSlots = new List<MapSlot>();
        bool populationPlaced;
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
            UpdatePopulation();
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
        // Map goblins: asleep on free spots all over the ground. Each spot respawns its goblin after the cooldown,
        // never within sight distance of the player.
        void UpdatePopulation()
        {
            if (settings.mapPopulation <= 0) return;
            if (!populationPlaced)
            {
                // The ground registers itself when the player rig starts.
                if (!GroundSafety.TryGetBounds(out var area)) return;
                PlacePopulation(area); populationPlaced = true;
            }
            foreach (var slot in mapSlots)
            {
                if (slot.actor != null) continue;
                if (slot.spawned) { slot.spawned = false; slot.readyAt = Time.timeAsDouble + settings.respawnSeconds; }
                if (Time.timeAsDouble < slot.readyAt || FlatDistance(head.position, slot.position) < settings.populationMinPlayerDistance) continue;
                slot.actor = Instantiate(goblinPrefab, slot.position, slot.rotation).GetComponent<GoblinActor>();
                slot.actor.Initialize(settings, head, playerHealth);
                slot.spawned = true;
            }
        }
        void PlacePopulation(Bounds area)
        {
            var taken = new List<Vector3>();
            if (spawnPointsRoot != null) foreach (Transform point in spawnPointsRoot) taken.Add(point.position);
            foreach (var point in spawnPoints) if (point != null) taken.Add(point.position);
            for (int attempt = 0; attempt < settings.mapPopulation * 60 && mapSlots.Count < settings.mapPopulation; attempt++)
            {
                var sample = new Vector3(Random.Range(area.min.x, area.max.x), 0, Random.Range(area.min.z, area.max.z));
                if (!GroundSafety.TryGetSurface(sample, out var surface)) continue;
                // The first thing seen from above must be the ground itself: not water, a tree, a rock or the table.
                if (!Physics.Raycast(surface + Vector3.up * 30, Vector3.down, out var hit, 31, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                    || !GroundSafety.IsGround(hit.collider) || Vector3.Angle(hit.normal, Vector3.up) > 25) continue;
                if (FlatDistance(head.position, hit.point) < settings.populationMinPlayerDistance) continue;
                bool crowded = false;
                foreach (var other in taken) if (FlatDistance(other, hit.point) < settings.populationSpacing) { crowded = true; break; }
                if (crowded) continue;
                // Room for the goblin's body.
                if (Physics.CheckCapsule(hit.point + Vector3.up * 0.7f, hit.point + Vector3.up * 1.3f, 0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                taken.Add(hit.point);
                mapSlots.Add(new MapSlot { position = hit.point + Vector3.up * 0.05f, rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0) });
            }
            if (mapSlots.Count < settings.mapPopulation)
                Debug.Log($"GoblinSpawner: solo hubo sitio libre para {mapSlots.Count} de {settings.mapPopulation} duendes en el mapa.", this);
        }
        static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }
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
            Gizmos.color = new Color(1f, .5f, 0f);
            foreach (var slot in mapSlots) { Gizmos.DrawSphere(slot.position, 0.25f); Gizmos.DrawWireSphere(slot.position, settings.wakeRadius); }
        }
        void DrawPoint(Transform point)
        {
            Gizmos.color = Color.green; Gizmos.DrawWireSphere(point.position, settings.zoneRadius);
            Gizmos.DrawSphere(point.position, 0.2f);
            Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(point.position, settings.wakeRadius);
        }
    }
}
