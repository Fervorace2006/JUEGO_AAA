
using UnityEngine;

namespace ForestVR
{
    // Sounds of the game that do not belong to one enemy: weapons, the player, music and the forest ambience.
    // Loaded from Resources/GameAudio so scripts created at runtime can use it without scene wiring.
    [CreateAssetMenu(menuName = "Forest VR/Game Audio")]
    public sealed class GameAudio : ScriptableObject
    {
        [Header("Armas")]
        public AudioClip gunshot;
        [Header("Jugador")]
        [Tooltip("Heavy breathing that loops while the player's health is low.")]
        public AudioClip lowHealthBreathing;
        [Range(0, 1)] public float lowHealthFraction = .35f;
        [Tooltip("Laugh heard in the dark when the player falls.")]
        public AudioClip playerDeathLaugh;
        [Header("Ambiente")]
        public AudioClip forestMusic;
        public AudioClip bossMusic;
        public AudioClip menuMusic;
        [Tooltip("Whispers that come now and then from somewhere around the player.")]
        public AudioClip whispers;
        public Vector2 whisperInterval = new Vector2(45, 100);
        [Tooltip("Sonido al recoger una pagina del diario (opcional).")]
        public AudioClip pagePickup;
        [Header("Volumen")]
        [Range(0, 1)] public float musicVolume = .35f;
        [Range(0, 1)] public float effectsVolume = 1;

        static GameAudio instance;
        public static GameAudio Get
        {
            get
            {
                if (instance == null) instance = Resources.Load<GameAudio>("GameAudio");
                return instance;
            }
        }

        // One-shot 3D sound at a point in the world.
        public static AudioSource PlayAt(AudioClip clip, Vector3 position, float volume = 1, float pitch = 1, float maxDistance = 35)
        {
            if (clip == null) return null;
            var go = new GameObject("Sound: " + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            Configure3D(source, maxDistance);
            source.clip = clip; source.pitch = pitch;
            source.volume = volume * (Get != null ? Get.effectsVolume : 1);
            source.Play();
            Destroy(go, clip.length / Mathf.Max(.1f, pitch) + .1f);
            return source;
        }

        public static void Configure3D(AudioSource source, float maxDistance)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0;
        }

        // Crossfades the background music to a new track (null fades it out).
        public static void Music(AudioClip clip, float fadeSeconds = 3) => MusicPlayer.Instance.Play(clip, fadeSeconds);
    }
}
