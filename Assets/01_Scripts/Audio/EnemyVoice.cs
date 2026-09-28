using UnityEngine;

namespace ForestVR
{
    // Sounds of one enemy, driven by what it is doing: snoring while it sleeps, a cry when it wakes,
    // growls while it chases, a sound when it attacks, a laugh when it hurts the player and one when it dies.
    // The clips are in the enemy's GoblinSettings, so goblins, zombies and the werewolf each sound different.
    [RequireComponent(typeof(GoblinActor))]
    public sealed class EnemyVoice : MonoBehaviour
    {
        GoblinActor actor;
        GoblinSettings settings;
        Health player;
        AudioSource loop, voice;
        GoblinActor.State lastState = (GoblinActor.State)(-1);
        float nextChaseVoice;
        bool alerted;

        public static void Attach(GoblinActor actor, Health player)
        {
            var voice = actor.GetComponent<EnemyVoice>();
            if (voice == null) voice = actor.gameObject.AddComponent<EnemyVoice>();
            voice.Setup(actor, player);
        }

        void Setup(GoblinActor owner, Health playerHealth)
        {
            actor = owner; settings = owner.settings; player = playerHealth;
            float range = settings.hearingDistance;
            loop = gameObject.AddComponent<AudioSource>();
            GameAudio.Configure3D(loop, range * .6f);
            loop.loop = true;
            voice = gameObject.AddComponent<AudioSource>();
            GameAudio.Configure3D(voice, range);
            // Every enemy of a kind sounds a little different.
            voice.pitch = loop.pitch = Random.Range(settings.voicePitch.x, settings.voicePitch.y);
            if (player != null) player.Damaged += OnPlayerDamaged;
            actor.Health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (player != null) player.Damaged -= OnPlayerDamaged;
            if (actor != null && actor.Health != null) actor.Health.Died -= OnDied;
        }

        void Update()
        {
            if (actor == null || settings == null) return;
            var state = actor.CurrentState;
            if (state != lastState) Enter(state);
            lastState = state;
            if ((state == GoblinActor.State.Walking || state == GoblinActor.State.Crawling) && Time.time >= nextChaseVoice)
            {
                Say(Pick(settings.chaseSounds), .8f);
                nextChaseVoice = Time.time + Random.Range(settings.chaseSoundInterval.x, settings.chaseSoundInterval.y);
            }
        }

        void Enter(GoblinActor.State state)
        {
            bool resting = state == GoblinActor.State.Resting;
            if (resting && settings.sleepLoop != null)
            {
                loop.clip = settings.sleepLoop; loop.volume = settings.sleepVolume * Effects;
                // Start at a random point so several sleepers do not snore in unison.
                loop.time = Random.Range(0, settings.sleepLoop.length * .9f);
                loop.Play();
            }
            else if (!resting && loop.isPlaying) loop.Stop();

            switch (state)
            {
                case GoblinActor.State.GettingUp:
                case GoblinActor.State.Idle:
                case GoblinActor.State.Walking:
                case GoblinActor.State.Crawling:
                    // One cry when it first wakes up; then only growls while it chases.
                    if (!alerted)
                    {
                        alerted = true;
                        Say(settings.alertSound, 1);
                        nextChaseVoice = Time.time + Random.Range(settings.chaseSoundInterval.x, settings.chaseSoundInterval.y);
                    }
                    break;
                case GoblinActor.State.Attacking:
                    if (Random.value < settings.attackSoundChance) Say(settings.attackSound, 1);
                    break;
            }
        }

        // The player's Health reports where the blow came from: the attacker's own position.
        void OnPlayerDamaged(Vector3 source)
        {
            if (actor == null || actor.Health.IsDead || (source - transform.position).sqrMagnitude > .01f) return;
            if (Random.value < settings.hitPlayerSoundChance) Say(settings.hitPlayerSound, 1);
        }

        void OnDied()
        {
            loop.Stop();
            voice.Stop();
            voice.pitch *= settings.deathPitch;
            Say(settings.deathSound, 1);
        }

        void Say(AudioClip clip, float volume)
        {
            if (clip == null) return;
            voice.PlayOneShot(clip, volume * settings.voiceVolume * Effects);
        }

        static AudioClip Pick(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;
        static float Effects => GameAudio.Get != null ? GameAudio.Get.effectsVolume : 1;
    }
}
