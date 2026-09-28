using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ForestVR
{
    // Mateo's letter, lying inside the drawer of the cabin (InteriorHouse): the notebook sheet placed in the scene
    // (hojas_de_Libreta) or, without it, one made here. It slides out with the drawer; once the drawer is open, grab it
    // with Grip: it is read in front of the player (NotePage 4, Resources/Story) and the story's ending starts.
    public sealed class DrawerNote : MonoBehaviour
    {
        const string SceneName = "InteriorHouse";
        const int LetterNumber = 4;
        // The sheet placed inside the drawer in the scene.
        const string PlacedSheetName = "hojas_de_libreta";
        // Where the note lies, in the drawer model's own space: on its floor, a little toward the back.
        static readonly Vector3 LocalPosition = new Vector3(0, .03f, -.08f);
        // How far the drawer must be pulled out before the note can be taken.
        const float OpenEnough = .35f;

        public static DrawerNote Current { get; private set; }
        public static event System.Action<NoteReader> Picked;
        public VRDrawer Drawer => drawer;

        VRDrawer drawer;
        Collider[] solids;
        bool taken;
        // Pose inside the drawer, in the drawer's own space: where the sheet was placed in the scene.
        Vector3 relativePosition;
        Quaternion relativeRotation;
        bool placedInScene;

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
                if (drawer.gameObject.scene == scene)
                {
                    var sheet = FindPlacedSheet(scene);
                    if (sheet != null) Adopt(sheet, drawer); else Create(drawer);
                    return;
                }
        }

        static GameObject FindPlacedSheet(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.ToLowerInvariant().StartsWith(PlacedSheetName)) return t.gameObject;
            return null;
        }

        // The sheet placed in the scene: it keeps the spot where it was put inside the drawer and moves with it.
        static void Adopt(GameObject sheet, VRDrawer drawer)
        {
            // A solid, grabbable shape that fits the sheet (the model has no collider of its own).
            foreach (var filter in sheet.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                var box = filter.gameObject.AddComponent<BoxCollider>();
                box.center = filter.sharedMesh.bounds.center;
                // A little thickness so a flat sheet can still be touched with the hand.
                var size = filter.sharedMesh.bounds.size;
                box.size = Vector3.Max(size, Vector3.one * .02f / Mathf.Max(.0001f, filter.transform.lossyScale.x));
            }
            var body = sheet.GetComponent<Rigidbody>();
            if (body == null) body = sheet.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            var note = sheet.AddComponent<DrawerNote>();
            note.drawer = drawer;
            note.solids = sheet.GetComponentsInChildren<Collider>(true);
            var d = drawer.transform;
            note.relativePosition = d.InverseTransformPoint(sheet.transform.position);
            note.relativeRotation = Quaternion.Inverse(d.rotation) * sheet.transform.rotation;
            note.placedInScene = true;
            var grab = sheet.AddComponent<XRSimpleInteractable>();
            grab.selectEntered.AddListener(note.OnPicked);
            Current = note;
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
            note.solids = go.GetComponentsInChildren<Collider>(true);
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
            if (placedInScene) { transform.SetPositionAndRotation(d.TransformPoint(relativePosition), d.rotation * relativeRotation); return; }
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
            if (solids != null) { bool open = DrawerOpen(); foreach (var solid in solids) if (solid != null) solid.enabled = open; }
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
