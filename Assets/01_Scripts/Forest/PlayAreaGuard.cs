using Unity.XR.CoreUtils;
using UnityEngine;

namespace ForestVR
{
    // Keeps the player on walkable ground: walking into the lake or past the edge of the map pushes the rig back to the
    // last good spot and shows a short warning.
    public sealed class PlayAreaGuard : MonoBehaviour
    {
        [SerializeField] string message = "No deberías pasar por ahí.";
        XROrigin origin;
        Vector3 lastGood;
        bool hasGood;
        VRWorldLabel label;
        float hideAt;

        void Awake()
        {
            origin = GetComponentInChildren<XROrigin>();
            if (origin == null) origin = GetComponentInParent<XROrigin>();
        }

        void LateUpdate()
        {
            if (origin == null || origin.Camera == null || !PlayArea.Ready) return;
            var head = origin.Camera.transform.position;
            if (PlayArea.IsWalkable(head)) { lastGood = head; hasGood = true; }
            else if (hasGood)
            {
                var back = lastGood - head; back.y = 0;
                origin.transform.position += back;
                Warn();
            }
            if (label != null && label.gameObject.activeSelf && Time.time >= hideAt) label.gameObject.SetActive(false);
        }

        void Warn()
        {
            if (label == null)
            {
                label = VRWorldLabel.Create(origin.Camera.transform, "Play Area Warning", new Vector3(0, -.05f, .8f));
                label.Text.color = new Color(1f, .82f, .7f);
            }
            if (!label.gameObject.activeSelf || Time.time > hideAt - 2.2f) label.Show(message, .5f, .07f);
            label.gameObject.SetActive(true);
            hideAt = Time.time + 2.5f;
        }

        void OnDestroy() { if (label != null) Destroy(label.gameObject); }
    }
}
