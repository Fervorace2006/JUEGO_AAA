using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ForestVR
{
    // Story text for VR: chapter cards, typewritten narration and the current objective on a panel that gently
    // follows the head (a panel locked to the head is tiring to read), plus a marker over the objective in the world.
    // Everything is drawn over the scene so trees and hands never cover it.
    public sealed class StoryHud : MonoBehaviour
    {
        const float Distance = 1.3f, PixelsPerMeter = 1000;
        static readonly Color Bone = new Color(.9f, .86f, .77f);
        static readonly Color Blood = new Color(.75f, .06f, .04f);

        Transform head;
        CanvasGroup titleGroup, narrationGroup;
        Text title, subtitle, narration, objective;
        Material overlay;
        Font titleFont, textFont;
        AudioSource audioSource;
        AudioClip toll;
        StoryMarker marker;

        public static StoryHud Create(Transform head)
        {
            var hud = new GameObject("Story HUD").AddComponent<StoryHud>();
            hud.head = head;
            hud.Build();
            return hud;
        }

        void Build()
        {
            titleFont = Resources.Load<Font>("Fonts/CinzelDecorative-Bold");
            textFont = Resources.Load<Font>("Fonts/Cinzel");
            // UI material that ignores depth: always readable over the scene.
            overlay = new Material(Canvas.GetDefaultCanvasMaterial());
            overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(1100, 800);
            rect.localScale = Vector3.one / PixelsPerMeter;
            Follow(true);

            titleGroup = Group("Chapter Card");
            title = Label(titleGroup.transform, "Title", titleFont, 84, new Vector2(0, 130), new Vector2(1100, 120), Blood);
            subtitle = Label(titleGroup.transform, "Subtitle", textFont, 44, new Vector2(0, 40), new Vector2(1100, 70), Bone);
            titleGroup.alpha = 0;

            narrationGroup = Group("Narration");
            var back = new GameObject("Backing", typeof(RectTransform)).AddComponent<RawImage>();
            Place(back.rectTransform, narrationGroup.transform, new Vector2(0, -200), new Vector2(1000, 190));
            back.texture = Backing(); back.color = new Color(0, 0, 0, .8f); back.material = overlay; back.raycastTarget = false;
            narration = Label(narrationGroup.transform, "Text", textFont, 36, new Vector2(0, -200), new Vector2(900, 170), Bone);
            narrationGroup.alpha = 0;

            objective = Label(transform, "Objective", textFont, 28, new Vector2(0, 340), new Vector2(1000, 60), Bone);
            objective.text = "";

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0;
            toll = TollClip();
            marker = StoryMarker.Create(head, overlay, textFont);
        }

        CanvasGroup Group(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Place((RectTransform)go.transform, transform, Vector2.zero, ((RectTransform)transform).sizeDelta);
            return go.AddComponent<CanvasGroup>();
        }

        Text Label(Transform parent, string name, Font font, int size, Vector2 position, Vector2 box, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            Place(text.rectTransform, parent, position, box);
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; text.material = overlay; text.lineSpacing = 1.1f;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .95f); shadow.effectDistance = new Vector2(2, -2);
            return text;
        }

        static void Place(RectTransform rect, Transform parent, Vector2 position, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        void LateUpdate() => Follow(false);

        // Stays ahead of the eyes, catching up smoothly when the head turns.
        void Follow(bool snap)
        {
            if (head == null) return;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = head.up;
            forward.Normalize();
            var targetPosition = head.position + forward * Distance + Vector3.down * .05f;
            var targetRotation = Quaternion.LookRotation(forward);
            float k = snap ? 1 : 1 - Mathf.Exp(-4 * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, targetPosition, k), Quaternion.Slerp(transform.rotation, targetRotation, k));
        }

        // ---------- Story API ----------

        public IEnumerator ChapterCard(string heading, string name)
        {
            audioSource.PlayOneShot(toll, .8f);
            title.text = heading; subtitle.text = name;
            yield return FadeGroup(titleGroup, 0, 1, 1.2f);
            yield return new WaitForSeconds(2.8f);
            yield return FadeGroup(titleGroup, 1, 0, 1.2f);
        }

        // Types each line, holds it long enough to read, then moves on.
        public IEnumerator Narrate(params string[] lines)
        {
            yield return FadeGroup(narrationGroup, narrationGroup.alpha, 1, .4f);
            foreach (var line in lines)
            {
                for (int i = 1; i <= line.Length; i++)
                {
                    narration.text = line.Substring(0, i);
                    yield return new WaitForSeconds(line[i - 1] == ',' || line[i - 1] == '.' ? .09f : .03f);
                }
                yield return new WaitForSeconds(Mathf.Clamp(line.Length * .045f, 2f, 6f));
            }
            yield return FadeGroup(narrationGroup, 1, 0, .8f);
            narration.text = "";
        }

        public void SetObjective(string text, bool announce = true)
        {
            objective.text = string.IsNullOrEmpty(text) ? "" : "— " + text + " —";
            if (announce && !string.IsNullOrEmpty(text)) audioSource.PlayOneShot(toll, .35f);
        }

        public void SetMarker(Transform target, float height = 2.2f) => marker.Track(target, height);

        IEnumerator FadeGroup(CanvasGroup group, float from, float to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) { group.alpha = Mathf.Lerp(from, to, t / seconds); yield return null; }
            group.alpha = to;
        }

        // ---------- Generated assets ----------

        static Texture2D Backing()
        {
            // Soft-edged dark strip behind the narration.
            const int w = 128, h = 32;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Abs(x / (w - 1f) * 2 - 1), v = Mathf.Abs(y / (h - 1f) * 2 - 1);
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(1, 0, Mathf.Max((u - .7f) / .3f, (v - .5f) / .5f))));
                }
            texture.Apply(false, true);
            return texture;
        }

        // A distant funeral bell.
        static AudioClip TollClip()
        {
            const int rate = 22050; int n = rate * 3;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                data[i] = (Mathf.Sin(2 * Mathf.PI * 110 * t) * .5f + Mathf.Sin(2 * Mathf.PI * 165.5f * t) * .3f
                    + Mathf.Sin(2 * Mathf.PI * 263 * t) * .15f * Mathf.Exp(-t * 3)) * Mathf.Exp(-t * 1.4f) * Mathf.Min(1, t * 200);
            }
            var clip = AudioClip.Create("Bell", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (overlay != null) Destroy(overlay);
            if (marker != null) Destroy(marker.gameObject);
        }
    }

}
