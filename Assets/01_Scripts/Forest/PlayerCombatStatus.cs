using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ForestVR
{
    // Health, stamina, HUD, blood and sounds of the player. Dying: the view turns red and then black with "HAS MUERTO",
    // and a panel with REINTENTAR appears; pressing it brings the player back to life where they died, at full health.
    [DisallowMultipleComponent]
    public sealed class PlayerCombatStatus : MonoBehaviour
    {
        const float DeathFadeSeconds = 2.5f, DeathHoldSeconds = 1.5f, ReviveFadeSeconds = 1f;

        Health health;
        Transform head;
        PlayerHud bar;
        PlayerStamina stamina;
        BloodScreen blood;
        bool dying;

        public void Initialize(Health playerHealth, Transform playerHead)
        {
            if (health != null) return;
            health = playerHealth;
            head = playerHead;
            // Running with stamina, and the health and stamina bars low in the middle of the view.
            stamina = GetComponent<PlayerStamina>();
            if (stamina == null) stamina = gameObject.AddComponent<PlayerStamina>();
            bar = PlayerHud.Create(head, health, stamina);
            blood = BloodScreen.Create(head, health);
            PlayerSounds.Create(health, head);
            health.Died += OnDied;
        }

        void OnDied()
        {
            if (dying) return;
            dying = true;
            StartCoroutine(GameOver());
        }

        IEnumerator GameOver()
        {
            var music = MusicPlayer.Instance.Clip;
            var fade = DeathScreen.Create(head);
            GameAudio.Music(null, DeathFadeSeconds);
            // Red, then black, then the words.
            for (float t = 0; t < DeathFadeSeconds; t += Time.unscaledDeltaTime)
            {
                fade.Set(t / DeathFadeSeconds);
                yield return null;
            }
            fade.Set(1);
            yield return new WaitForSecondsRealtime(DeathHoldSeconds);
            // Wait for REINTENTAR.
            bool retry = false;
            var panel = RetryPanel.Create(head, () => retry = true);
            fade.HideWords();
            while (!retry) yield return null;
            Destroy(panel);
            // Back to life on the same spot, at full health, as the black fades away.
            health.Restore();
            GameAudio.Music(music, 3);
            for (float t = 0; t < ReviveFadeSeconds; t += Time.unscaledDeltaTime)
            {
                fade.Set(1 - t / ReviveFadeSeconds);
                yield return null;
            }
            fade.Destroy();
            dying = false;
        }

        void OnEnable() { if (bar != null) bar.gameObject.SetActive(true); }
        void OnDisable() { if (bar != null) bar.gameObject.SetActive(false); }

        void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
            if (bar != null) Destroy(bar.gameObject);
            if (blood != null) Destroy(blood.gameObject);
        }

        // Full-view fade in front of the eyes: blood red to black, then "HAS MUERTO" in the menu's font.
        sealed class DeathScreen
        {
            GameObject root;
            Image cover;
            Text words;
            Material overlay;
            bool wordsHidden;

            public static DeathScreen Create(Transform head)
            {
                var screen = new DeathScreen();
                var go = screen.root = new GameObject("Death Screen");
                go.transform.SetParent(head, false);
                go.transform.localPosition = new Vector3(0, 0, .25f);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 100;
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(2000, 2000);
                rect.localScale = Vector3.one / 1000f;
                screen.overlay = new Material(Canvas.GetDefaultCanvasMaterial());
                screen.overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                screen.cover = new GameObject("Cover", typeof(RectTransform)).AddComponent<Image>();
                screen.cover.rectTransform.SetParent(rect, false);
                screen.cover.rectTransform.sizeDelta = rect.sizeDelta;
                screen.cover.material = screen.overlay; screen.cover.raycastTarget = false;
                screen.words = new GameObject("Words", typeof(RectTransform)).AddComponent<Text>();
                screen.words.rectTransform.SetParent(rect, false);
                screen.words.rectTransform.sizeDelta = new Vector2(1200, 200);
                var font = Resources.Load<Font>("Fonts/CinzelDecorative-Bold");
                screen.words.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                screen.words.fontSize = 110; screen.words.alignment = TextAnchor.MiddleCenter;
                screen.words.horizontalOverflow = HorizontalWrapMode.Overflow;
                screen.words.text = "HAS MUERTO";
                screen.words.material = screen.overlay; screen.words.raycastTarget = false;
                screen.Set(0);
                return screen;
            }

            public void Set(float progress)
            {
                // 0..0.5: the red of the wound spreads; 0.5..1: it sinks into black and the words appear.
                var red = new Color(.35f, 0, 0);
                cover.color = progress < .5f
                    ? new Color(red.r, 0, 0, Mathf.SmoothStep(0, .85f, progress / .5f))
                    : new Color(Mathf.Lerp(red.r, 0, (progress - .5f) / .5f), 0, 0, Mathf.Lerp(.85f, 1, (progress - .5f) / .5f));
                words.color = new Color(.75f, .06f, .04f, wordsHidden ? 0 : Mathf.Clamp01((progress - .55f) / .35f));
            }

            // The retry panel shows its own title.
            public void HideWords() { wordsHidden = true; words.enabled = false; }

            public void Destroy()
            {
                Object.Destroy(root);
                Object.Destroy(overlay);
            }
        }

        // "HAS MUERTO" and a REINTENTAR button, fixed in the world in front of the player (not on the face), so the
        // controller ray can point at it. Drawn over the black of the death screen.
        static class RetryPanel
        {
            public static GameObject Create(Transform head, UnityEngine.Events.UnityAction onRetry)
            {
                var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
                forward.Normalize();
                var go = new GameObject("Retry Panel");
                go.transform.SetPositionAndRotation(head.position + forward * 1.3f, Quaternion.LookRotation(forward));
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 101;
                canvas.worldCamera = head.GetComponent<Camera>();
                go.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(1000, 560);
                rect.localScale = Vector3.one / 1000f;
                var overlay = new Material(Canvas.GetDefaultCanvasMaterial());
                overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                var font = Resources.Load<Font>("Fonts/CinzelDecorative-Bold");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                var panel = AddImage("Panel", rect, rect.sizeDelta, Vector2.zero, new Color(.05f, 0, 0, .92f), overlay);
                panel.raycastTarget = false;
                AddText("Title", rect, "HAS MUERTO", font, 100, new Vector2(0, 120), new Color(.75f, .06f, .04f), overlay);

                var button = AddImage("Reintentar", rect, new Vector2(560, 150), new Vector2(0, -120), new Color(.35f, .03f, .02f), overlay);
                var action = button.gameObject.AddComponent<Button>();
                action.targetGraphic = button;
                var colors = action.colors;
                colors.highlightedColor = new Color(1.6f, 1.3f, 1.3f); colors.pressedColor = new Color(.7f, .7f, .7f);
                action.colors = colors;
                action.onClick.AddListener(onRetry);
                AddText("Label", (RectTransform)button.transform, "REINTENTAR", font, 70, Vector2.zero, new Color(.9f, .85f, .75f), overlay);
                // Destroyed with the panel.
                go.AddComponent<DestroyMaterial>().material = overlay;
                return go;
            }

            static Image AddImage(string name, RectTransform parent, Vector2 size, Vector2 position, Color color, Material material)
            {
                var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
                image.rectTransform.SetParent(parent, false);
                image.rectTransform.sizeDelta = size; image.rectTransform.anchoredPosition = position;
                image.color = color; image.material = material;
                return image;
            }

            static void AddText(string name, RectTransform parent, string text, Font font, int size, Vector2 position, Color color, Material material)
            {
                var label = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
                label.rectTransform.SetParent(parent, false);
                label.rectTransform.sizeDelta = new Vector2(parent.sizeDelta.x, size * 1.6f);
                label.rectTransform.anchoredPosition = position;
                label.font = font; label.fontSize = size; label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.text = text; label.color = color; label.material = material; label.raycastTarget = false;
            }
        }

        sealed class DestroyMaterial : MonoBehaviour
        {
            public Material material;
            void OnDestroy() { if (material != null) Destroy(material); }
        }
    }
}
