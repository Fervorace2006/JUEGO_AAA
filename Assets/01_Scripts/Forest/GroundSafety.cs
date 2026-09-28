using System.Collections.Generic;
using UnityEngine;

namespace ForestVR
{
    // Keeps dropped weapons and enemies from falling forever: a thick catch collider under the whole ground,
    // and a query that finds the ground surface above anything that slipped through it.
    public static class GroundSafety
    {
        const float CatchThickness = 2f, CatchMargin = 30f;
        static readonly List<Collider> grounds = new List<Collider>();

        public static void Register(Collider ground)
        {
            if (ground == null || grounds.Contains(ground)) return;
            grounds.RemoveAll(g => g == null);
            grounds.Add(ground);
            // A mesh collider is a surface with no inside; a box this thick cannot be crossed in one physics step.
            var bounds = ground.bounds;
            var safety = new GameObject("Ground Safety Collider (" + ground.name + ")");
            safety.transform.position = new Vector3(bounds.center.x, bounds.min.y - 0.05f - CatchThickness / 2, bounds.center.z);
            var box = safety.AddComponent<BoxCollider>();
            box.size = new Vector3(bounds.size.x + CatchMargin, CatchThickness, bounds.size.z + CatchMargin);
        }

        public static bool IsGround(Collider collider) => collider != null && grounds.Contains(collider);

        // Area covered by the registered ground.
        public static bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var ground in grounds)
            {
                if (ground == null) continue;
                if (found) bounds.Encapsulate(ground.bounds); else { bounds = ground.bounds; found = true; }
            }
            return found;
        }

        // Ground surface straight above or below the position, if it is over the registered ground.
        public static bool TryGetSurface(Vector3 position, out Vector3 surface)
        {
            surface = position;
            bool found = false;
            foreach (var ground in grounds)
            {
                if (ground == null) continue;
                var bounds = ground.bounds;
                var ray = new Ray(new Vector3(position.x, bounds.max.y + 2, position.z), Vector3.down);
                if (ground.Raycast(ray, out var hit, bounds.size.y + 4) && (!found || hit.point.y > surface.y))
                {
                    surface = hit.point; found = true;
                }
            }
            return found;
        }

        // True when the position is clearly under the ground (fell through it) or beside it below its lowest point.
        public static bool IsLost(Vector3 position, float tolerance = 0.5f)
        {
            foreach (var ground in grounds)
                if (ground != null && position.y < ground.bounds.min.y - tolerance) return true;
            return TryGetSurface(position, out var surface) && position.y < surface.y - tolerance;
        }
    }
}
