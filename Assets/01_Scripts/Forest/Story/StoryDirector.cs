using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ForestVR
{
    // SHADOWWOOD — the story of the forest, told in chapters that drive the game:
    //   Prologue      Mateo vanished three nights ago. Take the hunter's weapons from the table.
    //   I   Sleepers  Reach the lit cabin without waking the goblins (they only wake if you get very close).
    //   II  Diary     The goblins stole the diary pages that tell how to break the curse: kill 3 goblins.
    //   III Restless  Spilled blood wakes the dead: zombies rise without pause until 5 of them fall.
    //   IV  Blood moon The hunter himself is the werewolf. Kill him.
    //   Dawn          The dead return to the earth, Mateo is alive, back to the menu.
    // Each chapter switches the enemy spawners on or off, changes how fast they come back, darkens or lights the night,
    // and points the objective marker at the place or the nearest enemy to deal with.
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
            foreach (var spawner in FindObjectsByType<GoblinSpawner>())
                if (spawner.settings != null) spawners[spawner.settings.displayName] = spawner;
            SetSpawner(Zombie, false);
            SetSpawner(Werewolf, false);
        }
        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (player != null) player.Died -= OnPlayerDied;
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
                "Su linterna apareció aquí, junto a la mesa del viejo cazador Elías Varga... apagada.",
                "El cazador dejó sus armas sobre la mesa. Vas a necesitarlas.");
            hud.SetObjective("Toma un arma de la mesa del cazador");
            hud.SetMarker(AnchorOn("MESA", "Mesa del cazador"), .6f);
            while (!HoldingWeapon()) yield return null;

            // ---------- I ----------
            Chapter = 1;
            hud.SetObjective("");
            hud.SetMarker(null);
            yield return hud.ChapterCard("Capítulo I", "Los que duermen");
            yield return Say(
                "Los duendes duermen entre los árboles. Solo despiertan si te acercas demasiado.",
                "En la cabaña del cazador hay luz. Alguien la ha encendido esta noche.");
            hud.SetObjective("Llega a la cabaña sin despertar a los duendes");
            var cabin = AnchorOn("Casita (1)", "Cabana del cazador");
            hud.SetMarker(cabin, 1.2f);
            bool warned = false;
            while (cabin != null && FlatDistance(head.position, cabin.position) > CabinReach(cabin))
            {
                if (!warned && AnyAwake(Goblin)) { warned = true; StartCoroutine(Aside("Te han oído. Muévete, o pelea.")); }
                yield return null;
            }

            // ---------- II ----------
            Chapter = 2;
            hud.SetMarker(null);
            hud.SetObjective("");
            yield return hud.ChapterCard("Capítulo II", "El diario del cazador");
            yield return Say(
                "La puerta está abierta. Sobre la mesa, el diario de Elías, con páginas arrancadas.",
                "«Los duendes se llevaron las páginas que dicen cómo romper la maldición. Las guardan en sus harapos.»",
                "Sin esas páginas no sabrás qué le pasó a Mateo.");
            Night.Darken(.25f, 10);
            yield return KillObjective(Goblin, 3, "Recupera las páginas: elimina duendes", 1.9f,
                n => n < 3 ? "Una página manchada de sangre. «...la luna llena...»" : null);

            // ---------- III ----------
            Chapter = 3;
            hud.SetMarker(null);
            hud.SetObjective("");
            yield return Say(
                "La última página tiembla en tus manos: «Cuando la sangre del bosque se derrama, los muertos despiertan».",
                "Detrás de ti, la tierra se abre.");
            yield return hud.ChapterCard("Capítulo III", "Los que no descansan");
            // The dead keep coming, a few at a time, until the chapter is over.
            SetSpawner(Zombie, true, 6);
            Night.Darken(.5f, 12);
            yield return KillObjective(Zombie, 5, "Sobrevive a los muertos: elimina zombis", 2.2f,
                n => n == 2 ? "Siguen saliendo de la tierra. No dejes que te rodeen." : null);
            SetSpawner(Zombie, false);

            // ---------- IV ----------
            Chapter = 4;
            hud.SetMarker(null);
            hud.SetObjective("");
            yield return Say(
                "El bosque se queda en silencio. Un aullido rompe la noche.",
                "Otra página, escrita con una letra que ya no parece humana:",
                "«Si lees esto, ya no soy un hombre. Mateo está a salvo en el sótano de la cabaña. Mátame antes de que lo recuerde.» — E. Varga");
            Night.BloodMoon(8);
            yield return hud.ChapterCard("Capítulo IV", "Luna de sangre");
            SetSpawner(Werewolf, true, 99999);
            yield return KillObjective(Werewolf, 1, "Acaba con el Hombre Lobo", 3.2f, null);
            SetSpawner(Werewolf, false);

            // ---------- Dawn ----------
            Chapter = 5;
            hud.SetMarker(null);
            hud.SetObjective("");
            if (spawners.TryGetValue(Goblin, out var goblins)) goblins.respawnOverride = 99999;
            Night.Dawn(10);
            StartCoroutine(DeadReturnToEarth());
            yield return Say(
                "El aullido se apaga. La luna se esconde entre los árboles y los muertos vuelven a la tierra.",
                "Desde la cabaña, una voz débil pronuncia tu nombre.",
                "Mateo está vivo. Shadowwood, por ahora, duerme.");
            yield return hud.ChapterCard("Shadowwood", "Fin");
            yield return new WaitForSeconds(2);
            if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
        }

        // ---------- Objectives ----------

        IEnumerator KillObjective(string enemy, int count, string text, float markerHeight, System.Func<int, string> onKill)
        {
            int start = Kills(enemy), shown = -1;
            while (Kills(enemy) - start < count)
            {
                int done = Kills(enemy) - start;
                if (done != shown)
                {
                    if (shown >= 0) { var line = onKill?.Invoke(done); if (line != null) StartCoroutine(Aside(line)); }
                    shown = done;
                    hud.SetObjective(count > 1 ? $"{text} ({done}/{count})" : text, done == 0);
                }
                var nearest = Nearest(enemy);
                hud.SetMarker(nearest != null ? nearest.transform : null, markerHeight);
                yield return new WaitForSeconds(.25f);
            }
            hud.SetObjective(count > 1 ? $"{text} ({count}/{count})" : text, false);
            yield return new WaitForSeconds(1.5f);
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
                    if (watched.Add(actor))
                    {
                        string name = actor.settings.displayName;
                        actor.Health.Died += () => kills[name] = Kills(name) + 1;
                    }
                    if (!actor.Health.IsDead) living.Add(actor);
                }
                yield return wait;
            }
        }

        int Kills(string enemy) => kills.TryGetValue(enemy, out int n) ? n : 0;

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

        bool AnyAwake(string enemy)
        {
            foreach (var actor in living)
                if (actor != null && actor.settings.displayName == enemy && actor.CurrentState != GoblinActor.State.Resting) return true;
            return false;
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

        static bool HoldingWeapon() =>
            WeaponGrip.HeldIn(InteractorHandedness.Left) != null || WeaponGrip.HeldIn(InteractorHandedness.Right) != null
            || WeaponGrip.HeldIn(InteractorHandedness.None) != null;

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

        static float CabinReach(Transform cabin) => Mathf.Max(cabin.localScale.x, cabin.localScale.z) * .5f + 2.5f;
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
