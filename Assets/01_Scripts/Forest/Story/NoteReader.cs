using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ForestVR
{
    // A note held up in front of the player on aged paper: one of Mateo's pages or his letter. It stays in place while
    // it is read and closes with either trigger (not in the first moment, so the pull that picked it up does not close
    // it) or on its own after a while. Drawn over trees and walls.
    public sealed class NoteReader : MonoBehaviour
    {
        const float Distance = 1.1f, MinSeconds = 1.5f;
        public static NoteReader Open { get; private set; }

        Material overlay;
        InputAction close;
        float openedAt, closesAt;

        public static NoteReader Show(Transform head, NotePage page, float seconds = 40) =>
            page != null ? Show(head, page.title, page.text, page.ending, seconds) : null;

        public static NoteReader Show(Transform head, string title, string body, string ending, float seconds = 40)
        {
            if (Open != null) Destroy(Open.gameObject);
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            forward.Normalize();
            var go = new GameObject("Nota (lectura)");
            go.transform.SetPositionAndRotation(head.position + forward * Distance + Vector3.down * .05f, Quaternion.LookRotation(forward));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(900, 1040);
            rect.localScale = Vector3.one / 1000f;
            var reader = go.AddComponent<NoteReader>();
            reader.overlay = new Material(Canvas.GetDefaultCanvasMaterial());
            reader.overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            var titleFont = Resources.Load<Font>("Fonts/CinzelDecorative-Bold");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (titleFont == null) titleFont = font;

            var paper = new GameObject("Papel", typeof(RectTransform)).AddComponent<Image>();
            paper.rectTransform.SetParent(rect, false);
            paper.rectTransform.sizeDelta = rect.sizeDelta;
            paper.color = new Color(.86f, .8f, .64f, .97f);
            paper.material = reader.overlay; paper.raycastTarget = false;

            reader.Add(rect, title, titleFont, 46, FontStyle.Normal, new Vector2(0, 430), new Vector2(820, 100), new Color(.35f, .08f, .05f));
            reader.Add(rect, body, font, 34, FontStyle.Italic, new Vector2(0, 20), new Vector2(780, 700), new Color(.15f, .1f, .07f));
            reader.Add(rect, ending, font, string.IsNullOrEmpty(ending) || ending.Length > 14 ? 58 : 40, FontStyle.Bold, new Vector2(0, -385), new Vector2(800, 90),
                ending != null && ending.StartsWith("CONTINUAR") ? new Color(.6f, .05f, .03f) : new Color(.25f, .15f, .1f));
            reader.Add(rect, "Pulsa el gatillo para cerrar", font, 24, FontStyle.Normal, new Vector2(0, -475), new Vector2(800, 40), new Color(.4f, .3f, .22f));

            reader.openedAt = Time.unscaledTime;
            reader.closesAt = Time.unscaledTime + seconds;
            Open = reader;
            return reader;
        }

        void Add(RectTransform parent, string text, Font font, int size, FontStyle style, Vector2 position, Vector2 box, Color color)
        {
            var label = new GameObject("Texto", typeof(RectTransform)).AddComponent<Text>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.sizeDelta = box; label.rectTransform.anchoredPosition = position;
            label.font = font; label.fontSize = size; label.fontStyle = style;
            label.alignment = TextAnchor.MiddleCenter; label.lineSpacing = 1.1f;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = text; label.color = color; label.material = overlay; label.raycastTarget = false;
        }

        void Awake()
        {
            close = new InputAction("Close Note", InputActionType.Button);
            close.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
            close.AddBinding("<XRController>{RightHand}/{TriggerButton}");
#if UNITY_EDITOR
            close.AddBinding("<Keyboard>/enter");
            close.AddBinding("<Keyboard>/space");
#endif
            close.Enable();
        }

        void Update()
        {
            bool canClose = Time.unscaledTime - openedAt >= MinSeconds;
            if ((canClose && close.WasPressedThisFrame()) || Time.unscaledTime >= closesAt) Destroy(gameObject);
        }

        void OnDestroy()
        {
            close?.Dispose();
            if (overlay != null) Destroy(overlay);
            if (Open == this) Open = null;
        }
    }
}
