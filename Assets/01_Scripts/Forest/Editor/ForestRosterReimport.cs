using System.IO;
using UnityEditor;

namespace ForestVR.EditorTools
{
    // When the roster asset arrives together with its script (a pull, a copy), Unity can import the asset before the
    // script is compiled and cache it as "script missing". After every compile, re-import it if it does not load.
    [InitializeOnLoad]
    static class ForestRosterReimport
    {
        const string Path = "Assets/Resources/ForestEnemyRoster.asset";

        static ForestRosterReimport() => EditorApplication.delayCall += Check;

        static void Check()
        {
            if (!File.Exists(Path) || AssetDatabase.LoadAssetAtPath<ForestEnemyRoster>(Path) != null) return;
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
