using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ForestVR
{
    // Red arrow floating over every living enemy, seen from afar and through the trees, so the player always knows where
    // each one (re)appeared. Same apparent size at any distance; hidden up close, where its nameplate takes over, and
    // gone when it dies.
    public sealed class EnemyBeacon : MonoBehaviour
    {
        const float AngularSize = 2.2f, HideCloserThan = 4, Lift = .7f;
        static Texture2D arrow;
        static Material overlay;

        GoblinActor actor;
        Transform viewer;
        RawImage image;
        float headHeight, phase;

        public static void Attach(GoblinActor actor, Transform viewer)
        {
            if (actor == null) return;
            var go = new GameObject("Enemy Beacon");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(100, 100);
            if (arrow == null) arrow = ArrowTexture();
            if (overlay == null)
            {
                // Drawn over trees and rocks.
                overlay = new Material(Canvas.GetDefaultCanvasMaterial());
                overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            }
            var beacon = go.AddComponent<EnemyBeacon>();
            beacon.actor = actor; beacon.viewer = viewer;
            beacon.image = new GameObject("Arrow", typeof(RectTransform)).AddComponent<RawImage>();
            beacon.image.rectTransform.SetParent(rect, false);
            beacon.image.rectTransform.sizeDelta = rect.sizeDelta;
            beacon.image.texture = arrow; beacon.image.material = overlay; beacon.image.raycastTarget = false;
            beacon.image.color = new Color(1f, .15f, .1f, .9f);
            var body = actor.GetComponent<CharacterController>();
            beacon.headHeight = (body != null ? body.center.y + body.height * .5f : 1.6f) + Lift;
            beacon.phase = Random.value * 6;
            beacon.LateUpdate();
        }

        void LateUpdate()
        {
            if (actor == null) { Destroy(gameObject); return; }
            if (viewer == null) { var eyes = Camera.main; if (eyes == null) { image.enabled = false; return; } viewer = eyes.transform; }
            bool dead = actor.Health == null || actor.Health.IsDead;
            var position = actor.transform.position + Vector3.up * (headHeight + Mathf.Sin(Time.time * 3 + phase) * .08f);
            var away = position - viewer.position;
            float distance = away.magnitude;
            bool show = !dead && distance >= HideCloserThan;
            if (image.enabled != show) image.enabled = show;
            if (!show) return;
            // Faces the eyes and keeps the same size on screen near and far.
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(away / distance, Vector3.up));
            float size = 2 * distance * Mathf.Tan(AngularSize * .5f * Mathf.Deg2Rad);
            transform.localScale = Vector3.one * (size / 100);
        }

        // Downward-pointing arrowhead with a soft glow.
        static Texture2D ArrowTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size;
                    // Triangle: point at the bottom (v = .1), wide at the top (v = .9).
                    float half = Mathf.InverseLerp(.1f, .9f, v) * .8f;
                    float inside = Mathf.Min(half - Mathf.Abs(u), Mathf.Min(v - .1f, .9f - v));
                    float a = Mathf.Clamp01(inside * 20 + .5f);
                    float glow = Mathf.Clamp01(.35f + inside * 4) * .45f;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Max(a, inside > -.12f ? glow : 0));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
