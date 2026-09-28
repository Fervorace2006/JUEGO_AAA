using UnityEngine;

namespace ForestVR
{
    // Tree trunk sizes used by ForestSceneSetup. Enemy spawn points live in ForestScene itself.
    [CreateAssetMenu(menuName = "Forest VR/Enemy Roster")]
    public sealed class ForestEnemyRoster : ScriptableObject
    {
        [System.Serializable]
        public sealed class Trunk
        {
            [Tooltip("Name of the tree mesh.")]
            public string meshName;
            [Tooltip("Trunk centre in the mesh's local X/Z.")]
            public Vector2 center;
            [Tooltip("Local Y of the base of the trunk.")]
            public float bottom;
            public float radius = .3f;
            public float height = 3;
        }

        public Trunk[] trunks = new Trunk[0];
    }
}
