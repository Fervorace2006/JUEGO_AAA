using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ForestVR
{
    // Round spawn area on a spawn point of a GoblinSpawner (Duende, Zombie, Hombre Lobo). The spawner keeps `count`
    // enemies alive inside the circle: they appear at random spots within `radius`, apart from each other, only on
    // walkable ground (never in the lake or past the edges of the map). A dead one comes back after the spawner's
    // respawn time. Each enemy still uses its own wake and vision ranges from its settings.
    public sealed class SpawnZone : MonoBehaviour
    {
        [Tooltip("Radio de la zona redonda, en metros.")]
        [Min(0.5f)] public float radius = 10;
        [Tooltip("Cuantos enemigos aparecen (y se mantienen) dentro de la zona.")]
        [Min(1)] public int count = 5;
        [Tooltip("Distancia minima entre dos enemigos de la zona.")]
        [Min(0.5f)] public float spacing = 2.5f;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Handles.color = new Color(.2f, .8f, 1f, .9f);
            Handles.DrawWireDisc(transform.position, Vector3.up, radius, 2);
            Handles.color = new Color(.2f, .8f, 1f, .06f);
            Handles.DrawSolidDisc(transform.position, Vector3.up, radius);
        }

        void OnDrawGizmosSelected()
        {
            var spawner = GetComponentInParent<GoblinSpawner>();
            if (spawner == null)
                foreach (var s in FindObjectsByType<GoblinSpawner>(FindObjectsInactive.Include))
                    if (System.Array.IndexOf(s.spawnPoints, transform) >= 0) { spawner = s; break; }
            string enemy = spawner != null && spawner.settings != null ? spawner.settings.displayName : "sin spawner";
            Handles.Label(transform.position + Vector3.up * 1.5f, $"Zona: {count} x {enemy} | radio {radius:0.#} m");
        }
#endif
    }
}
