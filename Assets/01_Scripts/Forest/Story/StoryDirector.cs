using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ForestVR
{
    // Mateo's three notebook fragments are carried by the last enemy in each fixed encounter.
    public sealed class StoryDirector : MonoBehaviour
    {
        const string Goblin = "Duende", Zombie = "Zombie", Werewolf = "Hombre Lobo";
        public static StoryDirector Current { get; private set; }
        public int Chapter { get; private set; }

        Transform head;
        Health player;
        StoryHud hud;
        readonly Dictionary<string, GoblinSpawner> spawners = new Dictionary<string, GoblinSpawner>();
        readonly Dictionary<string, int> kills = new Dictionary<string, int>();
        readonly HashSet<GoblinActor> watched = new HashSet<GoblinActor>();
        readonly HashSet<string> dropped = new HashSet<string>();
        readonly List<GoblinActor> living = new List<GoblinActor>();
        bool narrating, deathLineSaid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "ForestScene" || Current != null) return;
            var go = new GameObject("Story Director");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<StoryDirector>();
        }

        void Awake()
        {
            Current = this;
            // Before any spawner runs its first frame: the dead and the beast wait for their chapter,
            // the goblins already sleep in the forest.
            PagePickup.ResetCount();
            PagePickup.Required = 3;
            foreach (var spawner in FindObjectsByType<GoblinSpawner>())
                if (spawner.settings != null)
                {
                    spawners[spawner.settings.displayName] = spawner;
                    spawner.stopRespawning = true;
                    spawner.Spawned += Watch;
                }
            SetSpawner(Zombie, false);
            SetSpawner(Werewolf, false);
        }
        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (player != null) player.Died -= OnPlayerDied;
            foreach (var spawner in spawners.Values) if (spawner != null) spawner.Spawned -= Watch;
            if (hud != null) Destroy(hud.gameObject);
        }

        IEnumerator Start()
        {
            // Let the rig and the spawners start first.
            yield return null;
            if (spawners.TryGetValue(Goblin, out var goblins) && goblins.player != null) player = goblins.player.GetComponent<Health>();
            var camera = Camera.main;
            if (camera == null) { Debug.LogError("StoryDirector: no hay camara principal."); yield break; }
            head = camera.transform;
            hud = StoryHud.Create(head);
            if (player != null) player.Died += OnPlayerDied;
            Night.Capture();
            StartCoroutine(TrackEnemies());
            if (GameAudio.Get != null) GameAudio.Music(GameAudio.Get.forestMusic, 5);
            StartCoroutine(Whispers());
            yield return Story();
        }

        IEnumerator Story()
        {
            yield return new WaitForSeconds(2.5f);
            // ---------- Prologue ----------
            Chapter = 0;
            yield return hud.ChapterCard("Shadowwood", "Prólogo · Tres noches");
            yield return Say(
                "Hace tres noches que Mateo, tu hermano, entró en Shadowwood. No volvió.",
                "Sus notas quedaron dispersas entre las criaturas del bosque.",
                "Encuentra las tres piezas de su libreta para seguir su rastro.");

            // ---------- I: the goblins and the diary pages ----------
            Chapter = 1;
            hud.SetObjective("");
            hud.SetMarker(null);
            yield return hud.ChapterCard("Capítulo I", "Las páginas robadas");
            yield return Say(
                "Los duendes guardan la primera pieza de la libreta de Mateo.",
                "Elimínalos a todos y busca la pieza en el último que caiga.");
            Night.Darken(.25f, 10);
            yield return EncounterAndPage(Goblin, 1, "Elimina a todos los duendes y consigue la primera pieza");

            // ---------- II: second table, the bow, the dead ----------
            Chapter = 2;
            hud.SetMarker(null);
            hud.SetObjective("");
            Whisper(3);
            yield return Say(
                "La primera nota de Mateo señala a los muertos más adelante.",
                "Antes de ir, recoge el arco de la segunda mesa.");
            yield return hud.ChapterCard("Capítulo II", "Los que no descansan");
            hud.SetObjective("Toma el arco de la segunda mesa");
            hud.SetMarker(AnchorOn("MESA 2", "Segunda mesa"), .6f);
            while (!Holding<VRBow>()) yield return null;
            hud.SetMarker(null);
            SetSpawner(Zombie, true);
            Night.Darken(.5f, 12);
            yield return EncounterAndPage(Zombie, 2, "Elimina a todos los zombis y consigue la segunda pieza");
            SetSpawner(Zombie, false);

            // ---------- III: third table, the revolver, the werewolf ----------
            Chapter = 3;
            hud.SetMarker(null);
            hud.SetObjective("");
            yield return Say(
                "La segunda nota de Mateo habla de un aullido junto a la cabaña.",
                "Ve a la tercera mesa y toma el revólver antes de buscar al lobo.");
            Night.BloodMoon(8);
            if (GameAudio.Get != null) GameAudio.Music(GameAudio.Get.bossMusic, 4);
            yield return hud.ChapterCard("Capítulo III", "Luna de sangre");
            hud.SetObjective("Toma el revólver de la tercera mesa");
            hud.SetMarker(AnchorOn("MESA 3", "Tercera mesa"), .6f);
            while (!Holding<VRRevolver>()) yield return null;
            hud.SetMarker(null);
            SetSpawner(Werewolf, true);
            yield return EncounterAndPage(Werewolf, 3, "Elimina al Hombre Lobo y consigue la última pieza");
            SetSpawner(Werewolf, false);

            // ---------- Dawn: into the cabin ----------
            Chapter = 4;
            hud.SetMarker(null);
            hud.SetObjective("");
            Night.Dawn(10);
            GameAudio.Music(null, 8);
            StartCoroutine(DeadReturnToEarth());
            yield return Say(
                "Las tres piezas de la libreta de Mateo apuntan a la cabaña.",
                "Entra y busca la última pista en su interior.");
            var door = SceneDoor.Current;
            if (door != null)
            {
                door.locked = false;
                hud.SetObjective("Entra en la cabaña");
                hud.SetMarker(door.transform, 2.4f);
                // The door loads the next scene.
                while (door != null && !door.Entered) yield return null;
                yield break;
            }
            yield return hud.ChapterCard("Shadowwood", "Fin");
            yield return new WaitForSeconds(2);
            if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
        }

        // ---------- Objectives ----------

        IEnumerator EncounterAndPage(string enemy, int pageNumber, string text)
        {
            if (!spawners.TryGetValue(enemy, out var spawner)) yield break;
            // The spawner fills the configured zone once; no replacement can inflate the kill target.
            while (!spawner.InitialWaveReady) yield return new WaitForSeconds(.25f);
            int count = spawner.EncounterCount, shown = -1;
            Transform center = spawner.spawnPoints != null && spawner.spawnPoints.Length > 0
                ? spawner.spawnPoints[0] : spawner.spawnPointsRoot;
            while (Kills(enemy) < count)
            {
                int done = Kills(enemy);
                if (done != shown)
                {
                    shown = done;
                    hud.SetObjective(count > 1 ? $"{text} ({done}/{count})" : text, done == 0);
                }
                hud.SetMarker(center, 2f);
                yield return new WaitForSeconds(.25f);
            }
            while (PagePickup.Collected < pageNumber)
            {
                hud.SetObjective($"Recoge la pieza de la libreta de tu hermano ({PagePickup.Collected}/3)");
                var page = NearestPage();
                hud.SetMarker(page != null ? page.transform : center, .9f);
                yield return new WaitForSeconds(.25f);
            }
            hud.SetMarker(null);
            yield return Say($"Encontraste una pieza de las notas de tu hermano ({pageNumber}/3).");
        }

        PagePickup NearestPage()
        {
            PagePickup best = null; float closest = float.MaxValue;
            foreach (var page in FindObjectsByType<PagePickup>())
            {
                float d = (page.transform.position - head.position).sqrMagnitude;
                if (d < closest) { closest = d; best = page; }
            }
            return best;
        }

        IEnumerator Say(params string[] lines)
        {
            narrating = true;
            yield return hud.Narrate(lines);
            narrating = false;
        }

        // Short line that does not interrupt the story.
        IEnumerator Aside(string line)
        {
            if (narrating) yield break;
            yield return Say(line);
        }

        void OnPlayerDied()
        {
            if (deathLineSaid) return;
            deathLineSaid = true;
            StartCoroutine(Aside("Todavía no. Mateo te necesita. Levántate."));
        }

        // ---------- Ambience ----------

        // Whispers from somewhere in the dark around the player, now and then (never during the dawn).
        IEnumerator Whispers()
        {
            var audio = GameAudio.Get;
            if (audio == null || audio.whispers == null) yield break;
            while (Chapter < 4)
            {
                yield return new WaitForSeconds(Random.Range(audio.whisperInterval.x, audio.whisperInterval.y));
                if (Chapter < 4) Whisper(Random.Range(4f, 8f));
            }
        }

        // A whisper at the given distance: behind the player when close, anywhere around when far.
        void Whisper(float distance)
        {
            var audio = GameAudio.Get;
            if (audio == null || head == null) return;
            var back = -Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            var direction = Quaternion.Euler(0, Random.Range(-70f, 70f), 0) * back;
            GameAudio.PlayAt(audio.whispers, head.position + direction * distance, .9f, Random.Range(.9f, 1.05f), 20);
        }

        // ---------- Enemies ----------

        IEnumerator TrackEnemies()
        {
            var wait = new WaitForSeconds(.5f);
            while (true)
            {
                living.Clear();
                foreach (var actor in FindObjectsByType<GoblinActor>())
                {
                    if (actor.settings == null || actor.Health == null) continue;
                    Watch(actor);
                    if (!actor.Health.IsDead) living.Add(actor);
                }
                yield return wait;
            }
        }

        int Kills(string enemy) => kills.TryGetValue(enemy, out int n) ? n : 0;

        void Watch(GoblinActor actor)
        {
            if (actor == null || actor.Health == null || actor.settings == null || !watched.Add(actor)) return;
            string enemy = actor.settings.displayName;
            actor.Health.Died += () =>
            {
                kills[enemy] = Kills(enemy) + 1;
                if (spawners.TryGetValue(enemy, out var spawner) && spawner.InitialWaveReady
                    && Kills(enemy) >= spawner.EncounterCount && dropped.Add(enemy))
                    PagePickup.TryDrop(actor.transform.position + actor.transform.forward * .5f, null);
            };
        }

        GoblinActor Nearest(string enemy)
        {
            GoblinActor best = null; float closest = float.MaxValue;
            foreach (var actor in living)
            {
                if (actor == null || actor.Health.IsDead || actor.settings.displayName != enemy) continue;
                float d = (actor.transform.position - head.position).sqrMagnitude;
                if (d < closest) { closest = d; best = actor; }
            }
            return best;
        }

        IEnumerator DeadReturnToEarth()
        {
            yield return new WaitForSeconds(3);
            foreach (var actor in FindObjectsByType<GoblinActor>())
                if (actor.settings != null && actor.settings.displayName == Zombie && !actor.Health.IsDead)
                {
                    actor.Health.TakeDamage(actor.Health.Current + 1);
                    yield return new WaitForSeconds(Random.Range(.2f, .6f));
                }
        }

        void SetSpawner(string enemy, bool on, float respawn = -1)
        {
            if (!spawners.TryGetValue(enemy, out var spawner)) { if (on) Debug.LogWarning("StoryDirector: no hay spawner de " + enemy); return; }
            spawner.enabled = on;
            spawner.respawnOverride = respawn;
            if (on) spawner.ResetCooldown();
        }

        // ---------- Places ----------

        // A weapon of that kind (VRAxe, VRBow, VRRevolver) in either hand.
        static bool Holding<T>() where T : Component
        {
            foreach (var hand in new[] { InteractorHandedness.Left, InteractorHandedness.Right, InteractorHandedness.None })
            {
                var grip = WeaponGrip.HeldIn(hand);
                if (grip != null && grip.GetComponent<T>() != null) return true;
            }
            return false;
        }

        // Point on top of a scene object (found by name) for the marker.
        static Transform AnchorOn(string objectName, string label)
        {
            var target = GameObject.Find(objectName);
            if (target == null) { Debug.LogWarning("StoryDirector: no se encontro " + objectName); return null; }
            var bounds = new Bounds(target.transform.position, Vector3.zero);
            foreach (var renderer in target.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            var anchor = new GameObject("Story Point: " + label).transform;
            anchor.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            anchor.localScale = bounds.size;
            return anchor;
        }

        static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }

        // ---------- Night ----------

        // The night gets darker and redder chapter by chapter, and lightens at dawn.
        static class Night
        {
            static Color fogColor, ambient;
            static float fogDensity, ambientIntensity;
            static bool fog;
            static Light sun;
            static Color sunColor;
            static float sunIntensity;
            static StoryDirector Runner => Current;

            public static void Capture()
            {
                fog = RenderSettings.fog; fogColor = RenderSettings.fogColor; fogDensity = RenderSettings.fogDensity;
                ambient = RenderSettings.ambientLight; ambientIntensity = RenderSettings.ambientIntensity;
                foreach (var light in Object.FindObjectsByType<Light>())
                    if (light.type == LightType.Directional && light.enabled) { sun = light; sunColor = light.color; sunIntensity = light.intensity; break; }
                RenderSettings.fog = true;
                if (!fog || RenderSettings.fogMode != FogMode.Exponential && RenderSettings.fogMode != FogMode.ExponentialSquared)
                { RenderSettings.fogMode = FogMode.Exponential; fogDensity = Mathf.Max(fogDensity, .015f); }
            }

            public static void Darken(float amount, float seconds) =>
                Blend(Color.Lerp(fogColor, Color.black, amount), fogDensity * (1 + amount * 1.5f), Color.Lerp(ambient, Color.black, amount * .6f), 1 - amount * .5f, seconds);

            public static void BloodMoon(float seconds) =>
                Blend(new Color(.18f, .02f, .02f), fogDensity * 2f, new Color(.25f, .05f, .05f), .8f, seconds, new Color(1f, .25f, .2f));

            public static void Dawn(float seconds) =>
                Blend(new Color(.55f, .55f, .6f), fogDensity * .7f, Color.Lerp(ambient, new Color(.55f, .5f, .5f), .6f), 1.6f, seconds, new Color(1f, .8f, .6f), 1.5f);

            static void Blend(Color toFog, float toDensity, Color toAmbient, float sunScale, float seconds, Color? toSun = null, float ambientScale = 1)
            {
                if (Runner != null) Runner.StartCoroutine(BlendRoutine(toFog, toDensity, toAmbient, sunScale, seconds, toSun, ambientScale));
            }

            static IEnumerator BlendRoutine(Color toFog, float toDensity, Color toAmbient, float sunScale, float seconds, Color? toSun, float ambientScale)
            {
                Color f0 = RenderSettings.fogColor, a0 = RenderSettings.ambientLight;
                float d0 = RenderSettings.fogDensity, i0 = RenderSettings.ambientIntensity;
                Color s0 = sun != null ? sun.color : Color.white; float si0 = sun != null ? sun.intensity : 0;
                for (float t = 0; t <= seconds; t += Time.deltaTime)
                {
                    float k = Mathf.SmoothStep(0, 1, t / seconds);
                    RenderSettings.fogColor = Color.Lerp(f0, toFog, k);
                    RenderSettings.fogDensity = Mathf.Lerp(d0, toDensity, k);
                    RenderSettings.ambientLight = Color.Lerp(a0, toAmbient, k);
                    RenderSettings.ambientIntensity = Mathf.Lerp(i0, ambientIntensity * ambientScale, k);
                    if (sun != null)
                    {
                        sun.color = Color.Lerp(s0, toSun ?? sunColor, k);
                        sun.intensity = Mathf.Lerp(si0, sunIntensity * sunScale, k);
                    }
                    yield return null;
                }
            }
        }
    }
}
