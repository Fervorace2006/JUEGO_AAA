using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ForestVR
{
    [ExecuteAlways]
    public sealed class EditorOnlyLight : MonoBehaviour
    {
#if UNITY_EDITOR
        Light preview;
        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            Refresh();
        }
        void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Remove();
            else if (state == PlayModeStateChange.EnteredEditMode) Refresh();
        }
        void Update() => Refresh();
        void Refresh()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) { Remove(); return; }
            if (preview != null) return;
            var lightObject = new GameObject("Luz de edicion (temporal)");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            preview = lightObject.AddComponent<Light>();
            preview.type = LightType.Directional;
            preview.intensity = 2;
            preview.shadows = LightShadows.None;
            lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
        }
        void Remove() { if (preview != null) DestroyImmediate(preview.gameObject); }
        void OnDisable() { EditorApplication.playModeStateChanged -= OnPlayMode; Remove(); }
#endif
    }
}
