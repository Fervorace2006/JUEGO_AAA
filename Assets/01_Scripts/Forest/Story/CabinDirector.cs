using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForestVR
{
    // Chapter IV, inside the cabin (InteriorHouse): the lamp Mateo left lit, the drawer of the table, his letter, and the
    // end of the night. Opening the drawer and taking the letter ends the story with the final screen.
    public sealed class CabinDirector : MonoBehaviour
    {
        const string SceneName = "InteriorHouse";
        public const int Chapter = 5;
        public static CabinDirector Current { get; private set; }

        StoryHud hud;
        Transform head;
        NoteReader letter;
        bool picked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName || Current != null) return;
            var go = new GameObject("Cabin Director");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<CabinDirector>();
        }

        void Awake()
        {
            Current = this;
            DrawerNote.Picked += OnLetterPicked;
        }

        void OnDestroy()
        {
            DrawerNote.Picked -= OnLetterPicked;
            if (Current == this) Current = null;
            if (hud != null) Destroy(hud.gameObject);
        }

        void OnLetterPicked(NoteReader reader) { picked = true; letter = reader; }

        IEnumerator Start()
        {
            // Let the rig and the drawer note appear first.
            yield return null;
            var camera = Camera.main;
            if (camera == null) { Debug.LogError("CabinDirector: no hay camara principal."); yield break; }
            head = camera.transform;
            hud = StoryHud.Create(head);
            yield return new WaitForSeconds(1.5f);
            yield return hud.ChapterCard("Capítulo IV", "La cabaña");
            if (!picked)
                yield return hud.Narrate(
                    "La cabaña está vacía. La lámpara de Mateo sigue encendida.",
                    "Sobre la mesa hay un cajón entreabierto.");
            if (!picked)
            {
                hud.SetObjective("Abre el cajón de la mesa y toma la carta de Mateo");
                var note = DrawerNote.Current;
                if (note != null && note.Drawer != null)
                    hud.SetMarker(note.Drawer.handle != null ? note.Drawer.handle.transform : note.Drawer.transform, .35f);
            }
            while (!picked) yield return null;
            hud.SetMarker(null);
            hud.SetObjective("", false);

            // Read the letter, then the end of the night.
            while (letter != null) yield return null;
            yield return hud.Narrate(
                "Mateo subió a la montaña. Y la luna volverá a teñirse de rojo.",
                "Esta noche sobreviviste. La próxima, irás a buscarlo.");
            if (GameAudio.Get != null && GameAudio.Get.menuMusic != null) GameAudio.Music(GameAudio.Get.menuMusic, 4);
            EndingScreen.Show(head);
        }
    }
}
