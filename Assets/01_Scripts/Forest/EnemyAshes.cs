using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ForestVR
{
    // A dead enemy turns to ash: golden sparkles when it falls (a reward), then, once its death animation ends, the body
    // chars to ash grey and slowly crumbles away while its ashes drift up into the night sky.
    public sealed class EnemyAshes : MonoBehaviour
    {
        const float CharSeconds = 1.5f, CrumbleSeconds = 3.5f, AshLifetime = 6;
        static readonly Color Ash = new Color(.16f, .15f, .14f);
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), LegacyColor = Shader.PropertyToID("_Color");

        struct Part { public Material[] materials; public Color[] colors; public int[] properties; }
        readonly List<Part> parts = new List<Part>();
        Vector3 startScale;

        // Called when the enemy dies; `delay` is how long its death animation lasts. The enemy is destroyed at the end.
        public static void Begin(GameObject enemy, float delay)
        {
            var body = FindBounds(enemy);
            Sparkles.Burst(body.center + Vector3.up * body.extents.y * .3f, 45, Mathf.Max(.15f, body.extents.x * .5f));
            var ashes = enemy.AddComponent<EnemyAshes>();
            ashes.StartCoroutine(ashes.Run(delay));
        }

        IEnumerator Run(float delay)
        {
            yield return new WaitForSeconds(delay + .3f);
            startScale = transform.localScale;
            // Per-enemy copies of the materials, so only this body chars.
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is SkinnedMeshRenderer || renderer is MeshRenderer) || !renderer.enabled) continue;
                var part = new Part { materials = renderer.materials };
                part.colors = new Color[part.materials.Length];
                part.properties = new int[part.materials.Length];
                for (int i = 0; i < part.materials.Length; i++)
                {
                    var m = part.materials[i];
                    part.properties[i] = m.HasProperty(BaseColor) ? BaseColor : LegacyColor;
                    part.colors[i] = m.HasProperty(part.properties[i]) ? m.GetColor(part.properties[i]) : Color.white;
                }
                parts.Add(part);
            }
            var ashes = Ashes(FindBounds(gameObject));
            var emission = ashes.emission;

            // Charring: the colors sink into ash, the first ashes rise.
            emission.rateOverTime = 12;
            for (float t = 0; t < CharSeconds; t += Time.deltaTime)
            {
                Tint(Mathf.SmoothStep(0, 1, t / CharSeconds));
                yield return null;
            }
            Tint(1);

            // Crumbling: the body shrinks into the ground while most of the ashes fly off.
            emission.rateOverTime = 45;
            for (float t = 0; t < CrumbleSeconds; t += Time.deltaTime)
            {
                float k = t / CrumbleSeconds;
                transform.localScale = startScale * (1 - k * k);
                ashes.transform.position = FindBounds(gameObject).center;
                yield return null;
            }
            emission.rateOverTime = 0;
            // The last ashes keep rising on their own for a while.
            ashes.transform.SetParent(null, true);
            Destroy(ashes.gameObject, AshLifetime);
            Destroy(gameObject);
        }

        void Tint(float amount)
        {
            foreach (var part in parts)
                for (int i = 0; i < part.materials.Length; i++)
                    if (part.materials[i].HasProperty(part.properties[i]))
                        part.materials[i].SetColor(part.properties[i], Color.Lerp(part.colors[i], Ash * part.colors[i].a, amount));
        }

        // Grey flakes with a few glowing embers, drifting up and slightly sideways like smoke.
        ParticleSystem Ashes(Bounds body)
        {
            var system = Sparkles.Create("Cenizas", body.center, 1);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, AshLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .25f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.35f, .33f, .31f), new Color(.6f, .57f, .53f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.maxParticles = 250;
            main.gravityModifier = -.04f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.Max(body.size, Vector3.one * .2f);
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.15f, .15f);
            velocity.y = new ParticleSystem.MinMaxCurve(.35f, .8f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.15f, .15f);
            var noise = system.noise;
            noise.enabled = true; noise.strength = .25f; noise.frequency = .4f; noise.scrollSpeed = .3f;
            Sparkles.FadeOut(system, Color.white);
            system.Play();
            EmberBursts(system);
            return system;
        }

        // A few orange embers mixed in with the grey ash.
        static void EmberBursts(ParticleSystem system)
        {
            var parameters = new ParticleSystem.EmitParams { startColor = new Color(1f, .45f, .12f), startSize = .03f, applyShapeToPosition = true };
            system.Emit(parameters, 12);
        }

        static Bounds FindBounds(GameObject enemy)
        {
            var bounds = new Bounds(enemy.transform.position + Vector3.up * .5f, Vector3.one * .5f);
            bool first = true;
            foreach (var renderer in enemy.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is SkinnedMeshRenderer || renderer is MeshRenderer) || !renderer.enabled) continue;
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        void OnDestroy()
        {
            foreach (var part in parts)
                foreach (var material in part.materials)
                    if (material != null) Destroy(material);
        }
    }
}
