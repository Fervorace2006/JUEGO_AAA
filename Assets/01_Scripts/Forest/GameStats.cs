using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForestVR
{
    // Run statistics shown on the final screen and kept in the saved game: play time and enemies defeated.
    // A new game resets them; CONTINUAR PARTIDA puts back the saved ones. Time only runs in the game scenes and stops
    // in the pause menu (it follows the game clock).
    public static class GameStats
    {
        public static float PlaySeconds { get; private set; }
        public static int EnemiesDefeated { get; private set; }

        public static void Reset() { PlaySeconds = 0; EnemiesDefeated = 0; }
        public static void Restore(float seconds, int enemies) { PlaySeconds = Mathf.Max(0, seconds); EnemiesDefeated = Mathf.Max(0, enemies); }
        public static void CountDefeat() => EnemiesDefeated++;

        public static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return total >= 3600 ? $"{total / 3600}:{total / 60 % 60:00}:{total % 60:00}" : $"{total / 60}:{total % 60:00}";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reset();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var clock = new GameObject("Game Stats Clock").AddComponent<Clock>();
            Object.DontDestroyOnLoad(clock.gameObject);
        }

        sealed class Clock : MonoBehaviour
        {
            void Update()
            {
                var scene = SceneManager.GetActiveScene().name;
                if (scene == "ForestScene" || scene == "InteriorHouse") PlaySeconds += Time.deltaTime;
            }
        }
    }
}
