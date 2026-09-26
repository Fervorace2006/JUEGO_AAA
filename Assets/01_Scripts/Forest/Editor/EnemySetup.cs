using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestVR.EditorTools
{
    // Builds the zombie (intermediate enemy) and the werewolf (final boss) from their Tripo models and Mixamo clips,
    // reusing GoblinActor, and places their spawners in ForestScene. Safe to run again: it updates what exists.
    public static class EnemySetup
    {
        const string ZombieClips = "Assets/02_Prefabs/Enemy/ZombieAnimations";
        const string WolfClips = "Assets/02_Prefabs/Enemy/WolfAnimations";
        const string ZombieModel = "Assets/TripoModels/undead_zombie_3d_model/undead_zombie_3d_model.fbx";
        const string WolfModel = "Assets/TripoModels/werewolf_3d_model/werewolf_3d_model.fbx";
        const string ZombieSettingsPath = "Assets/SO_/ForestZombieSettings.asset";
        const string WolfSettingsPath = "Assets/SO_/ForestWerewolfSettings.asset";
        const string ZombiePrefabPath = "Assets/02_Prefabs/Enemy/ForestZombie.prefab";
        const string WolfPrefabPath = "Assets/02_Prefabs/Enemy/ForestWerewolf.prefab";
        const float ZombieHeight = 1.8f, WolfHeight = 2.5f;
        static readonly string[] LoopingClips = { "zombie idle", "zombie walk", "zombie run", "zombie crawl", "running crawl", "WolfIdle", "wolfwalk", "WolfRun" };

        [MenuItem("Forest VR/Enemigos/Crear zombie y hombre lobo y colocarlos en la escena")]
        public static void CreateAndPlace()
        {
            var report = new List<string>();
            CreateAssets(report);
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "ForestScene")
            {
                EditorUtility.DisplayDialog("Enemigos", "Prefabs creados. Abre ForestScene y vuelve a ejecutar este menu para colocarlos en el mapa.", "OK");
                return;
            }
            PlaceInScene(report);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("EnemySetup:\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Enemigos", "Zombie y Hombre Lobo creados y colocados en ForestScene.\nGuarda la escena (Ctrl+S).", "OK");
        }

        public static void CreateAssets(List<string> report)
        {
            ConfigureClips(ZombieClips); ConfigureClips(WolfClips);
            var zombie = LoadOrCreate(ZombieSettingsPath);
            zombie.displayName = "Zombie"; zombie.healthBarColor = new Color(.16f, .85f, .25f); zombie.nameplateScale = 1;
            zombie.health = 200; zombie.damage = 20; zombie.slowAttackDamage = 35;
            zombie.speed = 1.1f; zombie.run = Clip(ZombieClips, "zombie run"); zombie.runSpeed = 2.6f; zombie.runDistance = 5;
            zombie.detectionRadius = 16; zombie.wakeRadius = 10; zombie.visionAngle = 120; zombie.memorySeconds = 6;
            zombie.turnSpeed = 150; zombie.restAfterSeconds = 15; zombie.attackRange = 1.6f; zombie.attackCooldown = 2.2f;
            zombie.attackImpactDelay = .6f; zombie.slowAttackImpactDelay = 1.2f;
            zombie.respawnSeconds = 300; zombie.maxAlive = 3; zombie.zoneRadius = 500; zombie.corpseSeconds = 10;
            zombie.mapPopulation = 0; zombie.populationSpacing = 15; zombie.populationMinPlayerDistance = 25;
            zombie.idle = Clip(ZombieClips, "zombie idle"); zombie.walk = Clip(ZombieClips, "zombie walk");
            zombie.attack = Clip(ZombieClips, "zombie attack"); zombie.slowAttack = Clip(ZombieClips, "zombie neck bite");
            zombie.death = Clip(ZombieClips, "zombie death");
            // Resting zombies stand swaying; when they notice the player they scream before chasing.
            zombie.sleep = null; zombie.relaxing = null; zombie.gettingUp = Clip(ZombieClips, "zombie scream");
            zombie.attackedFromBack = null; zombie.lockHipsInPlace = true;
            EditorUtility.SetDirty(zombie);

            var wolf = LoadOrCreate(WolfSettingsPath);
            wolf.displayName = "Hombre Lobo"; wolf.healthBarColor = new Color(.85f, .08f, .08f); wolf.nameplateScale = 1.4f;
            wolf.health = 1500; wolf.damage = 35; wolf.slowAttackDamage = 35;
            wolf.speed = 1.8f; wolf.run = Clip(WolfClips, "WolfRun"); wolf.runSpeed = 5; wolf.runDistance = 4;
            wolf.detectionRadius = 25; wolf.wakeRadius = 18; wolf.visionAngle = 140; wolf.memorySeconds = 8;
            wolf.turnSpeed = 200; wolf.restAfterSeconds = 30; wolf.attackRange = 2.3f; wolf.attackCooldown = 1.6f;
            wolf.attackImpactDelay = .5f; wolf.slowAttackImpactDelay = .5f;
            wolf.respawnSeconds = 300; wolf.maxAlive = 1; wolf.zoneRadius = 500; wolf.corpseSeconds = 12; wolf.mapPopulation = 0;
            wolf.idle = Clip(WolfClips, "WolfIdle"); wolf.walk = Clip(WolfClips, "wolfwalk");
            wolf.attack = Clip(WolfClips, "WolfAttack"); wolf.slowAttack = null; wolf.death = Clip(WolfClips, "wolfdied");
            wolf.sleep = null; wolf.relaxing = null; wolf.gettingUp = null;
            wolf.attackedFromBack = Clip(WolfClips, "wolf Right Turn 90"); wolf.lockHipsInPlace = true;
            EditorUtility.SetDirty(wolf);
            AssetDatabase.SaveAssets();

            BuildPrefab("ForestZombie", ZombieModel, zombie, ZombieHeight, .35f, ZombiePrefabPath, report);
            BuildPrefab("ForestWerewolf", WolfModel, wolf, WolfHeight, .5f, WolfPrefabPath, report);
        }

        // Mixamo clips import without looping: loop the ones used for idle, walking and running, and name each clip after its file.
        static void ConfigureClips(string folder)
        {
            foreach (var path in Directory.GetFiles(folder, "*.fbx"))
            {
                var file = path.Replace('\\', '/');
                var importer = (ModelImporter)AssetImporter.GetAtPath(file);
                if (importer == null) continue;
                string name = Path.GetFileNameWithoutExtension(file);
                bool loop = LoopingClips.Contains(name);
                var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                bool changed = importer.clipAnimations.Length == 0 || importer.animationType != ModelImporterAnimationType.Generic;
                foreach (var clip in clips)
                {
                    if (clip.loopTime != loop || clip.name != name) changed = true;
                    clip.loopTime = loop; clip.name = name;
                }
                if (!changed) continue;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        static AnimationClip Clip(string folder, string name) =>
            AssetDatabase.LoadAllAssetsAtPath($"{folder}/{name}.fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        static GoblinSettings LoadOrCreate(string path)
        {
            var settings = AssetDatabase.LoadAssetAtPath<GoblinSettings>(path);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<GoblinSettings>();
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        static void BuildPrefab(string name, string modelPath, GoblinSettings settings, float height, float radius, string prefabPath, List<string> report)
        {
            var root = new GameObject(name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
                model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
                // The Animator goes where the clip paths resolve (the model root, or "Armature" for clips exported without it).
                var host = FindAnimatorHost(model.transform, settings.idle, out float resolved);
                // The Tripo "Armature" keeps Blender's -90 degree axis rotation, but the Mixamo clips animate "Hips" as a root
                // with no parent rotation: without this the body lies on its back and walking lifts it into the air.
                if (host != model.transform) host.localRotation = Quaternion.identity;
                foreach (var other in model.GetComponentsInChildren<Animator>(true)) if (other.transform != host) other.enabled = false;
                var animator = host.GetComponent<Animator>();
                if (animator == null) animator = host.gameObject.AddComponent<Animator>();
                animator.enabled = true; animator.runtimeAnimatorController = null; animator.avatar = null; animator.applyRootMotion = false;

                // Standing size from the idle pose, measured on a throwaway copy so the prefab keeps its bones untouched:
                // scale to the wanted height with the feet on the ground, centred on the root.
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                Bounds bounds;
                try
                {
                    probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var probeHost = host == model.transform ? probe.transform : probe.transform.Find(AnimationUtility.CalculateTransformPath(host, model.transform));
                    if (probeHost != null && probeHost != probe.transform) probeHost.localRotation = Quaternion.identity;
                    if (settings.idle != null && probeHost != null) settings.idle.SampleAnimation(probeHost.gameObject, 0);
                    bounds = RendererBounds(probe);
                }
                finally { Object.DestroyImmediate(probe); }
                float scale = bounds.size.y > 0.01f ? height / bounds.size.y : 1;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;

                var body = root.AddComponent<CharacterController>();
                body.height = height; body.radius = radius; body.center = new Vector3(0, height * .5f, 0);
                body.slopeLimit = 45; body.stepOffset = Mathf.Min(.4f, height * .2f); body.skinWidth = .03f; body.minMoveDistance = .001f;
                var health = root.AddComponent<Health>();
                var actor = root.AddComponent<GoblinActor>();
                actor.settings = settings; actor.animator = animator;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                report.Add($"{name}: animador en '{host.name}' ({resolved:P0} de las pistas del idle encontradas), escala del modelo {scale:0.###}, altura {height} m");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Transform FindAnimatorHost(Transform model, AnimationClip clip, out float resolved)
        {
            resolved = 0;
            if (clip == null) return model;
            var paths = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform)).Select(b => b.path).Distinct().ToList();
            Transform best = model; int bestCount = -1;
            foreach (var candidate in model.GetComponentsInChildren<Transform>(true))
            {
                int count = paths.Count(p => p.Length == 0 || candidate.Find(p) != null);
                if (count > bestCount) { bestCount = count; best = candidate; }
            }
            resolved = paths.Count > 0 ? bestCount / (float)paths.Count : 0;
            return best;
        }

        static Bounds RendererBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            bool first = true;
            foreach (var renderer in renderers)
            {
                Bounds b;
                if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    // Bake the current pose so the bounds follow the sampled animation, not the import pose.
                    var mesh = new Mesh();
                    skinned.BakeMesh(mesh, true);
                    b = TransformBounds(skinned.transform, mesh.bounds);
                    Object.DestroyImmediate(mesh);
                }
                else b = renderer.bounds;
                if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
            }
            return bounds;
        }

        static Bounds TransformBounds(Transform t, Bounds local)
        {
            var result = new Bounds(t.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                result.Encapsulate(t.TransformPoint(corner));
            }
            return result;
        }

        // Adds (or updates) one spawner per new enemy, with its spawn point on open ground: the zombie at middle distance
        // from the player's start, the werewolf far away as the final boss.
        public static void PlaceInScene(List<string> report)
        {
            var goblinSpawner = Object.FindObjectsByType<GoblinSpawner>(FindObjectsInactive.Include).FirstOrDefault(s => s.settings != null && s.settings.displayName == "Duende")
                ?? Object.FindObjectsByType<GoblinSpawner>(FindObjectsInactive.Include).FirstOrDefault();
            if (goblinSpawner == null || goblinSpawner.player == null) { report.Add("No se encontro el Goblin Spawner con su jugador; no se colocaron enemigos."); return; }
            var player = goblinSpawner.player;
            var floor = FindFloor();
            if (floor == null && (UserPoints("spawn zombie").Length == 0 || UserPoints("spawn wolf", "spawn lobo", "spawn hombre lobo").Length == 0))
            { report.Add("No se encontro el suelo (FloorWithLake) ni puntos Spawn Zombie / Spawn Wolf; no se colocaron enemigos."); return; }
            Physics.SyncTransforms();
            var avoid = new List<Vector3> { player.position };
            if (goblinSpawner.spawnPoints != null) avoid.AddRange(goblinSpawner.spawnPoints.Where(p => p != null).Select(p => p.position));
            if (goblinSpawner.spawnPointsRoot != null) foreach (Transform p in goblinSpawner.spawnPointsRoot) avoid.Add(p.position);

            // Points placed by hand in the scene ("Spawn Zombie", "Spawn Zombie (1)", "Spawn Wolf"...) win over automatic ones.
            var zombiePoints = UserPoints("spawn zombie");
            var wolfPoints = UserPoints("spawn wolf", "spawn lobo", "spawn hombre lobo");
            var zombiePoint = zombiePoints.Length > 0 ? zombiePoints[0].position : FindSpot(floor, player.position, 30, avoid, ZombieHeight);
            avoid.Add(zombiePoint);
            var wolfPoint = wolfPoints.Length > 0 ? wolfPoints[0].position : FindSpot(floor, player.position, 60, avoid, WolfHeight);
            Place("Zombie Spawner", "SPAWN_ZOMBIE", ZombiePrefabPath, ZombieSettingsPath, zombiePoint, zombiePoints, player, goblinSpawner.transform.parent, report);
            Place("Hombre Lobo Spawner", "SPAWN_HOMBRE_LOBO", WolfPrefabPath, WolfSettingsPath, wolfPoint, wolfPoints, player, goblinSpawner.transform.parent, report);
        }

        static Transform[] UserPoints(params string[] prefixes) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)
                .Where(t => prefixes.Any(p => t.name.ToLowerInvariant().StartsWith(p)) && t.GetComponent<GoblinSpawner>() == null)
                .OrderBy(t => t.name).ToArray();

        static Collider FindFloor()
        {
            var probe = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name == "FloorWithLake");
            if (probe == null) return null;
            return probe.GetComponentsInChildren<Collider>().OrderByDescending(c => c.bounds.size.x * c.bounds.size.z).FirstOrDefault();
        }

        static Vector3 FindSpot(Collider floor, Vector3 from, float wanted, List<Vector3> avoid, float height)
        {
            var area = floor.bounds; Vector3 best = from; float bestScore = float.MaxValue;
            for (float x = area.min.x + 3; x <= area.max.x - 3; x += 2)
            for (float z = area.min.z + 3; z <= area.max.z - 3; z += 2)
            {
                if (!Physics.Raycast(new Vector3(x, area.max.y + 30, z), Vector3.down, out var hit, area.size.y + 60, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                // The first thing seen from above must be the floor itself (not the lake, a tree, a rock or the house), fairly flat.
                if (hit.collider != floor || Vector3.Angle(hit.normal, Vector3.up) > 20) continue;
                if (Physics.CheckCapsule(hit.point + Vector3.up * .6f, hit.point + Vector3.up * height, .6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (avoid.Any(a => Flat(a - hit.point).magnitude < 18)) continue;
                float score = Mathf.Abs(Flat(hit.point - from).magnitude - wanted);
                if (score < bestScore) { bestScore = score; best = hit.point; }
            }
            return best;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        static void Place(string spawnerName, string pointName, string prefabPath, string settingsPath, Vector3 position, Transform[] userPoints, Transform player, Transform parent, List<string> report)
        {
            var existing = Object.FindObjectsByType<GoblinSpawner>(FindObjectsInactive.Include).FirstOrDefault(s => s.name == spawnerName);
            var spawner = existing != null ? existing : new GameObject(spawnerName).AddComponent<GoblinSpawner>();
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(spawner.gameObject, "Crear " + spawnerName);
                spawner.transform.SetParent(parent, false);
            }
            spawner.goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            spawner.settings = AssetDatabase.LoadAssetAtPath<GoblinSettings>(settingsPath);
            spawner.player = player;
            if (userPoints.Length > 0)
            {
                // Hand-placed points are used where they are; an automatic point from an earlier run is removed.
                var automatic = spawner.transform.Find(pointName);
                if (automatic != null) Undo.DestroyObjectImmediate(automatic.gameObject);
                spawner.spawnPoints = userPoints; spawner.spawnPointsRoot = null;
                foreach (var p in userPoints)
                {
                    var g = p.GetComponent<GoblinSpawnGizmos>();
                    if (g == null) g = Undo.AddComponent<GoblinSpawnGizmos>(p.gameObject);
                    g.settings = spawner.settings;
                    EditorUtility.SetDirty(p.gameObject);
                    report.Add($"{spawnerName}: usa tu punto '{p.name}' en {p.position}");
                }
                EditorUtility.SetDirty(spawner);
                return;
            }
            var point = spawner.transform.Find(pointName);
            if (point == null)
            {
                point = new GameObject(pointName).transform;
                point.SetParent(spawner.transform, false);
                point.position = position + Vector3.up * .05f;
                // Facing the player's start, so it turns toward whoever approaches from there.
                var look = Flat(player.position - position);
                if (look.sqrMagnitude > .01f) point.rotation = Quaternion.LookRotation(look);
            }
            spawner.spawnPoints = new[] { point };
            spawner.spawnPointsRoot = null;
            var gizmos = point.GetComponent<GoblinSpawnGizmos>();
            if (gizmos == null) gizmos = point.gameObject.AddComponent<GoblinSpawnGizmos>();
            gizmos.settings = spawner.settings;
            EditorUtility.SetDirty(spawner);
            report.Add($"{spawnerName}: punto {pointName} en {point.position} ({Flat(point.position - player.position).magnitude:0} m del inicio del jugador)");
        }
    }
}
