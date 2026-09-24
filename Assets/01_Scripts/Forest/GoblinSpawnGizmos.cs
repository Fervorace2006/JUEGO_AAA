using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace ForestVR
{
    public sealed class GoblinSpawnGizmos : MonoBehaviour
    {
        public GoblinSettings settings;
        void OnDrawGizmosSelected()
        {
            if (settings == null) return;
#if UNITY_EDITOR
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(transform.position, Vector3.up, settings.wakeRadius);
            Handles.Label(transform.position + Vector3.up, "Despertar: " + settings.wakeRadius + " m | Vision: " + settings.detectionRadius + " m");
            Handles.color = Color.yellow;
            var left = Quaternion.AngleAxis(-settings.visionAngle / 2, Vector3.up) * transform.forward;
            Handles.DrawWireArc(transform.position, Vector3.up, left, settings.visionAngle, settings.detectionRadius);
            Handles.DrawLine(transform.position, transform.position + left * settings.detectionRadius);
            Handles.DrawLine(transform.position, transform.position + Quaternion.AngleAxis(settings.visionAngle, Vector3.up) * left * settings.detectionRadius);
#endif
        }
    }
}
