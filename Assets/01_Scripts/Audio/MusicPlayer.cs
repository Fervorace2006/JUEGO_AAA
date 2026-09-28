using UnityEngine;

namespace ForestVR
{
    // Background music of the current scene with a crossfade between tracks.
    public sealed class MusicPlayer : MonoBehaviour
    {
        static MusicPlayer instance;
        AudioSource current, previous;
        float fadeSeconds = 3;
        // Track playing now (null when silent), so it can be resumed after a pause in the music.
        public AudioClip Clip => current != null ? current.clip : null;

        public static MusicPlayer Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("Music").AddComponent<MusicPlayer>();
                return instance;
            }
        }

        void Awake()
        {
            current = NewSource(); previous = NewSource();
        }

        AudioSource NewSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = true; source.spatialBlend = 0; source.playOnAwake = false; source.volume = 0;
            return source;
        }

        public void Play(AudioClip clip, float fade)
        {
            if (clip != null && current.clip == clip && current.isPlaying) return;
            (current, previous) = (previous, current);
            current.clip = clip;
            current.volume = 0;
            if (clip != null) current.Play(); else current.Stop();
            fadeSeconds = Mathf.Max(.05f, fade);
        }

        void Update()
        {
            float target = GameAudio.Get != null ? GameAudio.Get.musicVolume : .35f;
            float step = Time.unscaledDeltaTime / fadeSeconds * Mathf.Max(target, .01f);
            if (current.clip != null) current.volume = Mathf.MoveTowards(current.volume, target, step);
            previous.volume = Mathf.MoveTowards(previous.volume, 0, step);
            if (previous.isPlaying && previous.volume <= 0) previous.Stop();
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
