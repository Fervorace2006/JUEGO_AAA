using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForestVR
{
    // Runs when ForestScene loads (like the head flashlight), so nothing has to be wired by hand in the scene:
    // - trees block only with their trunk (a capsule), not with the whole canopy;
    // - the player is pushed out of trunks, since walking with the headset does not go through physics;
    // Enemy spawners and their exact spawn points are serialized in ForestScene.
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

    }
}
