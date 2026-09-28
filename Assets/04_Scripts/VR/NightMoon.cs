using UnityEngine;
using UnityEngine.Rendering;

namespace JuegoAAA.VR
{
    /// <summary>Moonlight over the whole forest: a soft blue directional light without shadows (cheap on the Quest),
    /// and a glowing moon in the sky that stays at the horizon distance whatever the player does. The story can tint
    /// and dim it (it is the scene's directional light: blood moon, dawn), and the disc takes the light's color.</summary>
    public sealed class NightMoon : MonoBehaviour
    {
        const float Elevation = 32, Azimuth = 215, AngularSize = 5;
        static readonly Color MoonColor = new Color(.62f, .7f, .95f);
        const float MoonIntensity = .35f;

        Transform viewer, disc, halo;
        Light moonlight;
        Material discMaterial, haloMaterial;
        Texture2D discTexture, haloTexture;
        Vector3 toMoon;

        public static NightMoon Create(Transform camera)
        {
            var moon = new GameObject("Night Moon").AddComponent<NightMoon>();
            moon.viewer = camera;
            moon.Build();
            return moon;
        }

        void Build()
        {
            var direction = Quaternion.Euler(-Elevation, Azimuth, 0);
            toMoon = direction * Vector3.forward;

            moonlight = gameObject.AddComponent<Light>();
            moonlight.type = LightType.Directional;
            moonlight.color = MoonColor;
            moonlight.intensity = MoonIntensity;
            moonlight.shadows = LightShadows.None;
            moonlight.bounceIntensity = 0;
            transform.rotation = Quaternion.LookRotation(-toMoon);

            // Sprites/Default is unlit, ignores fog and is always included in builds.
            var shader = Shader.Find("Sprites/Default");
            discTexture = RadialTexture(128, sharp: true);
            haloTexture = RadialTexture(128, sharp: false);
            discMaterial = new Material(shader) { mainTexture = discTexture };
            haloMaterial = new Material(shader) { mainTexture = haloTexture };
            halo = Quad("Moon Halo", haloMaterial);
            disc = Quad("Moon Disc", discMaterial);
        }

        Transform Quad(string name, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad.transform;
        }

        // The disc sits far away in the moon's direction from the eyes, so walking never brings it closer.
        void LateUpdate()
        {
            if (viewer == null) return;
            var camera = viewer.GetComponent<Camera>();
            float distance = camera != null ? camera.farClipPlane * .8f : 400;
            float size = 2 * distance * Mathf.Tan(AngularSize * .5f * Mathf.Deg2Rad);
            var center = viewer.position + toMoon * distance;
            var facing = Quaternion.LookRotation(toMoon);
            disc.SetPositionAndRotation(center, facing);
            disc.localScale = Vector3.one * size;
            halo.SetPositionAndRotation(center + toMoon * .5f, facing);
            halo.localScale = Vector3.one * size * 4;
            // A blood moon turns the disc red too; brightness follows the light.
            var tint = Color.Lerp(Color.white, moonlight.color, .6f);
            float glow = Mathf.Clamp01(moonlight.intensity / MoonIntensity);
            discMaterial.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(.35f, 1, glow));
            haloMaterial.color = new Color(tint.r, tint.g, tint.b, .22f * glow);
        }

        static Texture2D RadialTexture(int size, bool sharp)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f) * 2 - 1, v = y / (size - 1f) * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a;
                    if (sharp)
                    {
                        // Moon disc with soft edge and faint darker patches.
                        a = Mathf.Clamp01((1 - r) * 18);
                        float patches = Mathf.PerlinNoise(u * 3 + 5, v * 3 + 5);
                        float shade = Mathf.Lerp(.82f, 1, patches);
                        pixels[y * size + x] = new Color(shade, shade, shade * 1.02f, a);
                        continue;
                    }
                    a = Mathf.Pow(Mathf.Clamp01(1 - r), 2.5f);
                    pixels[y * size + x] = new Color(1, 1, 1, a);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void OnDestroy()
        {
            Destroy(discMaterial); Destroy(haloMaterial);
            Destroy(discTexture); Destroy(haloTexture);
        }
    }
}
