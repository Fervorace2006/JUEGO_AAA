using UnityEngine;

namespace ForestVR
{
    // Data for ForestSceneSetup, loaded from Resources/ForestEnemyRoster: which enemies get a spawner next to the
    // goblin's spawn point, and the trunk size of each tree mesh (the only part of a tree that blocks).
    [CreateAssetMenu(menuName = "Forest VR/Enemy Roster")]
    public sealed class ForestEnemyRoster : ScriptableObject
    {
        [System.Serializable]
        public sealed class Enemy
        {
            public string label;
            public GameObject prefab;
            public GoblinSettings settings;
            [Tooltip("Scene object the spawn point is placed next to.")]
            public string anchorName = "SPAWN_DUENDE";
            [Tooltip("Metres from the anchor: z = away from the player's start, x = to the right seen from there.")]
            public Vector2 offset;
        }

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

        public Enemy[] enemies = new Enemy[0];
        public Trunk[] trunks = new Trunk[0];
    }
}
