using UnityEngine;
namespace ForestVR
{
    [CreateAssetMenu(menuName = "Forest VR/Locomotion Settings")]
    public sealed class VRLocomotionSettings : ScriptableObject
    {
        [Range(1, 70)] public float slopeLimit = 60;
        [Range(0.05f, 0.6f)] public float stepHeight = 0.4f;
        [Range(0.01f, 0.1f)] public float skinWidth = 0.04f;
        [Min(0.1f)] public float recoveryDepth = 0.75f;
    }
}
