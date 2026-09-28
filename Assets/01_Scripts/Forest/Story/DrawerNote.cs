using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ForestVR
{
    // Mateo's letter, lying inside the drawer of the cabin (InteriorHouse). Open the drawer and grab it with Grip: it is
    // read in front of the player (NotePage 4, Resources/Story) and the story's ending starts (CabinDirector).
    public sealed class DrawerNote : MonoBehaviour
    {
        const string SceneName = "InteriorHouse";
        const int LetterNumber = 4;
        // Where the note lies, in the drawer model's own space: on its floor, a little toward the back.
        static readonly Vector3 LocalPosition = new Vector3(0, .03f, -.08f);
        // How far the drawer must be pulled out before the note can be taken.
        const float OpenEnough = .35f;

        public static DrawerNote Current { get; private set; }
        public static event System.Action<NoteReader> Picked;
        public VRDrawer Drawer => drawer;

        VRDrawer drawer;
        Collider solid;
        bool taken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Picked = null; Current = null; }

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
            go.name = "Carta de Mateo";
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
            Current = note;
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
            Sparkles.Burst(transform.position + Vector3.up * .1f, 40, .15f);
            var camera = Camera.main;
            var reader = camera != null ? NoteReader.Show(camera.transform, NotePage.Load(LetterNumber), 60) : null;
            Picked?.Invoke(reader);
            Destroy(gameObject);
        }

        void OnDestroy() { if (Current == this) Current = null; }
    }
}
