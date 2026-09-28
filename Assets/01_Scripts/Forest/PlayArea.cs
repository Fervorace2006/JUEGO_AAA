using System.Collections.Generic;
using UnityEngine;

namespace ForestVR
{
    // Where you can stand in ForestScene: on the green ground of FloorWithLake, not on the lake (the light-blue water mesh)
    // and not past the edges of the floor. Shared by the player guard, the enemy spawn zones and enemy steering.
    public static class PlayArea
    {
        const float EdgeMargin = 1.5f;
        static Collider floor;
        static readonly List<Collider> water = new List<Collider>();
        static bool searched;

        static void Find()
        {
            if (searched && floor != null) return;
            searched = true;
            water.Clear();
            var root = GameObject.Find("FloorWithLake");
            if (root == null) return;
            floor = root.GetComponent<Collider>();
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (c != floor && c.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0) water.Add(c);
        }

        public static bool Ready { get { Find(); return floor != null; } }

        // Inside the floor, away from its edges.
        public static bool InsideEdges(Vector3 position)
        {
            Find();
            if (floor == null) return true;
            var b = floor.bounds;
            return position.x > b.min.x + EdgeMargin && position.x < b.max.x - EdgeMargin
                && position.z > b.min.z + EdgeMargin && position.z < b.max.z - EdgeMargin;
        }

        // The lake: the water surface is what you would stand on there (it is above the lake bed).
        public static bool IsWater(Vector3 position)
        {
            Find();
            if (floor == null || water.Count == 0) return false;
            var top = floor.bounds.max.y + 5;
            var ray = new Ray(new Vector3(position.x, top, position.z), Vector3.down);
            float depth = floor.bounds.size.y + 15;
            float groundDistance = floor.Raycast(ray, out var groundHit, depth) ? groundHit.distance : float.MaxValue;
            foreach (var w in water)
                if (w != null && w.enabled && w.Raycast(ray, out var waterHit, depth) && waterHit.distance < groundDistance - 0.02f) return true;
            return false;
        }

        public static bool IsWalkable(Vector3 position) => InsideEdges(position) && !IsWater(position);

        // Ground point under the position when it is walkable ground.
        public static bool TryGetWalkableGround(Vector3 position, out Vector3 ground)
        {
            ground = position;
            Find();
            if (floor == null || !IsWalkable(position)) return false;
            var ray = new Ray(new Vector3(position.x, floor.bounds.max.y + 5, position.z), Vector3.down);
            if (!floor.Raycast(ray, out var hit, floor.bounds.size.y + 15)) return false;
            ground = hit.point;
            return Vector3.Angle(hit.normal, Vector3.up) <= 35;
        }
    }
}
