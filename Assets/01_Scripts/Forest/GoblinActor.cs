using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ForestVR
{
    [RequireComponent(typeof(Health), typeof(CharacterController))]
    public sealed class GoblinActor : MonoBehaviour
    {
        public enum State { Resting, GettingUp, Idle, Walking, Attacking, TurningFromHit, Dead }
        public GoblinSettings settings;
        public State CurrentState { get; private set; }
        Health health, targetHealth;
        Transform target;
        CharacterController body;
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationClipPlayable playing;
        AnimationClip currentClip;
        float nextAttack, impactAt = -1, stateEnds, verticalSpeed, lastSeenAt, idleSince, pendingDamage;
        Vector3 lastKnown;
        bool remembersTarget;
        public Health Health => health;

        public void Initialize(GoblinSettings config, Transform playerHead, Health playerHealth)
        {
            settings = config; target = playerHead; targetHealth = playerHealth;
            health = GetComponent<Health>(); health.Initialize(settings.health);
            body = GetComponent<CharacterController>();
            health.Died += Die; health.Damaged += ReactToHit;
            var animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null;
                graph = PlayableGraph.Create("Goblin animations");
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                output = AnimationPlayableOutput.Create(graph, "Body", animator);
                graph.Play();
            }
            Rest();
        }
        void Play(AnimationClip clip, bool restart = false)
        {
            if (!graph.IsValid() || clip == null || (!restart && currentClip == clip)) return;
            if (playing.IsValid()) graph.DestroyPlayable(playing);
            playing = AnimationClipPlayable.Create(graph, clip);
            playing.SetApplyFootIK(false); output.SetSourcePlayable(playing); currentClip = clip;
        }
        void Rest()
        {
            CurrentState = State.Resting; remembersTarget = false;
            var clip = Random.value < 0.5f ? settings.sleep : settings.relaxing;
            Play(clip != null ? clip : settings.idle);
        }
        void Wake()
        {
            CurrentState = State.GettingUp;
            Play(settings.gettingUp != null ? settings.gettingUp : settings.idle, true);
            stateEnds = Time.time + (settings.gettingUp != null ? settings.gettingUp.length : 0.2f);
            idleSince = stateEnds;
        }
        void Update()
        {
            if (settings == null || health == null || health.IsDead) return;
            if (GroundSafety.IsLost(transform.position)) PlaceOnGround();
            verticalSpeed = body.isGrounded ? -2 : verticalSpeed + Physics.gravity.y * Time.deltaTime;
            body.Move(Vector3.up * (verticalSpeed * Time.deltaTime));
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
                Face(lastKnown, settings.turnSpeed * 2);
                if (Time.time >= stateEnds) Idle();
                return;
            }
            if (CurrentState == State.Attacking)
            {
                if (impactAt >= 0 && Time.time >= impactAt)
                {
                    impactAt = -1;
                    if (distance <= settings.attackRange && InFront(delta, 100) && HasLineOfSight())
                        targetHealth.TakeHit(pendingDamage, transform.position);
                }
                if (Time.time >= stateEnds) Idle();
                return;
            }
            bool visible = CanSeeTarget();
            if (visible)
            { lastKnown = target.position; lastSeenAt = Time.time; remembersTarget = true; }
            if (visible && distance <= settings.attackRange)
            {
                Face(target.position, settings.turnSpeed);
                if (Time.time >= nextAttack) Attack(); else Idle();
                return;
            }
            if (remembersTarget && Time.time - lastSeenAt <= settings.memorySeconds && Flat(lastKnown - transform.position).magnitude > 0.5f)
            {
                Face(lastKnown, settings.turnSpeed);
                var before = transform.position;
                body.Move(Flat(lastKnown - transform.position).normalized * (settings.speed * Time.deltaTime));
                CurrentState = State.Walking;
                Play((transform.position - before).sqrMagnitude > 0.000001f ? settings.walk : settings.idle);
                idleSince = Time.time;
            }
            else
            {
                Idle(); remembersTarget = false;
                if (Time.time - idleSince >= settings.restAfterSeconds) Rest();
            }
        }
        void Idle()
        {
            if (CurrentState != State.Idle) idleSince = Time.time;
            CurrentState = State.Idle; Play(settings.idle);
        }
        void Attack()
        {
            bool slow = settings.slowAttack != null && Random.value < 0.5f;
            var clip = slow ? settings.slowAttack : settings.attack;
            float delay = slow ? settings.slowAttackImpactDelay : settings.attackImpactDelay;
            pendingDamage = slow ? settings.slowAttackDamage : settings.damage;
            CurrentState = State.Attacking; Play(clip, true);
            impactAt = Time.time + delay;
            stateEnds = Time.time + Mathf.Max(delay + 0.05f, clip != null ? clip.length : 1);
            nextAttack = Mathf.Max(stateEnds, Time.time + settings.attackCooldown);
        }
        void ReactToHit(Vector3 source)
        {
            if (health.IsDead) return;
            bool behind = Vector3.Dot(transform.forward, Flat(source - transform.position).normalized) < -0.15f;
            lastKnown = source; lastSeenAt = Time.time; remembersTarget = true;
            if (CurrentState == State.Resting) { Wake(); return; }
            if (behind && (CurrentState == State.Walking || CurrentState == State.Idle))
            {
                impactAt = -1; CurrentState = State.TurningFromHit;
                Play(settings.attackedFromBack != null ? settings.attackedFromBack : settings.idle, true);
                stateEnds = Time.time + (settings.attackedFromBack != null ? settings.attackedFromBack.length : 1);
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
            Play(settings.death, true);
            Destroy(gameObject, Mathf.Max(settings.corpseSeconds, settings.death != null ? settings.death.length : 0));
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
            if (health != null && health.IsDead && playing.IsValid() && currentClip != null && playing.GetTime() >= currentClip.length)
            { playing.SetTime(Mathf.Max(0, currentClip.length - 0.001f)); playing.SetSpeed(0); }
        }
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
