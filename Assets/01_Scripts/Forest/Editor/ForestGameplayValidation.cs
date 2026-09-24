using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestVR.Editor
{
    // Run only in a disposable validation project: enters Play Mode and exits the editor.
    [InitializeOnLoad]
    public static class ForestGameplayValidation
    {
        const string Flag = "ForestVR.BatchValidation";
        static int phase, frames;
        static double started;
        static GoblinSpawner spawner;
        static Health playerHealth;
        static Transform point;
        static GoblinActor first;
        static ForestGameplayValidation() { EditorApplication.update += Tick; }
        public static void RunBatch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); Debug.Log("FOREST PASS: " + message); }
        static GoblinActor[] Actors() => UnityEngine.Object.FindObjectsByType<GoblinActor>();
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                switch (phase)
                {
                    case 0:
                        var player = new GameObject("Validation player");
                        playerHealth = player.AddComponent<Health>();
                        var camera = new GameObject("Head"); camera.transform.SetParent(player.transform);
                        camera.transform.localPosition = Vector3.up * 1.6f; camera.AddComponent<Camera>();
                        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        floor.transform.position = Vector3.down * 0.5f;
                        floor.transform.localScale = new Vector3(100, 1, 100);
                        spawner = new GameObject("Spawner").AddComponent<GoblinSpawner>();
                        spawner.player = player.transform;
                        spawner.settings = AssetDatabase.LoadAssetAtPath<GoblinSettings>("Assets/SO_/ForestGoblinSettings.asset");
                        spawner.goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Enemy/ForestGoblin.prefab");
                        spawner.spawnPointsRoot = new GameObject("Points").transform;
                        Check(spawner.settings.respawnSeconds == 1200, "Default cooldown is 20 minutes");
                        var health = new GameObject("Health test").AddComponent<Health>();
                        int deaths = 0; health.Died += () => deaths++;
                        health.TakeDamage(-5); Check(health.Current == 100, "Negative damage ignored");
                        health.TakeDamage(25); health.Heal(10); Check(health.Current == 85, "Damage and healing");
                        health.TakeDamage(1000); health.TakeDamage(1000); health.Heal(100);
                        Check(health.IsDead && deaths == 1, "Death fires once and healing does not revive");
                        health.Restore(); Check(health.Current == 100, "Explicit restore");
                        new GameObject("Preview").AddComponent<EditorOnlyLight>();
                        Check(!Resources.FindObjectsOfTypeAll<Light>().Any(l => l.name == "Luz de edicion (temporal)"), "No editor preview light during play");
                        phase++; break;
                    case 1:
                        if (++frames < 5) break;
                        Check(Actors().Length == 0, "No spawn without points");
                        point = new GameObject("SPAWN_DUENDE").transform;
                        spawner.spawnPoints = new[] { point };
                        point.position = Vector3.right * 100; frames = 0; phase++; break;
                    case 2:
                        if (++frames < 5) break;
                        Check(Actors().Length == 0, "No spawn outside zone");
                        point.position = Vector3.forward * 0.8f;
                        var secondPoint = new GameObject("Point B").transform; secondPoint.SetParent(spawner.spawnPointsRoot);
                        secondPoint.position = point.position;
                        point.rotation = Quaternion.Euler(0, 180, 0);
                        secondPoint.rotation = point.rotation;
                        frames = 0; phase++; break;
                    case 3:
                        if (++frames < 10) break;
                        Check(Actors().Length == 1, "Exactly one goblin with two eligible points");
                        first = Actors()[0]; started = EditorApplication.timeSinceStartup; phase++; break;
                    case 4:
                        if (playerHealth.Current == 100 && EditorApplication.timeSinceStartup - started < 10) break;
                        Check(playerHealth.Current < 100, "Goblin attack damages player");
                        first.Health.TakeDamage(1000);
                        Check(spawner.RemainingSeconds > 1199 && spawner.RemainingSeconds <= 1200, "Cooldown begins on death");
                        Check(!first.GetComponent<CharacterController>().enabled, "Dead goblin collision disabled");
                        UnityEngine.Object.Destroy(first.gameObject); frames = 0; phase++; break;
                    case 5:
                        if (++frames < 10) break;
                        Check(Actors().Length == 0 && spawner.RemainingSeconds > 0, "No replacement before cooldown");
                        foreach (Transform t in spawner.spawnPointsRoot) t.position = Vector3.right * 100;
                        point.position = Vector3.right * 100;
                        typeof(GoblinSpawner).GetField("readyAt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spawner, Time.timeAsDouble - 1);
                        frames = 0; phase++; break;
                    case 6:
                        if (++frames < 10) break;
                        Check(Actors().Length == 0, "Expired cooldown still requires nearby player");
                        point.position = Vector3.forward * 0.8f; frames = 0; phase++; break;
                    case 7:
                        if (++frames < 10) break;
                        Check(Actors().Length == 1, "Returning to zone respawns one goblin");
                        playerHealth.TakeDamage(1000);
                        Actors()[0].Health.TakeDamage(1000);
                        UnityEngine.Object.Destroy(Actors()[0].gameObject);
                        typeof(GoblinSpawner).GetField("readyAt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spawner, Time.timeAsDouble - 1);
                        frames = 0; phase++; break;
                    case 8:
                        if (++frames < 10) break;
                        Check(Actors().Length == 0, "No spawn for a dead player");
                        SessionState.SetBool(Flag, false);
                        Debug.Log("FOREST_GAMEPLAY_VALIDATION_OK"); EditorApplication.Exit(0); break;
                }
            }
            catch (Exception e)
            { SessionState.SetBool(Flag, false); Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}

