using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ForestVR
{
    // An empty at a door: walking up to it loads another scene (the inside of the cabin). While locked it only says so.
    // The story unlocks it when the last chapter asks you to go in; untick "Locked" to use it freely.
    public sealed class SceneDoor : MonoBehaviour
    {
        [Tooltip("Escena que se carga al entrar (debe estar en File > Build Profiles para las builds).")]
        public string targetScene = "InteriorHouse";
        [Tooltip("Distancia a la que se entra, en metros.")]
        [Min(0.3f)] public float radius = 1.6f;
        public bool locked = true;
        [SerializeField] string lockedMessage = "La puerta está atrancada. Todavía no.";

        public static SceneDoor Current { get; private set; }
        public bool Entered { get; private set; }
        Transform viewer;
        VRWorldLabel label;
        float hideAt, nextWarning;

        void OnEnable() => Current = this;
        void OnDisable() { if (Current == this) Current = null; }

        void Update()
        {
            if (Entered) return;
            if (viewer == null) { var camera = Camera.main; if (camera == null) return; viewer = camera.transform; }
            if (label != null && label.gameObject.activeSelf && Time.time >= hideAt) label.gameObject.SetActive(false);
            var delta = viewer.position - transform.position; delta.y = 0;
            if (delta.magnitude > radius) return;
            if (locked) { if (Time.time >= nextWarning) Warn(); return; }
            Entered = true;
            Load();
        }

        void Load()
        {
            if (Application.CanStreamedLevelBeLoaded(targetScene)) { SceneManager.LoadScene(targetScene); return; }
#if UNITY_EDITOR
            // In the editor it also works when the scene is not in the build list yet.
            foreach (var guid in AssetDatabase.FindAssets(targetScene + " t:Scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != targetScene) continue;
                Debug.LogWarning($"SceneDoor: '{targetScene}' no está en Build Profiles; se carga solo en el editor. Añádela para las builds.", this);
                EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            Debug.LogError($"SceneDoor: no existe la escena '{targetScene}'.", this);
            Entered = false;
        }

        void Warn()
        {
            nextWarning = Time.time + 3;
            if (label == null) { label = VRWorldLabel.Create(viewer, "Door Message", new Vector3(0, -.05f, .8f)); label.Text.color = new Color(1f, .82f, .7f); }
            label.Show(lockedMessage, .55f, .07f);
            label.gameObject.SetActive(true);
            hideAt = Time.time + 2.5f;
        }

        void OnDestroy() { if (label != null) Destroy(label.gameObject); }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Handles.color = locked ? new Color(1f, .5f, .2f) : new Color(.3f, 1f, .4f);
            Handles.DrawWireDisc(transform.position, Vector3.up, radius, 2);
            Handles.Label(transform.position + Vector3.up * 2, "Puerta -> " + targetScene + (locked ? " (cerrada hasta el final)" : ""));
        }
#endif
    }
}
