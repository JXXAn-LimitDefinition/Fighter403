using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Fighter403.Audio;

namespace Fighter403.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RoundPlayerMovement : MonoBehaviour
    {
        private const float DashClearance = 0.04f;
        [SerializeField] private PlayerBaseStats baseStats;

        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private float moveSpeed;
        private readonly List<RaycastHit2D> dashHits = new();
        private readonly List<Collider2D> blinkOverlaps = new();
        private readonly List<Collider2D> phasedEnemies = new();
        private InputAction moveAction;
        private Vector2 moveInput;
        private Vector2 cardVelocity;
        private Vector2 combatDrift;
        private bool cardMotion;
        private bool cardFacing;
        private bool dashMotion;
        private Vector2 dashDirection;
        private float dashDistance;
        private float dashDuration;
        private float dashElapsed;
        private float dashTravelled;
        private int dashObstacleMask;
        private bool planningLock;

        private Vector2 lastSoundPosition;
        private float movementSoundDistance;
        public Vector2 FacingDirection { get; private set; } = Vector2.right;
        public float MoveAmount => cardMotion ? Mathf.Clamp01(body.linearVelocity.magnitude / Mathf.Max(moveSpeed, 1f))
            : !planningLock && moveSpeed > 0f ? moveInput.magnitude : 0f;
        internal PlayerBaseStats BaseStats => baseStats;
        internal Vector2 Position => body.position;
        internal bool DashInProgress => dashMotion;
        internal DungeonBreakableProp DashPropContact { get; private set; }
        internal float CombatRadius
        {
            get { GetBodyCircle(out _, out float radius); return radius; }
        }

        internal void AddCombatDrift(Vector2 velocity) => combatDrift += velocity;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<CircleCollider2D>();
            moveSpeed = baseStats != null ? baseStats.MoveSpeed : PlayerBaseStats.DefaultMoveSpeed;
        }

        private void OnEnable()
        {
            moveAction = InputSystem.actions?.FindAction("Player/Move");
            if (moveAction == null)
                Debug.LogError("Round Player needs the project-wide Player/Move input action.", this);
        }

        private void Update()
        {
            moveInput = Vector2.ClampMagnitude(moveAction?.ReadValue<Vector2>() ?? Vector2.zero, 1f);
            if (planningLock || cardFacing || moveInput.sqrMagnitude <= 0.0001f)
                return;

            FacingDirection = moveInput.normalized;
        }

        private void FixedUpdate()
        {
            Vector2 travelled = body.position - lastSoundPosition;
            lastSoundPosition = body.position;
            if (!planningLock && !cardMotion && moveInput.sqrMagnitude > 0.01f && travelled.sqrMagnitude < 0.25f)
            {
                movementSoundDistance += travelled.magnitude;
                if (movementSoundDistance >= 0.64f)
                {
                    movementSoundDistance = 0f;
                    GameSfx.Play(Sfx.PlayerMove, minimumInterval: 0.16f);
                }
            }
            else movementSoundDistance = 0f;

            Vector2 drift = combatDrift;
            combatDrift = Vector2.zero;
            if (dashMotion) IgnoreEnemyCollisions();
            else RestoreEnemyCollisions();

            if (!cardMotion)
            {
                body.linearVelocity = planningLock ? Vector2.zero : moveInput * moveSpeed + drift;
                return;
            }

            if (dashMotion)
            {
                StepDash();
                return;
            }

            body.linearVelocity = cardVelocity + drift;
        }

        public void BeginCardMotion(Vector2 direction, bool takeOverMovement = true)
        {
            cardFacing = true;
            cardMotion = takeOverMovement;
            dashMotion = false;
            FacingDirection = direction.normalized;
            if (cardMotion) SetCardVelocity(Vector2.zero);
        }

        public void BeginCardDash(Vector2 direction, float distance, float duration, int obstacleMask = ~0)
        {
            BeginCardMotion(direction, true);
            dashMotion = true;
            dashDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : FacingDirection;
            dashDistance = Mathf.Max(0f, distance);
            // A dash is advanced by the physics loop. Keep it long enough to
            // produce visible motion even when a card has a one-frame startup.
            dashDuration = Mathf.Max(1f / 60f, duration);
            dashElapsed = 0f;
            dashTravelled = 0f;
            DashPropContact = null;
            dashObstacleMask = obstacleMask;
            cardVelocity = Vector2.zero;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        public void SetCardVelocity(Vector2 velocity)
        {
            if (dashMotion) return;
            cardVelocity = velocity;
            if (body != null) body.linearVelocity = velocity;
        }

        internal void SetPlanningLock(bool value)
        {
            planningLock = value;
            if (value && body != null) body.linearVelocity = Vector2.zero;
        }

        internal Vector2 GetDashDestination(Vector2 origin, Vector2 direction, float distance, int obstacleMask,
            bool physicalDash = false) => GetDashDestination(origin, direction, distance, obstacleMask, out _, physicalDash);

        internal Vector2 GetDashDestination(Vector2 origin, Vector2 direction, float distance, int obstacleMask,
            out DungeonBreakableProp propContact, bool physicalDash = false)
        {
            Physics2D.SyncTransforms();
            float safe = SafeDashDistance(origin, direction, distance, obstacleMask, out var obstacle);
            propContact = obstacle != null ? obstacle.GetComponent<DungeonBreakableProp>() : null;
            if (physicalDash && obstacle != null && obstacle.TryGetComponent<DungeonGate>(out var gate) && gate.CanBreach)
                safe = Mathf.Min(distance, safe + DashClearance + 0.005f);
            return origin + direction * safe;
        }

        private float SafeDashDistance(Vector2 origin, Vector2 direction, float distance, int obstacleMask,
            out Collider2D obstacle)
        {
            obstacle = null;
            if (distance <= 0f) return 0f;
            GetBodyCircle(out Vector2 offset, out float radius);
            origin += offset;

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstacleMask);
            int count = Physics2D.CircleCast(origin, radius, direction, filter, dashHits, distance);
            float nearest = distance;
            for (int i = 0; i < count; ++i)
            {
                var hit = dashHits[i];
                if (hit.collider.attachedRigidbody == body || hit.collider.TryGetComponent<CombatTarget>(out _))
                    continue;
                if (hit.distance > nearest) continue;
                if (obstacle != null && hit.distance == nearest && hit.collider.TryGetComponent<DungeonGate>(out _)) continue;
                nearest = hit.distance;
                obstacle = hit.collider;
            }
            return obstacle != null ? Mathf.Max(0f, nearest - DashClearance) : distance;
        }

        private void IgnoreEnemyCollisions()
        {
            if (bodyCollider == null) return;
            foreach (var target in CombatTarget.Active)
            {
                if (!target.Alive) continue;
                var collider = target.BodyCollider;
                if (collider == null || collider.isTrigger || !collider.isActiveAndEnabled || Physics2D.GetIgnoreCollision(bodyCollider, collider))
                    continue;

                Physics2D.IgnoreCollision(bodyCollider, collider);
                if (!phasedEnemies.Contains(collider)) phasedEnemies.Add(collider);
            }
        }

        private void RestoreEnemyCollisions(bool force = false)
        {
            for (int index = phasedEnemies.Count - 1; index >= 0; index--)
            {
                var collider = phasedEnemies[index];
                if (bodyCollider != null && bodyCollider.isActiveAndEnabled && collider != null && collider.isActiveAndEnabled)
                {
                    // Let the player leave an overlapping enemy when a dash ends inside its body.
                    if (!force && bodyCollider.Distance(collider).isOverlapped) continue;
                    Physics2D.IgnoreCollision(bodyCollider, collider, false);
                }
                phasedEnemies.RemoveAt(index);
            }
        }

        internal bool TryGetBlinkDestination(Vector2 center, float targetRadius, Vector2 outward, int obstacleMask,
            out Vector2 destination)
        {
            GetBodyCircle(out Vector2 offset, out float radius);
            Physics2D.SyncTransforms();
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstacleMask);

            // Start on the near side of the target, then try either side before the far side.
            for (int i = 0; i < 8; i++)
            {
                float angle = 45f * ((i + 1) / 2) * (i % 2 == 0 ? -1 : 1);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * outward;
                Vector2 landing = center + direction * (targetRadius + radius + DashClearance);
                Physics2D.OverlapCircle(landing, radius, filter, blinkOverlaps);
                if (blinkOverlaps.Exists(collider => collider.attachedRigidbody != body)) continue;
                destination = landing - offset;
                return true;
            }
            destination = default;
            return false;
        }

        internal void SetCardPosition(Vector2 position)
        {
            combatDrift = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            body.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void GetBodyCircle(out Vector2 offset, out float radius)
        {
            offset = Vector2.zero;
            radius = 0.5f;
            if (bodyCollider == null) return;
            Vector3 scale = bodyCollider.transform.lossyScale;
            radius = bodyCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            offset = bodyCollider.transform.TransformVector(bodyCollider.offset);
        }

        private void StepDash()
        {
            if (dashDistance <= 0f || dashDuration <= 0f)
            {
                body.linearVelocity = Vector2.zero;
                dashMotion = false;
                return;
            }

            float stepTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            float nextElapsed = Mathf.Min(dashElapsed + stepTime, dashDuration);
            float progress = Mathf.Clamp01(nextElapsed / dashDuration);
            // Ease-in displacement: velocity rises from zero, but the body is
            // moved by small fixed-step increments instead of one large jump.
            float nextTravelled = dashDistance * progress * progress;
            float stepDistance = nextTravelled - dashTravelled;
            float safeDistance = SafeDashDistance(body.position, dashDirection, stepDistance, dashObstacleMask, out var obstacle);
            if (obstacle != null)
            {
                DashPropContact = obstacle.GetComponent<DungeonBreakableProp>();
                if (obstacle.TryGetComponent<DungeonGate>(out var gate)) gate.TryBreach();
            }
            body.linearVelocity = dashDirection * (safeDistance / stepTime);
            dashElapsed = nextElapsed;
            dashTravelled = nextTravelled;

            if (safeDistance < stepDistance || dashElapsed >= dashDuration - 0.0001f)
            {
                dashMotion = false;
                cardVelocity = Vector2.zero;
            }
        }

        public void EndCardMotion()
        {
            if (cardMotion && body != null) body.linearVelocity = planningLock ? Vector2.zero : moveInput * moveSpeed;
            cardMotion = false;
            cardFacing = false;
            dashMotion = false;
            dashDirection = Vector2.zero;
            dashDistance = dashDuration = dashElapsed = dashTravelled = 0f;
            DashPropContact = null;
            cardVelocity = Vector2.zero;
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
            combatDrift = Vector2.zero;
            planningLock = false;
            EndCardMotion();
            RestoreEnemyCollisions(force: true);
            if (body != null) body.linearVelocity = Vector2.zero;
        }
    }
}
