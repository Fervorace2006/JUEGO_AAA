using UnityEngine;
using UnityEngine.Rendering;

namespace ForestVR
{
    // Red blood stains around the edge of the view while enemies hit the player. They cover only the borders,
    // build up with each hit and fade away some seconds after the last one.
    // Screen-space canvases do not reach the headset, so the stains are quads parented to the camera.
    public sealed class BloodScreen : MonoBehaviour
    {
        const float Distance = 0.3f;
        // Splat spots around the border of the view (x, y in meters at Distance), away from the center.
        static readonly Vector2[] Spots =
        {
            new Vector2(-.26f, .16f), new Vector2(.26f, .16f), new Vector2(-.28f, -.12f), new Vector2(.28f, -.12f),
            new Vector2(-.12f, .24f), new Vector2(.12f, .24f), new Vector2(-.14f, -.24f), new Vector2(.14f, -.24f),
        };
        static Texture2D vignetteTexture;
        static Texture2D[] splatTextures;

        [SerializeField, Range(0, 1)] float maxVignetteAlpha = .6f;
        [SerializeField, Range(0, 1)] float maxSplatAlpha = .85f;
        [Tooltip("Seconds without being hit before the stains start to fade.")]
        [SerializeField, Min(0)] float holdSeconds = 2.5f;
        [SerializeField, Min(.1f)] float fadeSeconds = 2.5f;

        Health health;
        Material vignette;
        Material[] splats;
        Renderer vignetteRenderer;
        Renderer[] splatRenderers;
        float[] splatAlpha;
        float intensity, flash, lastHitAt = float.NegativeInfinity;
        Mesh quad;

        public static BloodScreen Create(Transform head, Health playerHealth)
        {
            var go = new GameObject("Blood Screen");
            go.transform.SetParent(head, false);
            var screen = go.AddComponent<BloodScreen>();
            screen.Build(playerHealth);
            return screen;
        }

        void Build(Health playerHealth)
        {
            health = playerHealth;
            health.Damaged += OnDamaged;
            health.onHealthChanged.AddListener(OnHealthChanged);
            if (vignetteTexture == null) CreateTextures();
            quad = new Mesh { name = "Blood quad" };
            quad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0), new Vector3(.5f, -.5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateBounds();
            // Just wider than the headset view: the clear center stays in front, the stained border lies in the periphery.
            vignette = CreateMaterial(vignetteTexture);
            vignetteRenderer = CreateQuad("Blood Vignette", vignette, new Vector3(0, 0, Distance), new Vector3(.8f, .76f, 1), 0);
            splats = new Material[Spots.Length];
            splatRenderers = new Renderer[Spots.Length];
            splatAlpha = new float[Spots.Length];
            for (int i = 0; i < Spots.Length; i++)
            {
                splats[i] = CreateMaterial(splatTextures[i % splatTextures.Length]);
                float size = Random.Range(.13f, .19f);
                splatRenderers[i] = CreateQuad("Blood Splat " + i, splats[i], new Vector3(Spots[i].x, Spots[i].y, Distance - .002f),
                    new Vector3(size, size, 1), Random.Range(0f, 360f));
            }
            Apply();
        }

        static Material CreateMaterial(Texture2D texture)
        {
            // Sprites/Default is unlit, alpha blended and always included in builds.
            var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, color = Color.clear };
            // Drawn after the scene but under the health bar (4000).
            material.renderQueue = 3990;
            return material;
        }

        Renderer CreateQuad(string name, Material material, Vector3 position, Vector3 scale, float roll)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, 0, roll);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return renderer;
        }

        void OnDamaged(Vector3 source)
        {
            lastHitAt = Time.time;
            intensity = Mathf.Min(1, intensity + .35f);
            flash = 1;
            // Stain the side the blow came from: one or two spots on that side, preferring clean ones.
            float side = Vector3.Dot(transform.right, source - transform.position) >= 0 ? 1 : -1;
            for (int n = 0; n < 2; n++)
            {
                int best = -1; float lowest = float.MaxValue;
                for (int i = 0; i < Spots.Length; i++)
                {
                    if (Mathf.Sign(Spots[i].x) != side) continue;
                    float score = splatAlpha[i] + Random.value * .3f;
                    if (score < lowest) { lowest = score; best = i; }
                }
                if (best >= 0) splatAlpha[best] = Mathf.Min(1, splatAlpha[best] + .7f);
            }
        }

        void OnHealthChanged(float value)
        {
            // Recovering the player's health clears the view.
            if (value >= health.Maximum) { intensity = 0; flash = 0; System.Array.Clear(splatAlpha, 0, splatAlpha.Length); Apply(); }
        }

        void Update()
        {
            if (intensity <= 0 && flash <= 0) return;
            flash = Mathf.MoveTowards(flash, 0, Time.deltaTime * 3);
            if (Time.time - lastHitAt > holdSeconds)
            {
                float step = Time.deltaTime / fadeSeconds;
                intensity = Mathf.MoveTowards(intensity, 0, step);
                for (int i = 0; i < splatAlpha.Length; i++) splatAlpha[i] = Mathf.MoveTowards(splatAlpha[i], 0, step);
            }
            Apply();
        }

        void Apply()
        {
            // A short pulse on each hit, then the stains settle to their level.
            float level = Mathf.Clamp01(intensity + flash * .25f);
            SetAlpha(vignette, level * maxVignetteAlpha, vignetteRenderer);
            for (int i = 0; i < splats.Length; i++) SetAlpha(splats[i], Mathf.Min(splatAlpha[i], level) * maxSplatAlpha, splatRenderers[i]);
        }

        static void SetAlpha(Material material, float alpha, Renderer renderer)
        {
            material.color = new Color(1, 1, 1, alpha);
            if (renderer != null) renderer.enabled = alpha > .001f;
        }

        static void CreateTextures()
        {
            // Border stain: clear center, blotchy dark red toward the edges.
            const int size = 256;
            vignetteTexture = NewTexture("Blood Vignette", size);
            var pixels = new Color32[size * size];
            float seed = Random.value * 100;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f) * 2 - 1, v = y / (size - 1f) * 2 - 1;
                    // Rounded-square distance so the corners stain first, like the edge of a lens.
                    float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 3) + Mathf.Pow(Mathf.Abs(v), 3), 1 / 3f);
                    float noise = Mathf.PerlinNoise(x * .035f + seed, y * .035f + seed) * .6f + Mathf.PerlinNoise(x * .11f + seed, y * .11f) * .4f;
                    float a = Mathf.Clamp01((r - .62f + (noise - .5f) * .35f) / .3f);
                    float dark = Mathf.Lerp(.75f, .35f, a);
                    pixels[y * size + x] = new Color(dark, .02f, .02f, a);
                }
            vignetteTexture.SetPixels32(pixels); vignetteTexture.Apply(false, true);

            // Splats: an irregular blob with droplets around it.
            splatTextures = new Texture2D[3];
            for (int t = 0; t < splatTextures.Length; t++)
            {
                const int s = 128;
                var tex = NewTexture("Blood Splat " + t, s);
                var px = new Color32[s * s];
                var drops = new Vector3[10];
                for (int d = 0; d < drops.Length; d++)
                {
                    var dir = Random.insideUnitCircle.normalized * Random.Range(.45f, .85f);
                    drops[d] = new Vector3(dir.x, dir.y, Random.Range(.05f, .13f));
                }
                float sd = Random.value * 100;
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = x / (s - 1f) * 2 - 1, v = y / (s - 1f) * 2 - 1;
                        float angle = Mathf.Atan2(v, u);
                        // Wobbly edge around the main blob.
                        float edge = .42f + (Mathf.PerlinNoise(Mathf.Cos(angle) * 1.5f + sd, Mathf.Sin(angle) * 1.5f + sd) - .5f) * .35f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float a = Mathf.Clamp01((edge - r) / .06f);
                        foreach (var drop in drops)
                        {
                            float dd = Vector2.Distance(new Vector2(u, v), new Vector2(drop.x, drop.y));
                            a = Mathf.Max(a, Mathf.Clamp01((drop.z - dd) / .03f));
                        }
                        float shade = Mathf.Lerp(.35f, .7f, Mathf.PerlinNoise(x * .08f + sd, y * .08f));
                        px[y * s + x] = new Color(shade, .01f, .015f, a * .95f);
                    }
                tex.SetPixels32(px); tex.Apply(false, true);
                splatTextures[t] = tex;
            }
        }

        static Texture2D NewTexture(string name, int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        void OnDestroy()
        {
            if (health != null) { health.Damaged -= OnDamaged; health.onHealthChanged.RemoveListener(OnHealthChanged); }
            if (vignette != null) Destroy(vignette);
            if (splats != null) foreach (var material in splats) if (material != null) Destroy(material);
            if (quad != null) Destroy(quad);
        }
    }
}
