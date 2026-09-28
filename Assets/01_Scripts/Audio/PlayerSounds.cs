using UnityEngine;

namespace ForestVR
{
    // The player's own sounds: tired, heavy breathing that grows as health drops below the threshold,
    // and a laugh from the dark behind the player when they fall.
    public sealed class PlayerSounds : MonoBehaviour
    {
        Health health;
        Transform head;
        AudioSource breathing;
        PlayerStamina stamina;

        public static PlayerSounds Create(Health playerHealth, Transform playerHead)
        {
            var sounds = playerHead.gameObject.AddComponent<PlayerSounds>();
            sounds.health = playerHealth; sounds.head = playerHead;
            sounds.breathing = playerHead.gameObject.AddComponent<AudioSource>();
            sounds.breathing.loop = true; sounds.breathing.spatialBlend = 0; sounds.breathing.playOnAwake = false;
            playerHealth.Died += sounds.OnDied;
            return sounds;
        }

        void Update()
        {
            var audio = GameAudio.Get;
            if (health == null || audio == null || audio.lowHealthBreathing == null) return;
            float fraction = health.Current / health.Maximum;
            // Silent above the threshold, loudest just before dying.
            float target = health.IsDead ? 0 : Mathf.Clamp01(1 - fraction / audio.lowHealthFraction) * audio.effectsVolume;
            // Also out of breath after running until exhausted.
            if (stamina == null) stamina = health.GetComponent<PlayerStamina>();
            if (stamina != null && !health.IsDead) target = Mathf.Max(target, (stamina.IsExhausted ? .8f : Mathf.Clamp01(.35f - stamina.Fraction) * 1.5f) * audio.effectsVolume);
            if (target > 0 && !breathing.isPlaying) { breathing.clip = audio.lowHealthBreathing; breathing.volume = 0; breathing.Play(); }
            breathing.volume = Mathf.MoveTowards(breathing.volume, target * .9f, Time.deltaTime * .8f);
            if (breathing.isPlaying && breathing.volume <= 0 && target <= 0) breathing.Stop();
        }

        void OnDied()
        {
            var audio = GameAudio.Get;
            if (audio == null || head == null) return;
            var behind = head.position - Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized * 3;
            GameAudio.PlayAt(audio.playerDeathLaugh, behind, 1, Random.Range(.9f, 1f), 15);
        }

        void OnDestroy() { if (health != null) health.Died -= OnDied; }
    }
}
