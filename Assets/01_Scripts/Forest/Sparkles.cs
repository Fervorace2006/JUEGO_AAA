using UnityEngine;
using UnityEngine.Rendering;

namespace ForestVR
{
    // Particle effects built in code: a golden burst of sparkles (a reward: a notebook piece taken, an enemy down) and
    // the particle systems of the enemies' ashes. Sprites/Default is unlit, ignores fog and is always in the build.
    public static class Sparkles
    {
        static Texture2D dot;
        static Material material;

        public static Material Material
        {
            get
            {
                if (material == null)
                {
                    dot = SoftDot();
                    material = new Material(Shader.Find("Sprites/Default")) { name = "Sparkles", mainTexture = dot };
                }
                return material;
            }
        }

        // A short golden burst that rises and fades, then removes itself.
        public static void Burst(Vector3 position, int count = 40, float radius = .15f)
        {
            var system = Create("Sparkles", position, radius);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.03f, .07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .85f, .35f), new Color(1f, .97f, .75f));
            main.gravityModifier = -.15f;
            main.maxParticles = count;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            FadeOut(system, new Color(1, .9f, .5f));
            // Twinkle: size pulses as they fade.
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .4f), new Keyframe(.2f, 1), new Keyframe(.5f, .6f), new Keyframe(.7f, 1), new Keyframe(1, 0)));
            system.Emit(count);
            Object.Destroy(system.gameObject, 2f);
        }

        // Configured, stopped particle system in world space with the soft dot; the caller emits.
        public static ParticleSystem Create(string name, Vector3 position, float radius)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = true;
            shape.radius = radius;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
            return system;
        }

        public static void FadeOut(ParticleSystem system, Color tint)
        {
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(tint, 0), new GradientColorKey(tint, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
        }

        static Texture2D SoftDot()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                    float a = Mathf.Pow(Mathf.Clamp01(1 - Mathf.Sqrt(u * u + v * v)), 1.8f);
                    pixels[y * size + x] = new Color(1, 1, 1, a);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
