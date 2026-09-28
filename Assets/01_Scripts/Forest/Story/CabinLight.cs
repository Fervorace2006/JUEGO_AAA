using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForestVR
{
    // The light of the cabin, off until the story turns it on: a lantern on the porch that lights the ground in front
    // of the door and glows through the fog from far away, and a warm light inside that spills out of the door and
    // windows (it casts shadows, so it only leaves the cabin through the openings). It stutters when it comes on
    // and then flickers like a flame.
    public sealed class CabinLight : MonoBehaviour
    {
        static readonly Color Flame = new Color(1f, .62f, .28f);
        const float PorchIntensity = 6, InsideIntensity = 5;

        Light porch, inside;
        Renderer bulb, halo;
        Material bulbMaterial, haloMaterial;
        float level;
        bool on;

        public bool IsOn => on;

        // Built at the cabin, next to its door. Returns null when the cabin is not in the scene.
        public static CabinLight Create(GameObject cabin, Transform door)
        {
            if (cabin == null) return null;
            var bounds = new Bounds(cabin.transform.position, Vector3.zero);
            foreach (var r in cabin.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var light = new GameObject("Luz de la cabaña").AddComponent<CabinLight>();
            light.transform.position = bounds.center;

            // Porch lantern: above the door, a little toward the wall.
            var doorPosition = door != null ? door.position : new Vector3(bounds.max.x, bounds.min.y, bounds.center.z);
            var towardCabin = new Vector3(bounds.center.x - doorPosition.x, 0, bounds.center.z - doorPosition.z).normalized;
            var lantern = doorPosition + towardCabin * .5f + Vector3.up * 2.3f;
            light.porch = light.NewLight("Farol del porche", lantern, 9, LightShadows.None);
            light.inside = light.NewLight("Luz interior", new Vector3(bounds.center.x, bounds.min.y + 1.8f, bounds.center.z),
                Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.4f, LightShadows.Soft);

            // The flame itself and a glow around it, unlit so they read as a light source through the fog.
            light.bulb = light.Quad(lantern, .18f, RadialTexture(.45f), out light.bulbMaterial);
            light.halo = light.Quad(lantern, 1.6f, RadialTexture(1f), out light.haloMaterial);
            light.Apply(0);
            return light;
        }

        Light NewLight(string name, Vector3 position, float range, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = Flame; l.range = range; l.intensity = 0;
            l.shadows = shadows; l.shadowStrength = .9f;
            l.renderMode = LightRenderMode.ForcePixel;
            l.enabled = false;
            return l;
        }

        Renderer Quad(Vector3 position, float size, Texture2D texture, out Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Brillo del farol";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, renderQueue = 3100 };
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        public void TurnOn()
        {
            if (on) return;
            on = true;
            StartCoroutine(Ignite());
        }

        // Catches, dies, catches again, then settles.
        IEnumerator Ignite()
        {
            float[] steps = { .5f, 0, .8f, .1f, .35f, 0, 1 };
            float[] waits = { .08f, .25f, .06f, .12f, .1f, .4f, 0 };
            for (int i = 0; i < steps.Length; i++)
            {
                level = steps[i];
                Apply(level);
                if (waits[i] > 0) yield return new WaitForSeconds(waits[i]);
            }
        }

        void Update()
        {
            if (!on || level < 1) return;
            // A living flame: slow sway plus small fast flickers.
            float t = Time.time;
            float flicker = .85f + (Mathf.PerlinNoise(t * 2.1f, 0) - .5f) * .25f + (Mathf.PerlinNoise(t * 13, 5) - .5f) * .12f;
            Apply(flicker);
        }

        void LateUpdate()
        {
            // The glow always faces the viewer.
            var camera = Camera.main;
            if (camera == null || !halo.enabled) return;
            var look = Quaternion.LookRotation(halo.transform.position - camera.transform.position);
            halo.transform.rotation = look; bulb.transform.rotation = look;
        }

        void Apply(float value)
        {
            bool lit = value > .01f;
            porch.enabled = inside.enabled = halo.enabled = bulb.enabled = lit;
            porch.intensity = PorchIntensity * value;
            inside.intensity = InsideIntensity * value;
            bulbMaterial.color = new Color(1f, .85f, .55f, Mathf.Clamp01(value * 1.2f));
            haloMaterial.color = new Color(Flame.r, Flame.g, Flame.b, .55f * value);
        }

        static Texture2D RadialTexture(float softness)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f) * 2 - 1, v = y / (size - 1f) * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v);
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 1 + softness * 2)));
                }
            texture.Apply(false, true);
            return texture;
        }

        void OnDestroy()
        {
            if (bulbMaterial != null) Destroy(bulbMaterial);
            if (haloMaterial != null) Destroy(haloMaterial);
        }
    }
}
