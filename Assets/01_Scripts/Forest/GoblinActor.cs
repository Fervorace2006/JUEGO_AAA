using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ForestVR
{
    [RequireComponent(typeof(Health), typeof(CharacterController))]
    public sealed class GoblinActor : MonoBehaviour
    {
        public enum State { Resting, GettingUp, Idle, Walking, Attacking, TurningFromHit, Dead, Crawling }
        public GoblinSettings settings;
        [Tooltip("Animator whose hierarchy the clips animate. Empty = the first one under this object.")]
        public Animator animator;
        public State CurrentState { get; private set; }
        Health health, targetHealth;
        Transform target;
        CharacterController body;
        const float AttackFadeIn = 0.12f, AttackFadeOut = 0.3f;
        // Moment of the swing in each attack clip, measured once from the clip itself.
        static readonly Dictionary<AnimationClip, float> strikeTimes = new Dictionary<AnimationClip, float>();
        PlayableGraph graph;
        AnimationPlayableOutput output;
        // Two-slot mixer: slot 0 is the clip playing now, slot 1 the one fading out, so every change crossfades.
        AnimationMixerPlayable mixer;
        AnimationClipPlayable playing, previous;
        AnimationClip currentClip, lastAttackClip;
        float blend = 1, fadeDuration;
        float nextAttack, impactAt = -1, stateEnds, verticalSpeed, lastSeenAt, pendingDamage;
        Vector3 lastKnown;
        bool remembersTarget;
        int attackRepeats;
        float movedAt;
        // After being hurt it knows where the player is for a while, even out of sight, and goes for them.
        const float AggroSeconds = 10;
        // Closer than this it notices the player even behind its back (it hears and smells them).
        const float CloseAwareness = 2.5f;
        float aggroUntil, turnStarted, lastHealth, animSpeed = 1;
        const float ReachMargin = .35f;
        bool inReach, turningInPlace;
        Vector3 knockback;
        Transform hips;
        Vector3 hipsRest;
        public Health Health => health;
        public AnimationClip CurrentAnimation => currentClip;

        public void Initialize(GoblinSettings config, Transform playerHead, Health playerHealth)
        {
            settings = config; target = playerHead; targetHealth = playerHealth;
            health = GetComponent<Health>(); health.Initialize(settings.health); lastHealth = health.Current;
            body = GetComponent<CharacterController>();
            health.Died += Die; health.Damaged += ReactToHit;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null;
                // The Animator is on Armature while the skinned renderer is on a sibling.
                // Visibility culling treats it as hidden and freezes the bones even on screen.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                MeasureStrike(animator.transform, settings.attack);
                MeasureStrike(animator.transform, settings.slowAttack);
                graph = PlayableGraph.Create("Goblin animations");
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                output = AnimationPlayableOutput.Create(graph, "Body", animator);
                mixer = AnimationMixerPlayable.Create(graph, 2);
                output.SetSourcePlayable(mixer);
                graph.Play();
                foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
                    if (bone.name == "Hips" || bone.name == "Hip") { hips = bone; hipsRest = transform.InverseTransformPoint(bone.position); break; }
                if (settings.snapFeetToGround) SetupGrounding();
            }
            EnemyNameplate.Create(this, target);
            Rest();
            EnemyVoice.Attach(this, targetHealth);
        }
        void Play(AnimationClip clip, bool restart = false, float fade = 0.25f, float speed = 1)
        {
            if (!graph.IsValid() || clip == null || (!restart && currentClip == clip)) return;
            if (previous.IsValid())
            {
                // Changing again in the middle of a crossfade: keep whichever pose is showing most, drop the other,
                // so the body does not jump back to a pose that had almost faded out.
                graph.Disconnect(mixer, 1); graph.Disconnect(mixer, 0);
                if (blend < .5f) { graph.DestroyPlayable(playing); playing = previous; }
                else graph.DestroyPlayable(previous);
                previous = default;
                graph.Connect(playing, 0, mixer, 0);
            }
            if (playing.IsValid()) { graph.Disconnect(mixer, 0); previous = playing; graph.Connect(previous, 0, mixer, 1); }
            playing = AnimationClipPlayable.Create(graph, clip);
            playing.SetApplyFootIK(false); playing.SetSpeed(speed);
            graph.Connect(playing, 0, mixer, 0);
            currentClip = clip; fadeDuration = fade;
            blend = fade > 0 && previous.IsValid() ? 0 : 1;
            ApplyBlend();
        }
        // Looping movement clip at a playback speed that matches how fast the body really moves (no sliding feet).
        void PlayAt(AnimationClip clip, float speed)
        {
            Play(clip);
            if (clip != null && currentClip == clip && playing.IsValid()) playing.SetSpeed(speed);
        }
        void ApplyBlend()
        {
            mixer.SetInputWeight(0, blend);
            mixer.SetInputWeight(1, previous.IsValid() ? 1 - blend : 0);
            if (blend >= 1 && previous.IsValid()) { graph.Disconnect(mixer, 1); graph.DestroyPlayable(previous); }
        }
        // Samples the clip on this goblin and keeps the time a hand reaches farthest forward: the moment the blow lands.
        void MeasureStrike(Transform root, AnimationClip clip)
        {
            if (clip == null || strikeTimes.ContainsKey(clip)) return;
            var hands = new List<Transform>();
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name != "R_Forearm" && bone.name != "L_Forearm" && bone.name != "Right_LowerArm" && bone.name != "Left_LowerArm") continue;
                // The deepest point under the forearm (hand or fingertip), in case the hand bone has another name.
                Transform tip = bone; float far = 0;
                foreach (var child in bone.GetComponentsInChildren<Transform>(true))
                {
                    float d = (child.position - bone.position).sqrMagnitude;
                    if (d > far) { far = d; tip = child; }
                }
                hands.Add(tip);
            }
            float best = float.NegativeInfinity, strike = -1;
            if (hands.Count > 0)
            {
                const int samples = 40;
                for (int i = 0; i <= samples; i++)
                {
                    // Ignore the very start and end: wind-up and recovery.
                    float time = clip.length * Mathf.Lerp(0.15f, 0.85f, i / (float)samples);
                    clip.SampleAnimation(root.gameObject, time);
                    foreach (var hand in hands)
                    {
                        float reach = transform.InverseTransformPoint(hand.position).z;
                        if (reach > best) { best = reach; strike = time; }
                    }
                }
            }
            strikeTimes[clip] = strike;
        }
        float StrikeTime(AnimationClip clip, float fallback) =>
            clip != null && strikeTimes.TryGetValue(clip, out float time) && time > 0 ? time : fallback;
        void Rest()
        {
            CurrentState = State.Resting; remembersTarget = false;
            var clip = Random.value < 0.5f ? settings.sleep : settings.relaxing;
            Play(clip != null ? clip : settings.idle);
        }
        void Wake()
        {
            CurrentState = State.GettingUp;
            Play(settings.gettingUp != null ? settings.gettingUp : settings.idle, true, 0.35f);
            stateEnds = Time.time + (settings.gettingUp != null ? settings.gettingUp.length : 0.2f);
        }
        void Update()
        {
            if (settings == null || health == null || health.IsDead) return;
            if (GroundSafety.IsLost(transform.position)) PlaceOnGround();
            verticalSpeed = body.isGrounded ? -2 : verticalSpeed + Physics.gravity.y * Time.deltaTime;
            body.Move(Vector3.up * (verticalSpeed * Time.deltaTime));
            // Pushed back a little by each blow, stronger for light enemies.
            if (knockback.sqrMagnitude > .0001f)
            {
                body.Move(knockback * Time.deltaTime);
                knockback = Vector3.MoveTowards(knockback, Vector3.zero, Time.deltaTime * 6);
            }
            if (target == null || targetHealth == null || targetHealth.IsDead || !target.gameObject.activeInHierarchy)
            { impactAt = -1; remembersTarget = false; if (CurrentState != State.Resting) Idle(); return; }
            var delta = Flat(target.position - transform.position);
            float distance = delta.magnitude;
            if (CurrentState == State.Resting)
            {
                if (distance <= settings.wakeRadius && HasLineOfSight()) Wake();
                return;
            }
            if (CurrentState == State.GettingUp)
            { if (Time.time >= stateEnds) Idle(); return; }
            if (CurrentState == State.TurningFromHit)
            {
                // Spins round toward whoever hit it, then goes straight for them: a blow if they are close, a chase if not.
                Face(target.position, settings.turnSpeed * 3);
                bool facing = InFront(delta, 40);
                if (facing && Time.time - turnStarted > .35f || Time.time >= stateEnds)
                {
                    if (distance <= settings.attackRange && Time.time >= nextAttack) Attack(); else Idle();
                }
                return;
            }
            if (CurrentState == State.Attacking)
            {
                if (impactAt >= 0)
                {
                    // Wind-up: keep tracking the player and step in so the blow reaches; after the swing it is committed.
                    Face(target.position, settings.turnSpeed * 1.5f);
                    if (distance > settings.attackRange * 0.6f)
                        body.Move(delta.normalized * (Mathf.Min(settings.speed * 1.2f, distance - settings.attackRange * 0.6f) * Time.deltaTime));
                }
                if (impactAt >= 0 && Time.time >= impactAt)
                {
                    impactAt = -1;
                    if (distance <= settings.attackRange && InFront(delta, 100) && HasLineOfSight())
                        targetHealth.TakeHit(pendingDamage, transform.position);
                }
                if (Time.time >= stateEnds) Idle();
                return;
            }
            // It knows where the player is if it sees them, if they are right next to it (even behind its back),
            // or for a while after being hurt.
            bool aware = CanSeeTarget() || (distance <= CloseAwareness && HasLineOfSight()) || Time.time < aggroUntil;
            if (aware)
            { lastKnown = target.position; lastSeenAt = Time.time; remembersTarget = true; }
            // A margin on the way out and on the turn, so a player standing right at the edge of its reach does not make
            // it switch between walking and standing every frame (each switch restarted the crossfade: jerky body).
            inReach = aware && distance <= settings.attackRange + (inReach ? ReachMargin : 0);
            if (inReach)
            {
                Face(target.position, settings.turnSpeed * 1.5f);
                // Only swing once it faces the player; while turning in place its feet step instead of sliding.
                turningInPlace = !InFront(delta, turningInPlace ? 15 : 30);
                if (InFront(delta, 70) && Time.time >= nextAttack) Attack();
                else if (turningInPlace && settings.walk != null) { CurrentState = State.Idle; PlayAt(settings.walk, .6f); }
                else Idle();
                return;
            }
            turningInPlace = false;
            if (remembersTarget && Time.time - lastSeenAt <= settings.memorySeconds && Flat(lastKnown - transform.position).magnitude > 0.5f)
            {
                // Head for the player, but around trees, rocks and walls instead of pushing into them.
                var heading = Steer(Flat(lastKnown - transform.position).normalized);
                Face(transform.position + heading, settings.turnSpeed);
                var before = transform.position;
                bool wounded = settings.woundedWalk != null && settings.woundedThreshold > 0
                    && health.Current <= health.Maximum * settings.woundedThreshold;
                bool running = (wounded ? settings.woundedRun != null && settings.woundedRunSpeed > 0
                    : settings.run != null && settings.runSpeed > 0)
                    && Flat(lastKnown - transform.position).magnitude > settings.runDistance;
                float moveSpeed = wounded ? (running ? settings.woundedRunSpeed : settings.woundedSpeed)
                    : (running ? settings.runSpeed : settings.speed);
                body.Move(heading * (moveSpeed * Time.deltaTime));
                if (CurrentState != State.Walking && CurrentState != State.Crawling) movedAt = Time.time;
                CurrentState = wounded ? State.Crawling : State.Walking;
                // Only fall back to idle after being blocked for a moment, so the walk cycle does not restart every frame.
                var moved = Flat(transform.position - before).magnitude;
                if (moved > 0.001f) movedAt = Time.time;
                var locomotion = wounded ? (running ? settings.woundedRun : settings.woundedWalk)
                    : (running ? settings.run : settings.walk);
                // Steps follow the real speed: slower when squeezing past trees or slopes, never faster than 30 %.
                float ratio = moveSpeed > 0 ? Mathf.Clamp(moved / Mathf.Max(Time.deltaTime, .001f) / moveSpeed, .6f, 1.3f) : 1;
                animSpeed = Mathf.Lerp(animSpeed, ratio, 1 - Mathf.Exp(-8 * Time.deltaTime));
                if (Time.time - movedAt < 0.25f) PlayAt(locomotion, animSpeed); else Idle();
            }
            else
            {
                Idle(); remembersTarget = false;
                // Once awakened, this enemy stays alert; sleep is only its initial state.
            }
        }
        static readonly float[] SteerAngles = { 0, 35, -35, 70, -70, 105, -105 };
        readonly RaycastHit[] steerHits = new RaycastHit[8];
        float avoidSide = 1;
        // First direction near the desired one where the body fits for the next ~0.9 m. Keeps turning to the same side
        // while going around an obstacle so it does not hesitate between left and right.
        Vector3 Steer(Vector3 desired)
        {
            if (desired.sqrMagnitude < 0.001f) return desired;
            float radius = body.radius * 0.9f;
            var bottom = transform.position + Vector3.up * (body.stepOffset + radius);
            var top = transform.position + Vector3.up * Mathf.Max(body.height - radius, body.stepOffset + radius);
            foreach (float angle in SteerAngles)
            {
                var direction = Quaternion.Euler(0, angle * avoidSide, 0) * desired;
                if (!Blocked(bottom, top, radius, direction)) { if (angle != 0) avoidSide = Mathf.Sign(angle * avoidSide); return direction; }
            }
            return desired;
        }
        bool Blocked(Vector3 bottom, Vector3 top, float radius, Vector3 direction)
        {
            // The lake and the edges of the map are off limits for enemies too.
            if (!PlayArea.IsWalkable(transform.position + direction * 1.2f) && PlayArea.IsWalkable(transform.position)) return true;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, steerHits, 0.9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = steerHits[i];
                if (hit.transform.IsChildOf(transform) || (targetHealth != null && hit.transform.IsChildOf(targetHealth.transform))) continue;
                // Walkable ground and gentle slopes do not block.
                if (GroundSafety.IsGround(hit.collider) || Vector3.Angle(hit.normal, Vector3.up) < body.slopeLimit) continue;
                if (hit.collider.GetComponentInParent<GoblinActor>() != null) continue;
                return true;
            }
            return false;
        }
        void Idle()
        {
            CurrentState = State.Idle; Play(settings.idle);
        }
        void Attack()
        {
            // Mostly alternate the quick and the slow attack; never the same one three times in a row.
            bool slow = settings.slowAttack != null && settings.attack != null
                ? (attackRepeats >= 2 ? lastAttackClip != settings.slowAttack : Random.value < (lastAttackClip == settings.slowAttack ? 0.3f : 0.7f))
                : settings.slowAttack != null;
            var clip = slow ? settings.slowAttack : settings.attack;
            attackRepeats = clip == lastAttackClip ? attackRepeats + 1 : 1; lastAttackClip = clip;
            // Slight speed variation so repeated attacks do not look identical; the impact follows the speed.
            float speed = Random.Range(0.95f, 1.12f);
            float delay = StrikeTime(clip, slow ? settings.slowAttackImpactDelay : settings.attackImpactDelay) / speed;
            pendingDamage = slow ? settings.slowAttackDamage : settings.damage;
            CurrentState = State.Attacking; Play(clip, true, AttackFadeIn, speed);
            impactAt = Time.time + delay;
            // Leave a little early so the recovery blends into the next pose instead of snapping.
            float length = clip != null ? clip.length / speed : 1;
            stateEnds = Time.time + Mathf.Max(delay + 0.1f, length - AttackFadeOut);
            nextAttack = Mathf.Max(stateEnds, Time.time + settings.attackCooldown);
        }
        void ReactToHit(Vector3 source)
        {
            float damage = Mathf.Max(0, lastHealth - health.Current);
            lastHealth = health.Current;
            if (health.IsDead) return;
            // Hurt: it now knows where the player is and hunts them for a while, wherever the shot came from.
            aggroUntil = Time.time + Mathf.Max(AggroSeconds, settings.memorySeconds);
            lastKnown = target != null ? target.position : source; lastSeenAt = Time.time; remembersTarget = true;
            // Recoil away from the blow; heavy enemies (the werewolf) barely move.
            float weight = health.Maximum > 300 ? .3f : 1;
            knockback = Flat(transform.position - source).normalized * (Mathf.Clamp(damage / health.Maximum * 8, .3f, 1.6f) * weight);
            if (CurrentState == State.Resting) { Wake(); return; }
            bool behind = Vector3.Dot(transform.forward, Flat(source - transform.position).normalized) < -0.15f;
            // A blow in the back makes it turn round and fight back — also while recovering from its own attack.
            bool canTurn = CurrentState == State.Walking || CurrentState == State.Crawling || CurrentState == State.Idle
                || CurrentState == State.Attacking && impactAt < 0;
            if (behind && canTurn)
            {
                impactAt = -1; CurrentState = State.TurningFromHit; turnStarted = Time.time;
                var clip = settings.attackedFromBack != null ? settings.attackedFromBack : settings.walk;
                Play(clip != null ? clip : settings.idle, true, .15f, 1.2f);
                // The turn ends as soon as it faces the player; this is only the longest it may take.
                stateEnds = Time.time + Mathf.Min(clip != null ? clip.length / 1.2f : 1, 1.4f);
            }
        }
        static Vector3 Flat(Vector3 value) { value.y = 0; return value; }
        bool InFront(Vector3 delta, float angle) => delta.sqrMagnitude < 0.001f || Vector3.Angle(transform.forward, delta) <= angle * 0.5f;
        public bool CanSeeTarget()
        {
            if (target == null) return false;
            var delta = Flat(target.position - transform.position);
            return delta.magnitude <= settings.detectionRadius && InFront(delta, settings.visionAngle) && HasLineOfSight();
        }
        void Face(Vector3 position, float speed)
        {
            var direction = Flat(position - transform.position);
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), speed * Time.deltaTime);
        }
        bool HasLineOfSight()
        {
            var from = transform.position + Vector3.up * body.height * 0.8f;
            var direction = target.position - from;
            foreach (var hit in Physics.RaycastAll(from, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                var weapon = hit.collider.GetComponentInParent<WeaponGrip>();
                if (weapon != null && weapon.Owner == targetHealth) continue;
                // Other enemies do not hide the player.
                if (hit.collider.GetComponentInParent<GoblinActor>() != null) continue;
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(targetHealth.transform)) return false;
            }
            return true;
        }
        void Die()
        {
            impactAt = -1; CurrentState = State.Dead;
            // The corpse has no collision or gravity: leave it lying on the ground, not floating or under it.
            if (GroundSafety.IsLost(transform.position)) PlaceOnGround(); else DropToFloorBelow();
            body.enabled = false;
            if (settings.dropsPages) PagePickup.TryDrop(transform.position + transform.forward * .5f, settings.pagePrefab);
            Play(settings.death, true);
            // Sparkles now; once the fall ends the body turns to ash that drifts up, and the enemy is removed.
            float fall = settings.death != null ? settings.death.length : 1;
            EnemyAshes.Begin(gameObject, fall);
            Destroy(gameObject, Mathf.Max(settings.corpseSeconds, fall + 12));
        }
        void PlaceOnGround()
        {
            if (!GroundSafety.TryGetSurface(transform.position, out var surface)) return;
            bool wasEnabled = body.enabled;
            body.enabled = false;
            transform.position = surface;
            body.enabled = wasEnabled;
            verticalSpeed = 0;
        }
        // Lands the corpse on whatever is under it (ground, rock, bridge), so it neither floats nor sinks.
        void DropToFloorBelow()
        {
            float lift = body.height * 0.5f;
            float best = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * lift, Vector3.down, lift + 3, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < best) best = hit.distance;
            if (!float.IsPositiveInfinity(best)) transform.position += Vector3.down * (best - lift);
        }
        void LateUpdate()
        {
            if (graph.IsValid() && blend < 1)
            {
                blend = fadeDuration > 0 ? Mathf.MoveTowards(blend, 1, Time.deltaTime / fadeDuration) : 1;
                ApplyBlend();
            }
            if (health != null && health.IsDead && playing.IsValid() && currentClip != null && playing.GetTime() >= currentClip.length)
            { playing.SetTime(Mathf.Max(0, currentClip.length - 0.001f)); playing.SetSpeed(0); }
            // Clips imported without "Loop Time" would freeze on their last frame in the looping states.
            else if (playing.IsValid() && currentClip != null && !currentClip.isLooping && IsLoopingClip(currentClip) && playing.GetTime() >= currentClip.length)
                playing.SetTime(playing.GetTime() % currentClip.length);
            if (hips != null && settings != null && settings.lockHipsInPlace)
            {
                var local = transform.InverseTransformPoint(hips.position);
                local.x = hipsRest.x; local.z = hipsRest.z;
                hips.position = transform.TransformPoint(local);
            }
            if (skeleton != null) GroundFeet();
        }

        // ---------- Feet on the ground ----------
        // The goblin's clips raise or lower the whole skeleton (its idle sits ~15 cm lower than its walk and attacks),
        // so a fixed model offset left it floating or sunk. After each animated frame the skeleton is shifted so the
        // soles touch the ground under it; once dead, so the lowest part of the fallen body rests on it.
        Transform skeleton;
        Transform[] feet, bones;
        Vector3 skeletonRest;
        float soleBelowFeet, groundShift;
        bool groundShiftReady;
        readonly RaycastHit[] groundHits = new RaycastHit[8];
        const float BodyThickness = .07f, MaxShift = .6f;
        static readonly Dictionary<Mesh, float> soleDepths = new Dictionary<Mesh, float>();

        void SetupGrounding()
        {
            var list = new List<Transform>();
            var body = new List<Transform>();
            foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
            {
                var n = bone.name.ToLowerInvariant();
                if (n.Contains("foot") || n.Contains("toe")) list.Add(bone);
                // The fallen body: every bone except the skeleton's container and its root, which stay at floor level.
                if (bone != animator.transform && !n.Contains("root") && !n.Contains("armature") && bone.GetComponent<Renderer>() == null) body.Add(bone);
            }
            if (list.Count == 0) return;
            feet = list.ToArray();
            bones = body.ToArray();
            skeleton = animator.transform;
            skeletonRest = skeleton.localPosition;
            // How far the mesh sole reaches below the lowest foot bone, measured once per model in the idle pose.
            var skin = GetComponentInChildren<SkinnedMeshRenderer>();
            soleBelowFeet = .03f;
            if (skin != null && skin.sharedMesh != null)
            {
                if (!soleDepths.TryGetValue(skin.sharedMesh, out float depth))
                {
                    if (settings.idle != null) settings.idle.SampleAnimation(animator.gameObject, 0);
                    var baked = new Mesh();
                    skin.BakeMesh(baked, true);
                    var vertices = baked.vertices;
                    var pose = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    float lowest = float.PositiveInfinity;
                    foreach (var v in vertices) lowest = Mathf.Min(lowest, pose.MultiplyPoint3x4(v).y);
                    Destroy(baked);
                    depth = Mathf.Clamp(LowestY(feet) - lowest, 0, .2f);
                    soleDepths[skin.sharedMesh] = depth;
                }
                soleBelowFeet = depth;
            }
        }

        void GroundFeet()
        {
            // Back to the prefab offset first, so the shift is measured from the animated pose of this frame.
            skeleton.localPosition = skeletonRest;
            if (!TryGround(out float ground)) return;
            bool dead = health != null && health.IsDead;
            float lowest = dead ? LowestY(bones) - BodyThickness : LowestY(feet) - soleBelowFeet;
            float shift = Mathf.Clamp(ground - lowest, -MaxShift, MaxShift);
            // Smooth, so a step onto a root or a rock does not snap the body.
            groundShift = groundShiftReady ? Mathf.Lerp(groundShift, shift, 1 - Mathf.Exp(-15 * Time.deltaTime)) : shift;
            groundShiftReady = true;
            skeleton.position += Vector3.up * groundShift;
        }

        static float LowestY(Transform[] list)
        {
            float lowest = float.PositiveInfinity;
            foreach (var t in list) if (t != null) lowest = Mathf.Min(lowest, t.position.y);
            return lowest;
        }

        // First solid surface below the goblin: ground, rock, bridge; never itself, another enemy, the player or a weapon.
        bool TryGround(out float height)
        {
            height = 0;
            var from = transform.position + Vector3.up * .8f;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, groundHits, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<GoblinActor>() != null
                    || hit.collider.GetComponentInParent<WeaponGrip>() != null
                    || (targetHealth != null && hit.transform.IsChildOf(targetHealth.transform))) continue;
                if (hit.distance < best) { best = hit.distance; height = hit.point.y; }
            }
            return !float.IsPositiveInfinity(best);
        }
        bool IsLoopingClip(AnimationClip clip) =>
            clip == settings.idle || clip == settings.walk || clip == settings.run || clip == settings.woundedWalk
            || clip == settings.woundedRun || clip == settings.sleep || clip == settings.relaxing;
        void OnDestroy()
        {
            if (health != null) { health.Died -= Die; health.Damaged -= ReactToHit; }
            if (graph.IsValid()) graph.Destroy();
        }
        void OnDrawGizmosSelected()
        {
            if (settings == null) return;
            Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, settings.wakeRadius);
            var eye = transform.position + Vector3.up;
            Gizmos.color = Color.yellow;
            Vector3 previous = eye + Quaternion.Euler(0, -settings.visionAngle / 2, 0) * transform.forward * settings.detectionRadius;
            Gizmos.DrawLine(eye, previous);
            for (int i = 1; i <= 24; i++)
            {
                var next = eye + Quaternion.Euler(0, -settings.visionAngle / 2 + settings.visionAngle * i / 24, 0) * transform.forward * settings.detectionRadius;
                Gizmos.DrawLine(previous, next); previous = next;
            }
            Gizmos.DrawLine(eye, previous);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, settings.attackRange);
        }
    }
}
