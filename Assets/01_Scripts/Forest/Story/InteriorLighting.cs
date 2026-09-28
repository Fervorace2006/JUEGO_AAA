using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ForestVR
{
    // Night inside the cabin (InteriorHouse): dark, lit by the torches someone kept burning on the walls (the light seen
    // from the forest), Mateo's lamp left on the table (a warm, softly flickering light with a visible glow) and a faint
    // cold glimmer of moonlight from above. The daylight
    // directional of the scene becomes a dim blue moon. Set up when the scene loads; the scene itself is not changed.
    public sealed class InteriorLighting : MonoBehaviour
    {
        const string SceneName = "InteriorHouse";
        static readonly Color Ambient = new Color(.05f, .045f, .055f), Background = new Color(.01f, .01f, .018f);
        static readonly Color LampColor = new Color(1f, .62f, .32f), MoonColor = new Color(.55f, .62f, .85f);
        const float LampIntensity = 1.1f, LampRange = 3.2f, LampHeight = .55f;
        const float GlimmerIntensity = .22f, MoonIntensity = .05f;

        Light lamp;
        Transform glow;
        Material glowMaterial;
        float seed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName) return;
            var go = new GameObject("Interior Lighting");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<InteriorLighting>().Build(scene);
        }

        void Build(Scene scene)
        {
            // Dark, slightly blue night air; no daylight sky.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(Ambient);
            RenderSettings.ambientProbe = probe;
            RenderSettings.reflectionIntensity = .1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Background;
            RenderSettings.fogDensity = .06f;
            foreach (var camera in FindObjectsByType<Camera>())
                if (camera.gameObject.scene == scene) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Background; }

            // The scene's daylight becomes a faint blue moon, without shadows.
            foreach (var light in FindObjectsByType<Light>())
                if (light.gameObject.scene == scene && light.type == LightType.Directional)
                { light.color = MoonColor; light.intensity = MoonIntensity; light.shadows = LightShadows.None; }

            var room = RoomBounds(scene);
            // Mateo's lamp on the table: warm and small, it barely reaches the walls.
            var table = FindByName(scene, "MESA");
            var lampPosition = table != null ? TopOf(table) + Vector3.up * LampHeight : room.center;
            lamp = new GameObject("Lampara de Mateo").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.position = lampPosition;
            lamp.type = LightType.Point; lamp.color = LampColor;
            lamp.intensity = LampIntensity; lamp.range = LampRange;
            lamp.shadows = LightShadows.None; lamp.renderMode = LightRenderMode.ForcePixel;
            glow = MakeGlow(lampPosition);

            // A cold glimmer from the roof, just enough to make out the corners.
            var glimmer = new GameObject("Luz de luna").AddComponent<Light>();
            glimmer.transform.SetParent(transform, false);
            glimmer.transform.position = new Vector3(room.center.x, room.max.y - .2f, room.center.z);
            glimmer.type = LightType.Point; glimmer.color = MoonColor;
            glimmer.intensity = GlimmerIntensity; glimmer.range = Mathf.Max(3, room.extents.magnitude * 1.2f);
            glimmer.shadows = LightShadows.None;
            seed = Random.value * 10;
            AddTorches(room);
        }

        // ---------- Torches ----------
        // One on the middle of each wall, at head height and a little away from it: a warm light that flickers and
        // now and then flares, a visible flame and embers drifting up.
        const float TorchHeight = 1.8f, TorchInset = .28f, TorchIntensity = 1.5f, TorchRange = 3.8f;
        static readonly Color TorchColor = new Color(1f, .5f, .18f);
        struct Torch { public Light light; public Transform flame; public float seed, flare; }
        readonly System.Collections.Generic.List<Torch> torches = new System.Collections.Generic.List<Torch>();
        Material flameMaterial, stickMaterial;

        void AddTorches(Bounds room)
        {
            flameMaterial = new Material(Sparkles.Material) { color = new Color(1f, .6f, .25f, .85f) };
            stickMaterial = new Material(Sparkles.Material) { mainTexture = null, color = new Color(.16f, .1f, .06f) };
            float y = room.min.y + TorchHeight;
            var c = room.center;
            var spots = new[]
            {
                new Vector3(room.min.x + TorchInset, y, c.z), new Vector3(room.max.x - TorchInset, y, c.z),
                new Vector3(c.x, y, room.min.z + TorchInset), new Vector3(c.x, y, room.max.z - TorchInset),
            };
            foreach (var spot in spots) AddTorch(spot, c);
        }

        void AddTorch(Vector3 position, Vector3 roomCenter)
        {
            var torch = new GameObject("Antorcha").transform;
            torch.SetParent(transform, false);
            torch.position = position;
            // The handle leans out from the wall toward the room.
            var outward = Vector3.ProjectOnPlane(roomCenter - position, Vector3.up).normalized;
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stick.GetComponent<Collider>());
            stick.name = "Mango";
            stick.transform.SetParent(torch, false);
            stick.transform.localScale = new Vector3(.04f, .16f, .04f);
            stick.transform.position = position - Vector3.up * .15f - outward * .04f;
            stick.transform.rotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up + outward * .35f).normalized);
            var stickRenderer = stick.GetComponent<MeshRenderer>();
            stickRenderer.sharedMaterial = stickMaterial; stickRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var flame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(flame.GetComponent<Collider>());
            flame.name = "Llama";
            flame.transform.SetParent(torch, false);
            flame.transform.position = position;
            flame.transform.localScale = new Vector3(.22f, .3f, 1);
            var flameRenderer = flame.GetComponent<MeshRenderer>();
            flameRenderer.sharedMaterial = flameMaterial; flameRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(torch, false);
            light.transform.position = position + outward * .1f;
            light.type = LightType.Point; light.color = TorchColor;
            light.intensity = TorchIntensity; light.range = TorchRange;
            light.shadows = LightShadows.None;

            // Embers rising from the flame.
            var embers = Sparkles.Create("Chispas", position + Vector3.up * .08f, .05f);
            embers.transform.SetParent(torch, true);
            var main = embers.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.15f, .45f);
            main.startSize = new ParticleSystem.MinMaxCurve(.012f, .03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .45f, .1f), new Color(1f, .8f, .35f));
            main.gravityModifier = -.2f;
            main.maxParticles = 30;
            var emission = embers.emission;
            emission.enabled = true; emission.rateOverTime = 6;
            Sparkles.FadeOut(embers, Color.white);
            embers.Play();

            torches.Add(new Torch { light = light, flame = flame.transform, seed = Random.value * 50 });
        }

        void UpdateTorches(Camera eyes)
        {
            for (int i = 0; i < torches.Count; i++)
            {
                var torch = torches[i];
                if (torch.light == null) continue;
                float t = Time.time + torch.seed;
                // Now and then a flare: the fire catches and the room brightens for a moment.
                if (torch.flare <= 0 && Random.value < Time.deltaTime * .08f) torch.flare = 1;
                torch.flare = Mathf.MoveTowards(torch.flare, 0, Time.deltaTime * 1.5f);
                float flicker = .8f + (Mathf.PerlinNoise(t * 2.2f, 1) - .5f) * .35f + (Mathf.PerlinNoise(t * 13f, 7) - .5f) * .2f;
                torch.light.intensity = TorchIntensity * (flicker + torch.flare * .6f);
                if (torch.flame != null)
                {
                    if (eyes != null) torch.flame.rotation = Quaternion.LookRotation(torch.flame.position - eyes.transform.position);
                    torch.flame.localScale = new Vector3(.22f, .3f, 1) * (.9f + flicker * .15f + torch.flare * .25f);
                }
                torches[i] = torch;
            }
        }

        // The flame breathes: small, slow changes with an occasional quick flicker.
        void Update()
        {
            UpdateTorches(Camera.main);
            if (lamp == null) return;
            float t = Time.time + seed;
            float flicker = .88f + (Mathf.PerlinNoise(t * 1.3f, 0) - .5f) * .25f + (Mathf.PerlinNoise(t * 9f, 3) - .5f) * .12f;
            lamp.intensity = LampIntensity * flicker;
            if (glow != null)
            {
                var eyes = Camera.main;
                if (eyes != null) glow.rotation = Quaternion.LookRotation(glow.position - eyes.transform.position);
                glowMaterial.color = new Color(LampColor.r, LampColor.g, LampColor.b, Mathf.Clamp01(.55f * flicker));
            }
        }

        // The visible flame of the lamp: a soft warm dot that always faces the eyes.
        Transform MakeGlow(Vector3 position)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Llama";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.position = position;
            quad.transform.localScale = Vector3.one * .18f;
            glowMaterial = new Material(Sparkles.Material);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glowMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return quad.transform;
        }

        static Bounds RoomBounds(Scene scene)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.ToLowerInvariant().StartsWith("pared_de_madera")) continue;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                }
            }
            if (first) bounds = new Bounds(Vector3.up * 1.5f, Vector3.one * 6);
            return bounds;
        }

        static Transform FindByName(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root.transform;
            return null;
        }

        static Vector3 TopOf(Transform table)
        {
            var bounds = new Bounds(table.position, Vector3.zero);
            bool first = true;
            foreach (var renderer in table.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        void OnDestroy()
        {
            if (glowMaterial != null) Destroy(glowMaterial);
            if (flameMaterial != null) Destroy(flameMaterial);
            if (stickMaterial != null) Destroy(stickMaterial);
        }
    }
}
