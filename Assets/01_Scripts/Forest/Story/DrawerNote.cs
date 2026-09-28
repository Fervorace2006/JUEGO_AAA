using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ForestVR
{
    // Mateo's last note, lying inside the drawer of the cabin (InteriorHouse). Open the drawer and grab it with Grip:
    // its text appears in front of the player (he went to the mountain), ending with "CONTINUARÁ...".
    public sealed class DrawerNote : MonoBehaviour
    {
        const string SceneName = "InteriorHouse";
        // Where the note lies, in the drawer model's own space: on its floor, a little toward the back.
        static readonly Vector3 LocalPosition = new Vector3(0, .03f, -.08f);
        // How far the drawer must be pulled out before the note can be taken.
        const float OpenEnough = .35f;

        const string Title = "Nota de Mateo";
        const string Body =
            "Si alguien encuentra esto:\n\n" +
            "Los duendes no eran lo peor de este bosque. Cada noche oigo algo que me llama desde la montaña, " +
            "más allá de los pinos. Creo que allí empezó todo, y allí tiene que terminar.\n\n" +
            "Me llevo la linterna y lo poco que queda de comida. No me sigas.\n\n" +
            "— Mateo";
        const string Ending = "CONTINUARÁ...";

        VRDrawer drawer;
        Collider solid;
        bool taken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName) return;
            foreach (var drawer in FindObjectsByType<VRDrawer>())
                if (drawer.gameObject.scene == scene) { Create(drawer); return; }
        }

        static void Create(VRDrawer drawer)
        {
            var prefab = Resources.Load<GameObject>("DiaryPage");
            var go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Nota de Mateo";
            if (prefab == null) go.transform.localScale = new Vector3(.22f, .3f, .01f);
            // The notebook pages of the forest collect themselves when walked over; this one is picked up by hand.
            foreach (var page in go.GetComponentsInChildren<PagePickup>(true)) DestroyImmediate(page);
            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            var note = go.AddComponent<DrawerNote>();
            note.drawer = drawer;
            note.solid = go.GetComponentInChildren<Collider>();
            note.Follow();
            var grab = go.AddComponent<XRSimpleInteractable>();
            grab.selectEntered.AddListener(note.OnPicked);
        }

        // The drawer model is scaled unevenly, so the note follows it instead of being its child (no stretching).
        void Follow()
        {
            if (drawer == null) return;
            var d = drawer.transform;
            // Lying flat, face up.
            transform.SetPositionAndRotation(d.TransformPoint(LocalPosition), d.rotation * Quaternion.Euler(90, 0, 0));
        }

        bool DrawerOpen()
        {
            if (drawer == null) return true;
            float range = drawer.openX - drawer.closedX;
            return range <= 0 || (drawer.transform.localPosition.x - drawer.closedX) / range >= OpenEnough;
        }

        void LateUpdate()
        {
            if (taken) return;
            Follow();
            // Out of reach while the drawer is closed.
            if (solid != null) solid.enabled = DrawerOpen();
        }

        void OnPicked(SelectEnterEventArgs args)
        {
            if (taken || !DrawerOpen()) return;
            taken = true;
            var audio = GameAudio.Get;
            if (audio != null && audio.pagePickup != null) GameAudio.PlayAt(audio.pagePickup, transform.position, 1, 1, 10);
            var camera = Camera.main;
            if (camera != null) NotePanel.Show(camera.transform, Title, Body, Ending);
            Destroy(gameObject);
        }

        // The note's text in front of the player, drawn over the walls of the small cabin, for a while.
        static class NotePanel
        {
            const float Distance = 1.1f, Seconds = 35;

            public static void Show(Transform head, string title, string body, string ending)
            {
                var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
                forward.Normalize();
                var go = new GameObject("Nota de Mateo (texto)");
                go.transform.SetPositionAndRotation(head.position + forward * Distance + Vector3.down * .05f, Quaternion.LookRotation(forward));
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 50;
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(900, 1000);
                rect.localScale = Vector3.one / 1000f;
                var overlay = new Material(Canvas.GetDefaultCanvasMaterial());
                overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                // Aged paper.
                var paper = new GameObject("Papel", typeof(RectTransform)).AddComponent<Image>();
                paper.rectTransform.SetParent(rect, false);
                paper.rectTransform.sizeDelta = rect.sizeDelta;
                paper.color = new Color(.86f, .8f, .64f, .97f);
                paper.material = overlay; paper.raycastTarget = false;

                Add(rect, title, font, 58, FontStyle.Bold, new Vector2(0, 400), new Vector2(800, 90), new Color(.35f, .08f, .05f), overlay);
                Add(rect, body, font, 36, FontStyle.Italic, new Vector2(0, 10), new Vector2(780, 640), new Color(.15f, .1f, .07f), overlay);
                Add(rect, ending, font, 64, FontStyle.Bold, new Vector2(0, -410), new Vector2(800, 100), new Color(.6f, .05f, .03f), overlay);

                go.AddComponent<Timed>().Setup(overlay, Seconds);
            }

            static void Add(RectTransform parent, string text, Font font, int size, FontStyle style, Vector2 position, Vector2 box, Color color, Material material)
            {
                var label = new GameObject("Texto", typeof(RectTransform)).AddComponent<Text>();
                label.rectTransform.SetParent(parent, false);
                label.rectTransform.sizeDelta = box; label.rectTransform.anchoredPosition = position;
                label.font = font; label.fontSize = size; label.fontStyle = style;
                label.alignment = TextAnchor.MiddleCenter; label.lineSpacing = 1.1f;
                label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
                label.text = text; label.color = color; label.material = material; label.raycastTarget = false;
            }
        }

        sealed class Timed : MonoBehaviour
        {
            Material material;
            float until;
            public void Setup(Material overlay, float seconds) { material = overlay; until = Time.time + seconds; }
            void Update() { if (Time.time >= until) Destroy(gameObject); }
            void OnDestroy() { if (material != null) Destroy(material); }
        }
    }
}
