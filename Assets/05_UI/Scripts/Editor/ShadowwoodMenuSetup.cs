using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JuegoAAA.UI.Editor
{
    // Creates the start menu scene (XR rig + ShadowwoodMenu) and puts it first in Build Settings, before ForestScene.
    // Runs once by itself after compiling if the scene does not exist; the menu item rebuilds it.
    [InitializeOnLoad]
    static class ShadowwoodMenuSetup
    {
        const string MenuScene = "Assets/00_Scenes/MainMenu.unity";
        const string GameScene = "Assets/00_Scenes/ForestScene.unity";
        // Inside of the cabin, loaded by the door at the end of the story.
        const string CabinScene = "Assets/00_Scenes/InteriorHouse.unity";
        const string Rig = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Hands Variant.prefab";
        const string Background = "Assets/05_UI/Textures/ShadowwoodMenu.png";
        const string TitleFont = "Assets/05_UI/Resources/Fonts/CinzelDecorative-Bold.ttf";
        const string TextFont = "Assets/05_UI/Resources/Fonts/Cinzel.ttf";

        static ShadowwoodMenuSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(MenuScene)) Create();
                else if (EditorBuildSettings.scenes.Length < 3 || EditorBuildSettings.scenes[0].path != MenuScene
                    || System.Array.FindIndex(EditorBuildSettings.scenes, s => s.path == CabinScene) < 0) AddToBuild();
                ApplyStartScene();
            };
        }

        // Play in the editor starts from the menu whatever scene is open, like the built game does.
        const string StartFromMenuPref = "Shadowwood.StartFromMenu";
        const string StartFromMenuItem = "Shadowwood/Empezar Play siempre desde el menu";

        [MenuItem(StartFromMenuItem)]
        static void ToggleStartFromMenu()
        {
            EditorPrefs.SetBool(StartFromMenuPref, !EditorPrefs.GetBool(StartFromMenuPref, true));
            ApplyStartScene();
        }

        [MenuItem(StartFromMenuItem, true)]
        static bool ToggleStartFromMenuValidate()
        {
            Menu.SetChecked(StartFromMenuItem, EditorPrefs.GetBool(StartFromMenuPref, true));
            return true;
        }

        static void ApplyStartScene()
        {
            bool enabled = EditorPrefs.GetBool(StartFromMenuPref, true);
            EditorSceneManager.playModeStartScene = enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) : null;
        }

        [MenuItem("Shadowwood/Crear o actualizar menu de inicio")]
        static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ConfigureTexture();
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Rig);
            if (rigPrefab == null) { Debug.LogError("No se encontro el rig VR: " + Rig); return; }

            // Built in an extra scene so the scene open in the editor is left untouched.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Invisible floor so the rig's gravity has something to stand on.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Menu Floor (invisible)";
            floor.transform.localScale = new Vector3(4, 1, 4);
            Object.DestroyImmediate(floor.GetComponent<MeshRenderer>());
            SceneManager.MoveGameObjectToScene(floor, scene);

            var menuObject = new GameObject("Shadowwood Menu");
            SceneManager.MoveGameObjectToScene(menuObject, scene);
            var menu = menuObject.AddComponent<ShadowwoodMenu>();
            menu.background = AssetDatabase.LoadAssetAtPath<Texture2D>(Background);
            menu.titleFont = AssetDatabase.LoadAssetAtPath<Font>(TitleFont);
            menu.textFont = AssetDatabase.LoadAssetAtPath<Font>(TextFont);
            menu.gameScene = Path.GetFileNameWithoutExtension(GameScene);

            EditorSceneManager.SaveScene(scene, MenuScene);
            EditorSceneManager.CloseScene(scene, true);
            AddToBuild();
            ApplyStartScene();
            Debug.Log("Menu de inicio creado en " + MenuScene + " y puesto primero en Build Settings.");
        }

        static void ConfigureTexture()
        {
            if (!(AssetImporter.GetAtPath(Background) is TextureImporter importer)) return;
            // Keep the artwork's 3:2 shape and full detail on the big VR panel.
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }

        static void AddToBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(MenuScene, true));
            scenes.Add(new EditorBuildSettingsScene(GameScene, true));
            if (File.Exists(CabinScene)) scenes.Add(new EditorBuildSettingsScene(CabinScene, true));
            foreach (var existing in EditorBuildSettings.scenes)
                if (existing.path != MenuScene && existing.path != GameScene && existing.path != CabinScene) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
