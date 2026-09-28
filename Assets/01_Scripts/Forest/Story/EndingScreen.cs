using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ForestVR
{
    // The end of the story: the view darkens and a panel sums up the night (play time, enemies defeated, Mateo's notes),
    // announces that the story continues, and VOLVER AL MENÚ goes back to the start menu (point with the controller and
    // pull the trigger; Enter in the editor).
    public sealed class EndingScreen : MonoBehaviour
    {
        const string MenuScene = "MainMenu";
        static readonly Color Bone = new Color(.9f, .85f, .76f), Blood = new Color(.75f, .06f, .04f);

        Material overlay;
        CanvasGroup group;
        Image shade;
        InputAction confirm;
        bool leaving;

        public static EndingScreen Show(Transform head)
        {
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<XRUIInputModule>();
            }
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            forward.Normalize();
            var go = new GameObject("Ending Screen");
            var screen = go.AddComponent<EndingScreen>();
            screen.Build(head, forward);
            return screen;
        }

        void Build(Transform head, Vector3 forward)
        {
            overlay = new Material(Canvas.GetDefaultCanvasMaterial());
            overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            var titleFont = Resources.Load<Font>("Fonts/CinzelDecorative-Bold");
            var textFont = Resources.Load<Font>("Fonts/Cinzel");
            var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (titleFont == null) titleFont = legacy;
            if (textFont == null) textFont = legacy;

            // Darkness in front of the eyes, behind the panel.
            var veil = new GameObject("Veil").AddComponent<Canvas>();
            veil.transform.SetParent(head, false);
            veil.transform.localPosition = new Vector3(0, 0, .3f);
            veil.renderMode = RenderMode.WorldSpace; veil.sortingOrder = 95;
            ((RectTransform)veil.transform).sizeDelta = new Vector2(2000, 2000);
            veil.transform.localScale = Vector3.one / 1000f;
            veil.transform.SetParent(transform, true);
            shade = new GameObject("Shade", typeof(RectTransform)).AddComponent<Image>();
            shade.rectTransform.SetParent(veil.transform, false);
            shade.rectTransform.sizeDelta = new Vector2(2000, 2000);
            shade.color = new Color(0, 0, 0, 0); shade.material = overlay; shade.raycastTarget = false;

            // The panel, fixed in the world in front of the player.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(transform, false);
            panel.transform.SetPositionAndRotation(head.position + forward * 1.6f, Quaternion.LookRotation(forward));
            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 96;
            canvas.worldCamera = head.GetComponent<Camera>();
            panel.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rect = (RectTransform)panel.transform;
            rect.sizeDelta = new Vector2(1100, 1000);
            rect.localScale = Vector3.one / 1000f;
            group = panel.AddComponent<CanvasGroup>();
            group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;

            Label(rect, "SHADOWWOOD", titleFont, 110, new Vector2(0, 390), new Vector2(1100, 150), Blood);
            Label(rect, "Sobreviviste a la noche", textFont, 44, new Vector2(0, 285), new Vector2(1000, 70), Bone);
            var line = new GameObject("Line", typeof(RectTransform)).AddComponent<Image>();
            line.rectTransform.SetParent(rect, false);
            line.rectTransform.sizeDelta = new Vector2(600, 3); line.rectTransform.anchoredPosition = new Vector2(0, 238);
            line.color = Blood; line.material = overlay; line.raycastTarget = false;

            var rows = new[]
            {
                ("Tiempo de juego", GameStats.FormatTime(GameStats.PlaySeconds)),
                ("Enemigos derrotados", GameStats.EnemiesDefeated.ToString()),
                ("Páginas de la libreta", $"{Mathf.Min(PagePickup.Collected, 3)} / 3"),
                ("Carta de Mateo", "Encontrada"),
            };
            float y = 170;
            foreach (var (name, value) in rows)
            {
                Label(rect, name, textFont, 36, new Vector2(-190, y), new Vector2(520, 60), new Color(.75f, .7f, .62f), TextAnchor.MiddleRight);
                Label(rect, value, titleFont, 38, new Vector2(250, y), new Vector2(360, 60), Bone, TextAnchor.MiddleLeft);
                y -= 68;
            }
            Label(rect, "Mateo sigue en algún lugar de la montaña.", textFont, 34, new Vector2(0, -150), new Vector2(1000, 60), new Color(.75f, .7f, .62f)).fontStyle = FontStyle.Italic;
            Label(rect, "CONTINUARÁ...", titleFont, 70, new Vector2(0, -240), new Vector2(1000, 100), Blood);

            var button = new GameObject("VOLVER AL MENÚ", typeof(RectTransform)).AddComponent<Image>();
            button.rectTransform.SetParent(rect, false);
            button.rectTransform.sizeDelta = new Vector2(560, 110); button.rectTransform.anchoredPosition = new Vector2(0, -385);
            button.color = new Color(.3f, .03f, .02f); button.material = overlay;
            var action = button.gameObject.AddComponent<Button>();
            action.targetGraphic = button;
            var colors = action.colors;
            colors.highlightedColor = new Color(1.6f, 1.3f, 1.3f); colors.pressedColor = new Color(.7f, .7f, .7f);
            colors.selectedColor = colors.normalColor;
            action.colors = colors;
            action.onClick.AddListener(BackToMenu);
            Label((RectTransform)button.transform, "VOLVER AL MENÚ", titleFont, 44, Vector2.zero, new Vector2(560, 110), Bone);

            confirm = new InputAction("Ending Confirm", InputActionType.Button);
#if UNITY_EDITOR
            confirm.AddBinding("<Keyboard>/enter");
#endif
            confirm.Enable();
            StartCoroutine(Appear());
        }

        Text Label(RectTransform parent, string text, Font font, int size, Vector2 position, Vector2 box, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var label = new GameObject("Texto", typeof(RectTransform)).AddComponent<Text>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.sizeDelta = box; label.rectTransform.anchoredPosition = position;
            label.font = font; label.fontSize = size; label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = text; label.color = color; label.material = overlay; label.raycastTarget = false;
            return label;
        }

        IEnumerator Appear()
        {
            for (float t = 0; t < 2.5f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 2.5f);
                shade.color = new Color(0, 0, 0, .85f * k);
                group.alpha = Mathf.Clamp01((t - .8f) / 1.7f);
                yield return null;
            }
            shade.color = new Color(0, 0, 0, .85f);
            group.alpha = 1; group.interactable = true; group.blocksRaycasts = true;
        }

        void Update()
        {
            if (!leaving && group != null && group.interactable && confirm.WasPressedThisFrame()) BackToMenu();
        }

        void BackToMenu()
        {
            if (leaving) return;
            leaving = true;
            if (Application.CanStreamedLevelBeLoaded(MenuScene)) SceneManager.LoadScene(MenuScene);
        }

        void OnDestroy()
        {
            confirm?.Dispose();
            if (overlay != null) Destroy(overlay);
        }
    }
}
