using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForestVR
{
    // Runs when ForestScene loads (like the head flashlight), so nothing has to be wired by hand in the scene:
    // - trees block only with their trunk (a capsule), not with the whole canopy;
    // - the player is pushed out of trunks, since walking with the headset does not go through physics;
    // - enemies listed in Resources/ForestEnemyRoster get a spawner next to the goblin's spawn point.
    public static class ForestSceneSetup
    {
        public static readonly List<CapsuleCollider> Trunks = new List<CapsuleCollider>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Setup(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Setup(scene);

        static void Setup(Scene scene)
        {
            if (scene.name != "ForestScene") return;
            var roster = Resources.Load<ForestEnemyRoster>("ForestEnemyRoster");
            if (roster == null) { Debug.LogWarning("ForestSceneSetup: falta Resources/ForestEnemyRoster."); return; }
            Trunks.RemoveAll(t => t == null);
            SetupTrunks(scene, roster);
            var goblinSpawner = FindGoblinSpawner(scene);
            if (goblinSpawner == null || goblinSpawner.player == null) return;
            var blocker = goblinSpawner.player.GetComponent<VRBodyBlocker>();
            if (blocker == null) goblinSpawner.player.gameObject.AddComponent<VRBodyBlocker>();
            SetupEnemies(scene, roster, goblinSpawner);
        }

        static void SetupTrunks(Scene scene, ForestEnemyRoster roster)
        {
            var sizes = new Dictionary<string, ForestEnemyRoster.Trunk>();
            foreach (var trunk in roster.trunks) if (trunk != null && !string.IsNullOrEmpty(trunk.meshName)) sizes[trunk.meshName] = trunk;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || !sizes.TryGetValue(filter.sharedMesh.name, out var size)) continue;
                    var tree = filter.gameObject;
                    if (tree.GetComponent<CapsuleCollider>() != null) continue;
                    // The imported mesh collider covers the canopy too: players and enemies would stop at the leaves.
                    foreach (var mesh in tree.GetComponents<MeshCollider>()) mesh.enabled = false;
                    var capsule = tree.AddComponent<CapsuleCollider>();
                    capsule.direction = 1; capsule.radius = size.radius; capsule.height = size.height;
                    capsule.center = new Vector3(size.center.x, size.bottom + size.height * .5f, size.center.y);
                    Trunks.Add(capsule);
                }
        }

        static GoblinSpawner FindGoblinSpawner(Scene scene)
        {
            GoblinSpawner first = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var spawner in root.GetComponentsInChildren<GoblinSpawner>(true))
                {
                    if (spawner.settings != null && spawner.settings.displayName == "Duende") return spawner;
                    if (first == null) first = spawner;
                }
            return first;
        }

        static void SetupEnemies(Scene scene, ForestEnemyRoster roster, GoblinSpawner goblinSpawner)
        {
            var existing = new List<GoblinSpawner>();
            foreach (var root in scene.GetRootGameObjects()) existing.AddRange(root.GetComponentsInChildren<GoblinSpawner>(true));
            var player = goblinSpawner.player;
            var taken = new List<Vector3>();
            foreach (var spawner in existing) foreach (var point in spawner.spawnPoints) if (point != null) taken.Add(point.position);
            foreach (var entry in roster.enemies)
            {
                if (entry == null || entry.prefab == null || entry.settings == null) continue;
                // A spawner already set up for this enemy (by hand or with the editor menu) wins.
                if (existing.Exists(s => s.settings == entry.settings)) continue;
                var anchorObject = GameObject.Find(entry.anchorName);
                var anchor = anchorObject != null ? anchorObject.transform
                    : goblinSpawner.spawnPoints.Length > 0 && goblinSpawner.spawnPoints[0] != null ? goblinSpawner.spawnPoints[0] : goblinSpawner.transform;
                var away = Flat(anchor.position - player.position);
                if (away.sqrMagnitude < .01f) away = Flat(anchor.forward);
                away.Normalize();
                var right = Vector3.Cross(Vector3.up, away);
                var wanted = anchor.position + away * entry.offset.y + right * entry.offset.x;
                if (!TryFindFreeGround(wanted, taken, out var spot)) { Debug.LogWarning($"ForestSceneSetup: no hay suelo libre cerca de {wanted} para {entry.label}.", anchor); continue; }
                taken.Add(spot);
                var spawnerObject = new GameObject(entry.label + " Spawner (auto)");
                SceneManager.MoveGameObjectToScene(spawnerObject, scene);
                var point = new GameObject("Spawn " + entry.label).transform;
                point.SetParent(spawnerObject.transform, false);
                point.position = spot + Vector3.up * .05f;
                var look = Flat(player.position - spot);
                if (look.sqrMagnitude > .01f) point.rotation = Quaternion.LookRotation(look);
                // Fields are set before the spawner's Start runs on the next frame.
                var created = spawnerObject.AddComponent<GoblinSpawner>();
                created.goblinPrefab = entry.prefab; created.settings = entry.settings; created.player = player;
                created.spawnPoints = new[] { point };
                existing.Add(created);
            }
        }

        // Ground (not a trunk, fairly flat) with room for a body, as close as possible to the wanted spot.
        static bool TryFindFreeGround(Vector3 wanted, List<Vector3> taken, out Vector3 spot)
        {
            for (float radius = 0; radius <= 9; radius += 1.5f)
                for (int i = 0; i < (radius == 0 ? 1 : 12); i++)
                {
                    var angle = i * Mathf.PI * 2 / 12;
                    var probe = wanted + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    if (!Physics.Raycast(probe + Vector3.up * 30, Vector3.down, out var hit, 80, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider is CapsuleCollider capsule && Trunks.Contains(capsule)) continue;
                    if (Vector3.Angle(hit.normal, Vector3.up) > 30) continue;
                    if (Physics.CheckCapsule(hit.point + Vector3.up * .7f, hit.point + Vector3.up * 2f, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (taken.Exists(t => Flat(t - hit.point).magnitude < 3)) continue;
                    spot = hit.point; return true;
                }
            spot = wanted; return false;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
