using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForestVR.EditorTools
{
    // Lays out the route of ForestScene: axe on MESA 1 (where the player starts), bow on MESA 2, revolver on MESA 3,
    // a round spawn zone on each enemy spawn point, and the door of the cabin that leads to the next scene.
    // Safe to run again: it only moves what is still on the first table and only adds what is missing.
    public static class ForestRouteSetup
    {
        [MenuItem("Forest VR/Recorrido/Colocar armas, zonas de spawn y puerta de la cabaña")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "ForestScene") { EditorUtility.DisplayDialog("Recorrido", "Abre ForestScene primero.", "OK"); return; }
            var report = Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("ForestRouteSetup:\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Recorrido", string.Join("\n", report) + "\n\nGuarda la escena (Ctrl+S).", "OK");
        }

        public static List<string> Apply()
        {
            var report = new List<string>();
            var table1 = GameObject.Find("MESA 1");
            MoveToTable("Arco en mesa", table1, "MESA 2", report);
            MoveToTable("Revolver en mesa", table1, "MESA 3", report);
            AddZone("SPAWN_DUENDE", 12, 5, report);
            AddZone("Spawn Zombie", 12, 5, report);
            AddZone("Spawn Wolf", 3, 1, report);
            AddDoor(report);
            return report;
        }

        // Same spot on the new table as it had on the first one (the three tables are the same model).
        static void MoveToTable(string weaponName, GameObject from, string tableName, List<string> report)
        {
            var weapon = GameObject.Find(weaponName);
            var table = GameObject.Find(tableName);
            if (weapon == null || table == null || from == null) { report.Add($"No se encontró {weaponName}, {tableName} o MESA 1."); return; }
            var offset = weapon.transform.position - from.transform.position;
            if (Flat(offset).magnitude > 3) { report.Add($"{weaponName} ya no está en MESA 1: no se mueve."); return; }
            Undo.RecordObject(weapon.transform, "Mover " + weaponName);
            weapon.transform.position = table.transform.position + table.transform.rotation * (Quaternion.Inverse(from.transform.rotation) * offset);
            report.Add($"{weaponName} -> {tableName} {weapon.transform.position}");
        }

        static void AddZone(string pointName, float radius, int count, List<string> report)
        {
            var point = GameObject.Find(pointName);
            if (point == null) { report.Add($"No se encontró {pointName}."); return; }
            var zone = point.GetComponent<SpawnZone>();
            if (zone != null) { report.Add($"{pointName} ya tiene zona ({zone.count} en {zone.radius} m)."); return; }
            zone = Undo.AddComponent<SpawnZone>(point);
            zone.radius = radius; zone.count = count;
            report.Add($"Zona en {pointName}: {count} enemigos en {radius} m de radio.");
        }

        // At the porch, in front of the door (east side of the cabin model), on the ground.
        static void AddDoor(List<string> report)
        {
            if (Object.FindObjectsByType<SceneDoor>(FindObjectsInactive.Include).Length > 0) { report.Add("La puerta de la cabaña ya existe."); return; }
            var house = GameObject.Find("Casita Final");
            if (house == null) { report.Add("No se encontró Casita Final."); return; }
            var bounds = new Bounds(house.transform.position, Vector3.zero);
            foreach (var r in house.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var spot = new Vector3(bounds.max.x + 1.2f, bounds.min.y, bounds.center.z + .5f);
            if (Physics.Raycast(spot + Vector3.up * 20, Vector3.down, out var hit, 40, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) spot = hit.point;
            var door = new GameObject("Puerta Cabaña");
            Undo.RegisterCreatedObjectUndo(door, "Crear puerta");
            door.transform.position = spot;
            door.transform.rotation = Quaternion.LookRotation(Vector3.left);
            var sceneDoor = door.AddComponent<SceneDoor>();
            sceneDoor.targetScene = "InteriorHouse";
            report.Add($"Puerta Cabaña en {spot} -> InteriorHouse (cerrada hasta el final de la historia).");
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
