using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ForestVR
{
    // Health, stamina, HUD, blood and sounds of the player. Dying ends the run: the view turns red and then black with
    // "HAS MUERTO", and the game goes back to the start menu, where JUGAR begins again from the start.
    [DisallowMultipleComponent]
    public sealed class PlayerCombatStatus : MonoBehaviour
    {
        const string MenuScene = "MainMenu";
        const float DeathFadeSeconds = 2.5f, DeathHoldSeconds = 1.5f;

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
            // Back to the menu; without it in the build, start the same scene again.
            if (Application.CanStreamedLevelBeLoaded(MenuScene)) SceneManager.LoadScene(MenuScene);
            else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
            Image cover;
            Text words;
            Material overlay;

            public static DeathScreen Create(Transform head)
            {
                var screen = new DeathScreen();
                var go = new GameObject("Death Screen");
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
                words.color = new Color(.75f, .06f, .04f, Mathf.Clamp01((progress - .55f) / .35f));
            }
        }
    }
}
