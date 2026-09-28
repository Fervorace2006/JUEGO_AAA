using System.Collections.Generic;
using UnityEngine;

namespace ForestVR
{
    // The nature models import a mesh collider of the whole model. For trees that is the canopy too: the player and
    // weapons bump into leaves and branches, shots stop inside the foliage, and a hollow surface is easy to slip through.
    // Trees get a solid capsule on the trunk instead, and flowers and small plants stop blocking anything.
    public static class ForestColliders
    {
        static readonly string[] Trees = { "Tree" };
        static readonly string[] Decoration = { "Hyacinth", "Daffodil", "Grass", "Flower", "Mushroom", "Pebbles", "Meadow" };
        const float MinTrunk = 0.1f, MaxTrunk = 0.55f, MaxTrunkHeight = 5f;

        public static void Setup()
        {
            var container = new GameObject("Tree Trunk Colliders").transform;
            int trees = 0, plants = 0;
            foreach (var collider in Object.FindObjectsByType<MeshCollider>())
            {
                if (!collider.enabled || GroundSafety.IsGround(collider)) continue;
                // The model name is on the collider object or its parent (the FBX root).
                string name = (collider.transform.parent != null ? collider.transform.parent.name + "/" : "") + collider.name;
                if (Contains(name, Decoration)) { collider.enabled = false; plants++; }
                else if (Contains(name, Trees) && AddTrunk(collider, container)) { collider.enabled = false; trees++; }
            }
            Physics.SyncTransforms();
            Debug.Log($"ForestColliders: {trees} arboles con tronco solido, {plants} plantas sin colision.");
        }

        static bool Contains(string name, string[] words)
        {
            foreach (var word in words) if (name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // Finds the trunk by casting rays at the tree's own mesh collider from all around, at a few heights near the ground.
        // Returns false when no trunk shows near the ground (a separate canopy mesh): its collider is then kept.
        static bool AddTrunk(MeshCollider tree, Transform container)
        {
            var bounds = tree.bounds;
            var pivot = tree.transform.position;
            // Trees are often sunk into the ground: measure from the ground surface at the trunk.
            float baseY = GroundSafety.TryGetSurface(pivot, out var ground) ? Mathf.Max(bounds.min.y, ground.y) : bounds.min.y;
            float reach = Mathf.Max(bounds.extents.x, bounds.extents.z) + 1;
            Vector3 center = new Vector3(pivot.x, 0, pivot.z);
            float radius;
            float bestRadius = float.MaxValue;
            var hits = new List<Vector3>();
            foreach (float height in new[] { 0.5f, 1f, 1.5f })
            {
                float y = baseY + height;
                hits.Clear();
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI * 2 / 12;
                    var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var origin = new Vector3(pivot.x, y, pivot.z) + dir * reach;
                    if (tree.Raycast(new Ray(origin, -dir), out var hit, reach)) hits.Add(hit.point);
                }
                if (hits.Count < 6) continue;
                var mid = Vector3.zero;
                foreach (var p in hits) mid += p;
                mid /= hits.Count;
                float r = 0;
                foreach (var p in hits) r += Vector2.Distance(new Vector2(p.x, p.z), new Vector2(mid.x, mid.z));
                r /= hits.Count;
                // The narrowest slice is the bare trunk; wider ones are branches or roots.
                if (r < bestRadius) { bestRadius = r; center = new Vector3(mid.x, 0, mid.z); }
            }
            if (bestRadius == float.MaxValue) return false;
            radius = Mathf.Clamp(bestRadius, MinTrunk, MaxTrunk);
            float trunkHeight = Mathf.Min(bounds.size.y, MaxTrunkHeight);
            var trunk = new GameObject(tree.name + " Trunk");
            trunk.layer = tree.gameObject.layer;
            trunk.transform.SetParent(container, false);
            trunk.transform.position = new Vector3(center.x, baseY + trunkHeight / 2, center.z);
            var capsule = trunk.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = radius;
            capsule.height = Mathf.Max(trunkHeight, radius * 2);
            return true;
        }
    }
}
