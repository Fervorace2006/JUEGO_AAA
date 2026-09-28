using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForestVR
{
    // The walls of the cabin (InteriorHouse) are solid: every piece of pared_de_madera and the door get a box collider
    // that fits their mesh, so walking with the joystick cannot leave the room. Only in the cabin; the same wall models
    // outside in the forest are left as they are. The scene and the models are not changed.
    public static class InteriorWalls
    {
        const string SceneName = "InteriorHouse";
        static readonly string[] SolidNames = { "pared_de_madera", "puerta_vieja" };
        // Thin panels get at least this thickness in meters, so the player's body cannot slip through.
        const float MinThickness = .1f;

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
            int added = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (IsSolid(t.name)) added += MakeSolid(t);
            if (added > 0) Physics.SyncTransforms();
        }

        static bool IsSolid(string name)
        {
            var n = name.ToLowerInvariant();
            foreach (var solid in SolidNames) if (n.StartsWith(solid)) return true;
            return false;
        }

        // A box around each mesh of this piece (boxes need no readable mesh, so they also work in the Quest build).
        static int MakeSolid(Transform piece)
        {
            int added = 0;
            foreach (var filter in piece.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                var bounds = filter.sharedMesh.bounds;
                var box = filter.gameObject.AddComponent<BoxCollider>();
                box.center = bounds.center;
                var scale = filter.transform.lossyScale;
                var size = bounds.size;
                // Thicken only the thinnest axis of a flat panel.
                for (int axis = 0; axis < 3; axis++)
                {
                    float minimum = MinThickness / Mathf.Max(.0001f, Mathf.Abs(scale[axis]));
                    if (size[axis] < minimum && size[axis] <= Mathf.Min(size[(axis + 1) % 3], size[(axis + 2) % 3])) size[axis] = minimum;
                }
                box.size = size;
                added++;
            }
            return added;
        }
    }
}
