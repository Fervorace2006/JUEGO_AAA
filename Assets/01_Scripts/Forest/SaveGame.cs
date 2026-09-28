using System.Collections;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ForestVR
{
    // One saved game in the app's storage (on the Quest, inside the headset): the scene, the story chapter, the pages,
    // the player's health, where their head was and looked, and the weapon in each hand. CONTINUAR PARTIDA in the start menu loads it: the saved
    // chapter starts again from its beginning (its enemies come back), with the player at the saved spot and health.
    public static class SaveGame
    {
        [System.Serializable]
        public sealed class Data
        {
            public string scene;
            public int chapter;
            public int pages;
            public float health;
            public Vector3 position;
            public Vector3 forward;
            public float playSeconds;
            public int enemiesDefeated;
            // Weapon held in each hand when saving ("hacha", "arco", "revolver"; empty = nothing).
            public string leftHand;
            public string rightHand;
            public string savedAt;
        }

        static string FilePath => Path.Combine(Application.persistentDataPath, "shadowwood_save.json");

        // Set by CONTINUAR PARTIDA, used once by the scene that loads.
        static Data pending;
        static bool chapterTaken, poseTaken;

        public static bool Exists => File.Exists(FilePath);

        public static Data Read()
        {
            try { return Exists ? JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)) : null; }
            catch (System.Exception e) { Debug.LogWarning("SaveGame: no se pudo leer la partida: " + e.Message); return null; }
        }

        // Saves this moment of the game.
        public static bool SaveNow()
        {
            var camera = Camera.main;
            var head = camera != null ? camera.transform : null;
            var status = Object.FindAnyObjectByType<PlayerCombatStatus>();
            var health = status != null ? status.GetComponent<Health>() : null;
            var director = StoryDirector.Current;
            var forward = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : Vector3.forward;
            var data = new Data
            {
                scene = SceneManager.GetActiveScene().name,
                // In the cabin the story is in its last chapter (CabinDirector).
                chapter = director != null ? director.Chapter : CabinDirector.Current != null ? CabinDirector.Chapter : 0,
                pages = PagePickup.Collected,
                health = health != null ? health.Current : 0,
                position = head != null ? head.position : Vector3.zero,
                forward = forward.sqrMagnitude > .001f ? forward.normalized : Vector3.forward,
                playSeconds = GameStats.PlaySeconds,
                enemiesDefeated = GameStats.EnemiesDefeated,
                leftHand = WeaponName(WeaponGrip.HeldIn(InteractorHandedness.Left)),
                rightHand = WeaponName(WeaponGrip.HeldIn(InteractorHandedness.Right)),
                savedAt = System.DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            };
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
                return true;
            }
            catch (System.Exception e) { Debug.LogWarning("SaveGame: no se pudo guardar: " + e.Message); return false; }
        }

        // CONTINUAR PARTIDA: the scene to load, remembering the save for it. Null without a usable save.
        public static string BeginLoad()
        {
            pending = Read();
            chapterTaken = false; poseTaken = false;
            if (pending == null || string.IsNullOrEmpty(pending.scene)) { pending = null; return null; }
            GameStats.Restore(pending.playSeconds, pending.enemiesDefeated);
            // The forest story sets the pages from its chapter; other scenes (the cabin) keep the saved ones.
            PagePickup.SetCollected(pending.pages);
            return pending.scene;
        }

        // JUGAR starts a new game: nothing to restore.
        public static void ClearPending() { pending = null; chapterTaken = false; poseTaken = false; }

        // The chapter the story starts at: 0 for a new game, the saved one once for a loaded game.
        public static int TakeStartChapter(string scene)
        {
            if (pending == null || chapterTaken || pending.scene != scene) return 0;
            chapterTaken = true;
            return Mathf.Max(0, pending.chapter);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (pending == null || poseTaken || pending.scene != scene.name) return;
            poseTaken = true;
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null) return;
            origin.StartCoroutine(Restore(origin, pending));
        }

        // After the scene has placed the player on its start point (PlayerSpawnPoint moves the head again a second
        // later), put them back where they saved, looking the same way, with the saved health.
        static IEnumerator Restore(XROrigin origin, Data data)
        {
            yield return new WaitForSeconds(1.6f);
            if (origin == null || origin.Camera == null) yield break;
            if (data.forward.sqrMagnitude > .001f) origin.MatchOriginUpCameraForward(Vector3.up, data.forward.normalized);
            origin.MoveCameraToWorldLocation(data.position);
            Physics.SyncTransforms();
            var status = origin.GetComponentInParent<PlayerCombatStatus>();
            if (status == null) status = Object.FindAnyObjectByType<PlayerCombatStatus>();
            var health = status != null ? status.GetComponent<Health>() : null;
            if (health != null && data.health > 0) health.SetCurrent(data.health);
            // Once the player stands where they saved, the weapons they held come back to the same hands.
            yield return new WaitForSeconds(.2f);
            GiveBack(origin, InteractorHandedness.Left, data.leftHand);
            GiveBack(origin, InteractorHandedness.Right, data.rightHand);
        }

        static string WeaponName(WeaponGrip weapon)
        {
            if (weapon == null) return "";
            if (weapon.GetComponent<VRAxe>() != null) return "hacha";
            if (weapon.GetComponent<VRBow>() != null) return "arco";
            if (weapon.GetComponent<VRRevolver>() != null) return "revolver";
            return weapon.name;
        }

        // Puts the saved weapon in that hand as if it had been grabbed: it stays there until Grip is pressed again.
        static void GiveBack(XROrigin origin, InteractorHandedness hand, string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName)) return;
            WeaponGrip weapon = null;
            foreach (var candidate in Object.FindObjectsByType<WeaponGrip>())
                if (!candidate.IsHeld && WeaponName(candidate) == weaponName) { weapon = candidate; break; }
            if (weapon == null || weapon.Grab == null) return;
            XRBaseInputInteractor interactor = null;
            foreach (var candidate in origin.GetComponentsInChildren<XRBaseInputInteractor>())
                if (candidate.isActiveAndEnabled && candidate.handedness == hand && !candidate.hasSelection
                    && (candidate is NearFarInteractor || candidate is XRDirectInteractor)) { interactor = candidate; break; }
            var manager = weapon.Grab.interactionManager;
            if (interactor == null || manager == null) return;
            manager.SelectEnter((IXRSelectInteractor)interactor, (IXRSelectInteractable)weapon.Grab);
        }
    }
}
