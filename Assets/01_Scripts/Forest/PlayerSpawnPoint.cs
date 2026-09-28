using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForestVR
{
    // Starts the player exactly on the scene's spawn point (an object named Punto_de_Aparicion), looking where it looks.
    // In VR the head is wherever the player stands in their play space, which can be a meter or more from the rig's
    // origin — enough to begin outside a small room. So it is the head, not the rig, that is put on the point, once
    // tracking has started and again a moment later in case the headset recentered.
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        const string PointName = "Punto_de_Aparicion";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameObject point = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == PointName) { point = root; break; }
                var child = root.transform.Find(PointName);
                if (child != null) { point = child.gameObject; break; }
            }
            if (point == null) return;
            var origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null) return;
            var runner = point.GetComponent<PlayerSpawnPoint>();
            if (runner == null) runner = point.AddComponent<PlayerSpawnPoint>();
            runner.StartCoroutine(Place(origin, point.transform));
        }

        static IEnumerator Place(XROrigin origin, Transform point)
        {
            // Let tracking start (the head leaves the origin) or give up waiting after a second.
            for (float t = 0; t < 1 && origin.Camera != null && origin.Camera.transform.localPosition.sqrMagnitude < .0001f; t += Time.unscaledDeltaTime)
                yield return null;
            Move(origin, point);
            yield return new WaitForSeconds(1);
            Move(origin, point);
        }

        static void Move(XROrigin origin, Transform point)
        {
            if (origin == null || point == null || origin.Camera == null) return;
            var body = origin.GetComponent<CharacterController>();
            bool hadBody = body != null && body.enabled;
            if (hadBody) body.enabled = false;
            var forward = Vector3.ProjectOnPlane(point.forward, Vector3.up);
            if (forward.sqrMagnitude > .001f) origin.MatchOriginUpCameraForward(Vector3.up, forward.normalized);
            // Keep the eye height, move only across the floor.
            var head = origin.Camera.transform.position;
            origin.MoveCameraToWorldLocation(new Vector3(point.position.x, head.y, point.position.z));
            if (hadBody) body.enabled = true;
            Physics.SyncTransforms();
        }

    }
}
