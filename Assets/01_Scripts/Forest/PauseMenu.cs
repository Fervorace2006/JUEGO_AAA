using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ForestVR
{
    // Pause menu of the game (forest and cabin): the X button of the left Touch controller (or its Menu button ☰) opens it in front of the player and
    // freezes the game (enemies, story, sounds). Point with the controller and pull the trigger:
    // CONTINUAR JUEGO closes it, GUARDAR PARTIDA saves this moment, SALIR DE LA PARTIDA goes back to the start menu,
    // where CONTINUAR PARTIDA loads the last save. In the editor Escape or P also open it.
    public sealed class PauseMenu : MonoBehaviour
    {
        const string MenuScene = "MainMenu";
        // The scenes of the game where it can be opened: the forest and the cabin.
        static readonly string[] GameScenes = { "ForestScene", "InteriorHouse" };
        const float Distance = 1.4f;
        public static bool Paused { get; private set; }

        InputAction menuButton;
        GameObject panel;
        Text status;
        Material overlay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetPaused(false);
            if (System.Array.IndexOf(GameScenes, scene.name) < 0) return;
            var go = new GameObject("Pause Menu");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PauseMenu>();
        }

        void Awake()
        {
            menuButton = new InputAction("Pause", InputActionType.Button);
            // X on the left Touch controller; the Menu button (☰) as well.
            menuButton.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
            menuButton.AddBinding("<XRController>{LeftHand}/{MenuButton}");
#if UNITY_EDITOR
            menuButton.AddBinding("<Keyboard>/escape");
            menuButton.AddBinding("<Keyboard>/p");
#endif
            menuButton.performed += _ => Toggle();
            menuButton.Enable();
        }

        void OnDestroy()
        {
            menuButton?.Dispose();
            if (overlay != null) Destroy(overlay);
            if (Paused) SetPaused(false);
        }

        void Toggle()
        {
            if (panel != null) { Resume(); return; }
            // Not while dying: the death screen has its own REINTENTAR.
            var player = FindAnyObjectByType<PlayerCombatStatus>();
            var health = player != null ? player.GetComponent<Health>() : null;
            if (health != null && health.IsDead) return;
            Open();
        }

        void Open()
        {
            var camera = Camera.main;
            if (camera == null) return;
            // The cabin has no EventSystem of its own: the controller ray needs one to press the buttons.
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var events = new GameObject("EventSystem");
                SceneManager.MoveGameObjectToScene(events, gameObject.scene);
                events.AddComponent<UnityEngine.EventSystems.EventSystem>();
                events.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            }
            SetPaused(true);
            Build(camera.transform);
            var save = SaveGame.Read();
            SetStatus(save != null ? "Última partida guardada: " + save.savedAt : "Aún no has guardado la partida.");
        }

        void Resume()
        {
            if (panel != null) Destroy(panel);
            panel = null;
            SetPaused(false);
        }

        void Save()
        {
            SetStatus(SaveGame.SaveNow()
                ? "Partida guardada. Podrás seguir desde aquí con CONTINUAR PARTIDA en el menú."
                : "No se pudo guardar la partida.");
        }

        void Quit()
        {
            Resume();
            if (Application.CanStreamedLevelBeLoaded(MenuScene)) SceneManager.LoadScene(MenuScene);
        }

        void SetStatus(string text) { if (status != null) status.text = text; }

        // Freezes enemies, projectiles, the story and the sounds; head tracking, hands and the menu keep working.
        public static void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0 : 1;
            AudioListener.pause = paused;
        }

        // ---------- Panel ----------

        void Build(Transform head)
        {
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            forward.Normalize();
            panel = new GameObject("Pause Panel");
            SceneManager.MoveGameObjectToScene(panel, gameObject.scene);
            panel.transform.SetPositionAndRotation(head.position + forward * Distance + Vector3.down * .1f, Quaternion.LookRotation(forward));
            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 90;
            canvas.worldCamera = head.GetComponent<Camera>();
            panel.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            var rect = (RectTransform)panel.transform;
            rect.sizeDelta = new Vector2(900, 820);
            rect.localScale = Vector3.one / 1000f;
            if (overlay == null)
            {
                // Drawn over trees and walls that could be between the player and the panel.
                overlay = new Material(Canvas.GetDefaultCanvasMaterial());
                overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            }
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var back = AddImage("Fondo", rect, rect.sizeDelta, Vector2.zero, new Color(.03f, .02f, .02f, .94f));
            back.raycastTarget = false;
            AddText("Titulo", rect, "PAUSA", font, 96, FontStyle.Bold, new Vector2(0, 310), new Vector2(800, 130), new Color(.75f, .06f, .04f));
            AddButton(rect, "CONTINUAR JUEGO", font, new Vector2(0, 150), Resume);
            AddButton(rect, "GUARDAR PARTIDA", font, new Vector2(0, 10), Save);
            AddButton(rect, "SALIR DE LA PARTIDA", font, new Vector2(0, -130), Quit);
            status = AddText("Estado", rect, "", font, 30, FontStyle.Italic, new Vector2(0, -290), new Vector2(820, 110), new Color(.8f, .75f, .65f));
        }

        void AddButton(RectTransform parent, string label, Font font, Vector2 position, UnityAction onClick)
        {
            var image = AddImage(label, parent, new Vector2(640, 110), position, new Color(.3f, .03f, .02f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.6f, 1.3f, 1.3f); colors.pressedColor = new Color(.7f, .7f, .7f);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            AddText("Texto", image.rectTransform, label, font, 50, FontStyle.Bold, Vector2.zero, new Vector2(640, 110), new Color(.92f, .87f, .77f));
        }

        Image AddImage(string name, RectTransform parent, Vector2 size, Vector2 position, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.rectTransform.sizeDelta = size; image.rectTransform.anchoredPosition = position;
            image.color = color; image.material = overlay;
            return image;
        }

        Text AddText(string name, RectTransform parent, string text, Font font, int size, FontStyle style, Vector2 position, Vector2 box, Color color)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.sizeDelta = box; label.rectTransform.anchoredPosition = position;
            label.font = font; label.fontSize = size; label.fontStyle = style; label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = text; label.color = color; label.material = overlay; label.raycastTarget = false;
            return label;
        }
    }
}
