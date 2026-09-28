using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForestVR
{
    // Aiming aid drawn in the world: a thin line along the path a shot will follow and a glowing dot where it will hit.
    // Straight for the revolver (bullets fly without gravity), a falling arc for the bow, using the same speed and
    // gravity as WeaponProjectile, so the dot is where the arrow really lands.
    public sealed class AimGuide : MonoBehaviour
    {
        const int MaxPoints = 90;
        static Material sharedMaterial;
        static Texture2D dotTexture;

        LineRenderer line;
        Transform dot;
        Material dotMaterial;
        Color color;
        readonly List<Vector3> points = new List<Vector3>(MaxPoints);
        readonly Vector3[] buffer = new Vector3[MaxPoints];
        Transform[] ignored = new Transform[0];

        public static AimGuide Create(Transform owner, Color color, float width)
        {
            var guide = new GameObject("Aim Guide").AddComponent<AimGuide>();
            guide.transform.SetParent(owner, false);
            guide.color = color;
            if (sharedMaterial == null) sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Aim Guide" };
            if (dotTexture == null) dotTexture = RadialTexture();

            guide.line = guide.gameObject.AddComponent<LineRenderer>();
            guide.line.useWorldSpace = true;
            guide.line.sharedMaterial = sharedMaterial;
            guide.line.widthCurve = new AnimationCurve(new Keyframe(0, width), new Keyframe(1, width * .5f));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) },
                new[] { new GradientAlphaKey(.05f, 0), new GradientAlphaKey(.8f, .08f), new GradientAlphaKey(.35f, 1) });
            guide.line.colorGradient = gradient;
            guide.line.numCapVertices = 2;
            guide.line.shadowCastingMode = ShadowCastingMode.Off; guide.line.receiveShadows = false;
            guide.line.lightProbeUsage = LightProbeUsage.Off; guide.line.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            dot.name = "Aim Dot";
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(guide.transform, false);
            guide.dotMaterial = new Material(sharedMaterial) { mainTexture = dotTexture, color = new Color(color.r, color.g, color.b, .95f), renderQueue = 3100 };
            var renderer = dot.GetComponent<Renderer>();
            renderer.sharedMaterial = guide.dotMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            guide.dot = dot.transform;
            guide.Hide();
            return guide;
        }

        // Colliders of these objects (the weapon, the player) never stop the guide.
        public void Ignore(params Transform[] roots) => ignored = roots;

        public void Hide()
        {
            if (line.enabled) line.enabled = false;
            if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
        }

        // Straight ray: the revolver.
        public void ShowStraight(Vector3 origin, Vector3 direction, float maxDistance)
        {
            points.Clear();
            points.Add(origin);
            if (Cast(origin, direction.normalized * maxDistance, out var hit)) { points.Add(hit.point); Show(hit.point, hit.normal); }
            else { points.Add(origin + direction.normalized * maxDistance); Show(null, Vector3.zero); }
        }

        // Ballistic arc: the bow. Same integration as WeaponProjectile, in coarser steps.
        public void ShowArc(Vector3 origin, Vector3 velocity, float gravity, float maxSeconds = 3)
        {
            points.Clear();
            points.Add(origin);
            var position = origin;
            const float step = .04f;
            for (float t = 0; t < maxSeconds && points.Count < MaxPoints; t += step)
            {
                var nextVelocity = velocity + Vector3.down * gravity * step;
                var next = position + (velocity + nextVelocity) * (.5f * step);
                if (Cast(position, next - position, out var hit)) { points.Add(hit.point); Show(hit.point, hit.normal); return; }
                points.Add(next);
                position = next; velocity = nextVelocity;
            }
            Show(null, Vector3.zero);
        }

        void Show(Vector3? hitPoint, Vector3 normal)
        {
            points.CopyTo(0, buffer, 0, points.Count);
            line.positionCount = points.Count;
            line.SetPositions(buffer);
            line.enabled = true;
            bool hit = hitPoint.HasValue;
            dot.gameObject.SetActive(hit);
            if (!hit) return;
            var camera = Camera.main;
            // Just off the surface, facing the eyes, the same apparent size near or far.
            dot.position = hitPoint.Value + normal * .01f;
            if (camera != null)
            {
                var toEye = camera.transform.position - dot.position;
                dot.rotation = Quaternion.LookRotation(-toEye);
                // The guide hangs under a scaled weapon: undo that scale.
                dot.localScale = Vector3.one * (Mathf.Clamp(toEye.magnitude * .012f, .03f, .6f) / Mathf.Max(.0001f, transform.lossyScale.x));
            }
        }

        readonly RaycastHit[] hits = new RaycastHit[16];
        bool Cast(Vector3 from, Vector3 delta, out RaycastHit best)
        {
            best = default;
            float distance = delta.magnitude;
            if (distance < .0001f) return false;
            int count = Physics.RaycastNonAlloc(from, delta / distance, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float closest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance >= closest || IsIgnored(hits[i].transform)) continue;
                closest = hits[i].distance; best = hits[i];
            }
            return closest < float.MaxValue;
        }

        bool IsIgnored(Transform t)
        {
            foreach (var root in ignored) if (root != null && t.IsChildOf(root)) return true;
            return false;
        }

        static Texture2D RadialTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f) * 2 - 1, v = y / (size - 1f) * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v);
                    // Bright core with a soft glow and a thin ring.
                    float a = Mathf.Clamp01(1 - r / .35f) + Mathf.Pow(Mathf.Clamp01(1 - r), 2) * .5f + Mathf.Clamp01(1 - Mathf.Abs(r - .8f) / .06f) * .6f;
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(a)));
                }
            texture.Apply(false, true);
            return texture;
        }

        void OnDestroy() { if (dotMaterial != null) Destroy(dotMaterial); }
    }
}
