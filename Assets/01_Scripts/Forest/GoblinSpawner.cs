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
        [Tooltip("Aparecer exactamente en el empty elegido, sin dispersarse alrededor.")]
        public bool spawnExactlyAtPoints;
        [System.NonSerialized] public bool stopRespawning;
        public event System.Action<GoblinActor> Spawned;
        GoblinActor current;
        Health playerHealth;
        Transform head;
        double readyAt;
        bool waitingForDeath;
        readonly List<Transform> candidates = new List<Transform>();
        readonly List<GoblinActor> alive = new List<GoblinActor>();
        sealed class MapSlot { public Vector3 position; public Quaternion rotation; public GoblinActor actor; public bool spawned; public double readyAt; }
        readonly List<MapSlot> mapSlots = new List<MapSlot>();
        bool populationPlaced;
        bool initialWaveFilled;
        bool initialSingleSpawned;
        // Set at runtime by the story to pace a chapter; negative uses the settings value.
        [System.NonSerialized] public float respawnOverride = -1;
        public float RespawnSeconds => respawnOverride >= 0 ? respawnOverride : settings.respawnSeconds;
        public int EncounterCount
        {
            get
            {
                if (zones.Count == 0) return settings != null ? Mathf.Max(1, settings.maxAlive) : 0;
                int total = 0;
                foreach (var z in zones) if (z.zone != null && z.zone.isActiveAndEnabled) total += z.zone.count;
                return total;
            }
        }
        public bool InitialWaveReady => zones.Count > 0 ? zones.TrueForAll(z => z.filled)
            : settings != null && settings.maxAlive > 1 ? initialWaveFilled : initialSingleSpawned;
        // Lets the next enemy appear right away (used when a chapter starts).
        public void ResetCooldown() { readyAt = 0; foreach (var slot in mapSlots) slot.readyAt = 0; foreach (var z in zones) z.nextAt = 0; }
        // Round spawn zones (a SpawnZone on a spawn point): each one keeps its own count of enemies inside its circle.
        sealed class ZoneState { public SpawnZone zone; public readonly List<GoblinActor> alive = new List<GoblinActor>(); public double nextAt; public bool filled; }
        readonly List<ZoneState> zones = new List<ZoneState>();
        public double RemainingSeconds => System.Math.Max(0, readyAt - Time.timeAsDouble);
        void Start()
        {
            if (player == null || settings == null || goblinPrefab == null)
            { Debug.LogError("GoblinSpawner: faltan jugador, prefab o configuracion.", this); enabled = false; return; }
            playerHealth = player.GetComponent<Health>();
            if (playerHealth == null) playerHealth = player.gameObject.AddComponent<Health>();
            var camera = player.GetComponentInChildren<Camera>(true);
            head = camera != null ? camera.transform : player;
            if (spawnPointsRoot != null) foreach (Transform point in spawnPointsRoot) AddZone(point);
            foreach (var point in spawnPoints) AddZone(point);
        }
        void AddZone(Transform point)
        {
            var zone = point != null ? point.GetComponent<SpawnZone>() : null;
            if (zone != null && !zones.Exists(z => z.zone == zone)) zones.Add(new ZoneState { zone = zone });
        }
        void Update()
        {
            if (playerHealth == null || playerHealth.IsDead || !player.gameObject.activeInHierarchy) return;
            UpdatePopulation();
            if (zones.Count > 0) { UpdateZones(); return; }
            if (settings.maxAlive > 1) { UpdateWaves(); return; }
            if (stopRespawning && initialSingleSpawned) return;
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
            Spawned?.Invoke(current);
            initialSingleSpawned = true;
            waitingForDeath = true;
            current.Health.Died += BeginCooldown;
        }
        // Zones start full; each dead one is replaced, one at a time, respawn seconds after the zone went short.
        void UpdateZones()
        {
            double now = Time.timeAsDouble;
            foreach (var z in zones)
            {
                if (z.zone == null || !z.zone.isActiveAndEnabled) continue;
                z.alive.RemoveAll(a => a == null || a.Health == null || a.Health.IsDead);
                if (z.alive.Count >= z.zone.count) { z.nextAt = -1; continue; }
                if (!z.filled)
                {
                    bool full = true;
                    for (int i = z.alive.Count; i < z.zone.count; i++) if (!SpawnInZone(z)) { full = false; break; }
                    z.filled = full; z.nextAt = -1;
                    continue;
                }
                if (stopRespawning) continue;
                if (z.nextAt < 0) { z.nextAt = now + RespawnSeconds; continue; }
                if (now < z.nextAt) continue;
                SpawnInZone(z);
                z.nextAt = z.alive.Count < z.zone.count ? now + RespawnSeconds : -1;
            }
        }
        bool SpawnInZone(ZoneState z)
        {
            var center = z.zone.transform.position;
            float radius = z.zone.radius;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                // Uniform over the disc, so the edge of the circle is not left empty.
                var p = Random.insideUnitCircle * radius;
                var probe = center + new Vector3(p.x, 0, p.y);
                if (!PlayArea.TryGetWalkableGround(probe, out var ground)) continue;
                bool crowded = false;
                foreach (var other in z.alive) if (other != null && FlatDistance(other.transform.position, ground) < z.zone.spacing) { crowded = true; break; }
                if (crowded) continue;
                // Never right on top of the player, and with room for the body (trunks, rocks, tables).
                if (FlatDistance(head.position, ground) < Mathf.Min(6, radius * .5f)) continue;
                if (Physics.CheckCapsule(ground + Vector3.up * .7f, ground + Vector3.up * 1.4f, .35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                var rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                var actor = Instantiate(goblinPrefab, ground + Vector3.up * .05f, rotation).GetComponent<GoblinActor>();
                actor.Initialize(settings, head, playerHealth);
                z.alive.Add(actor);
                Spawned?.Invoke(actor);
                return true;
            }
            return false;
        }
        // Timed spawning: one more every respawnSeconds (the first right away) while fewer than maxAlive are alive.
        void UpdateWaves()
        {
            alive.RemoveAll(a => a == null || a.Health == null || a.Health.IsDead);
            if (stopRespawning && initialWaveFilled) return;
            if ((!stopRespawning && Time.timeAsDouble < readyAt) || alive.Count >= settings.maxAlive) return;
            candidates.Clear();
            if (spawnPointsRoot != null) foreach (Transform point in spawnPointsRoot) AddCandidate(point);
            foreach (var point in spawnPoints) AddCandidate(point);
            if (candidates.Count == 0) return;
            var chosen = candidates[Random.Range(0, candidates.Count)];
            // Spread around the point so a new one does not appear inside one still standing there.
            var position = chosen.position;
            if (!spawnExactlyAtPoints)
            {
                var offset = Random.insideUnitCircle * 1.5f;
                position += new Vector3(offset.x, 0, offset.y);
                if (GroundSafety.TryGetSurface(position, out var surface) && Mathf.Abs(surface.y - chosen.position.y) < 1) position.y = surface.y + 0.05f;
                else position = chosen.position;
            }
            var actor = Instantiate(goblinPrefab, position, chosen.rotation).GetComponent<GoblinActor>();
            actor.Initialize(settings, head, playerHealth);
            alive.Add(actor);
            Spawned?.Invoke(actor);
            if (stopRespawning && alive.Count >= settings.maxAlive) initialWaveFilled = true;
            readyAt = stopRespawning ? 0 : Time.timeAsDouble + RespawnSeconds;
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
                if (stopRespawning && slot.spawned) continue;
                if (slot.spawned) { slot.spawned = false; slot.readyAt = Time.timeAsDouble + RespawnSeconds; }
                if (Time.timeAsDouble < slot.readyAt || FlatDistance(head.position, slot.position) < settings.populationMinPlayerDistance) continue;
                slot.actor = Instantiate(goblinPrefab, slot.position, slot.rotation).GetComponent<GoblinActor>();
                slot.actor.Initialize(settings, head, playerHealth);
                slot.spawned = true;
                Spawned?.Invoke(slot.actor);
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
            readyAt = Time.timeAsDouble + RespawnSeconds;
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
