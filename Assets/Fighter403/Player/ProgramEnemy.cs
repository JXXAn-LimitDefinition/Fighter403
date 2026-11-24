using System.Collections.Generic;
using UnityEngine;
using Fighter403.Audio;

namespace Fighter403.Player
{
    internal enum EnemyRole { Melee, Shooter, Swarm, Shield, Resonator, Mirror, Cluster, Splitter, Protocol }

    public sealed partial class ProgramEnemy : CombatTarget
    {
        private const int FrameSize = 112, Columns = 8, PixelsPerUnit = 32;
        private const int ProjectileDamage = 8;
        private const float ShotRange = 8f, ShotSpeed = 7f, ShotRadius = 0.09f;
        private const float AttackVisualTime = 0.38f, BreakVisualTime = 0.32f;
        private const float DeathVisualTime = 0.48f, CombatCueTime = 0.55f, ShieldCueTime = 0.55f;
        private const float TurnDegreesPerSecond = 1080f, ShotStart = 0.72f;
        private const float ChaseClearance = 0.01f;
        private static readonly float[] DetourAngles = { 30f, 55f, 80f, 95f, 115f, 135f, 155f, 180f };

        [SerializeField] private EnemyRole role;
        [SerializeField] private Texture2D animationSheet;
        [SerializeField, Min(1)] private int shieldCapacity = 30;

        private RoundPlayerMovement player;
        private CombatVitals vitals;
        private SpriteRenderer display, breakDisplay;
        private CircleCollider2D hurtbox;
        private EnemyOverhead overhead;
        private LineRenderer shotWarning;
        private Material warningMaterial;
        private ContactFilter2D solidFilter;
        private readonly List<RaycastHit2D> sweepHits = new();
        private readonly List<Collider2D> chaseNeighbors = new();
        private readonly List<Shot> shots = new();
        private Sprite[,] frames;
        private MeleeCycle cycle;
        private Vector3 spawn;
        private Vector2 facing = Vector2.right, spawnFacing;
        private int shield;
        private float phaseAge, idleAge, breakAge = BreakVisualTime;
        private float deathAge, combatCueUntil, shieldCueUntil;
        private float rotorAngle, rotorSpeed;
        private int detourSide;
        private bool engaged;
        private Quaternion breakRotation;

        private sealed class Shot
        {
            public Transform Visual;
            public Vector2 Direction;
            public float RemainingDistance;
        }

        internal EnemyRole Role => role;
        internal int ShieldCapacity => HasShield ? shieldCapacity : 0;
        internal int CurrentShield => shield;
        internal override void SetWorldHealthVisible(bool visible) => overhead.SetMetersVisible(visible);
        protected override Vector3 DamageNumberOrigin => overhead.DamageNumberOrigin;

        private string dungeonBossName;
        internal bool IsDungeonBoss => dungeonBossName != null;
        public override string DisplayName => dungeonBossName ?? (role switch
        {
            EnemyRole.Shooter => "射手",
            EnemyRole.Swarm => "蜂群",
            EnemyRole.Shield => "护盾兵",
            EnemyRole.Resonator => "共鸣者",
            EnemyRole.Mirror => "镜像",
            EnemyRole.Cluster => "集群",
            EnemyRole.Splitter => splitGeneration == 0 ? "分裂者" : "小分裂者",
            EnemyRole.Protocol => "协议",
            _ => "近战敌"
        });

        private float Range => role switch
        {
            EnemyRole.Shooter => ShotRange,
            EnemyRole.Swarm => 1.5f,
            EnemyRole.Shield or EnemyRole.Protocol => 43f / PixelsPerUnit,
            EnemyRole.Resonator or EnemyRole.Mirror => 1.4f,
            _ => MeleeCycle.Range
        };

        private float Windup => role switch
        {
            EnemyRole.Shooter => 0.55f,
            EnemyRole.Swarm => 0.38f,
            EnemyRole.Shield or EnemyRole.Protocol => 0.78f,
            _ => MeleeCycle.WindupSeconds
        };

        private float Cooldown => role switch
        {
            EnemyRole.Shooter => 2f - Windup,
            EnemyRole.Swarm => 1f - Windup,
            EnemyRole.Shield or EnemyRole.Protocol => 1.15f,
            _ => MeleeCycle.CooldownSeconds
        };

        protected override void Awake()
        {
            cycle = new MeleeCycle(Windup, Cooldown);
            display = GetComponent<SpriteRenderer>();
            hurtbox = GetComponent<CircleCollider2D>();
            spawn = transform.position;
            spawnFacing = transform.right;
            LoadFrames();
            overhead = new EnemyOverhead(display, HasShield);
            if (HasShield)
            {
                breakDisplay = new GameObject("Shield fragments").AddComponent<SpriteRenderer>();
                breakDisplay.transform.SetParent(transform, false);
                CopySorting(breakDisplay);
                breakDisplay.enabled = false;
            }
            base.Awake();
        }

        internal void Initialize(RoundPlayerMovement target, CombatVitals targetVitals, LayerMask obstacles)
        {
            player = target;
            vitals = targetVitals;
            solidFilter = new ContactFilter2D { useTriggers = false };
            solidFilter.SetLayerMask(obstacles | (1 << player.gameObject.layer));
        }

        internal void ConfigureDungeonBoss(string bossName, int health, int shieldAmount)
        {
            dungeonBossName = bossName;
            shieldCapacity = Mathf.Max(1, shieldAmount);
            SetHealthCapacity(health);
        }

        private void FixedUpdate()
        {
            if (player == null || vitals == null) return;

            float dt = Time.fixedDeltaTime * BehaviorTimeScale;
            StepShots(dt);
            breakAge += dt;
            if (!Alive) return;
            if (vitals.Health <= 0)
            {
                if (echoes.Count > 0) ClearEchoes();
                return;
            }
            StepElite(dt);
            if (!Alive || vitals.Health <= 0) return;
            if (Stunned || BeingPulled)
            {
                cycle.Reset();
                phaseAge = 0f;
                return;
            }

            if (echoes.Count > 0)
            {
                idleAge += dt;
                return;
            }

            Vector2 delta = player.Position - (Vector2)transform.position;
            bool near = CombatRules.InRange(delta.sqrMagnitude, Range);
            if (near || role != EnemyRole.Shooter) EnterCombat();
            MeleePhase previous = cycle.Phase;
            bool aimed = true;
            if (role != EnemyRole.Swarm && previous == MeleePhase.Chase && delta.sqrMagnitude > 0.0001f)
                aimed = TurnToward(delta.normalized, dt);
            bool canHit = cycle.Step(dt, near && aimed);
            phaseAge = cycle.Phase == previous ? phaseAge + dt : 0f;
            if (previous != MeleePhase.Windup && cycle.Phase == MeleePhase.Windup)
                GameSfx.Play(role == EnemyRole.Shooter ? Sfx.ShooterAim : Sfx.EnemyWindup, minimumInterval: 0.14f);
            if (previous == MeleePhase.Windup && cycle.Phase == MeleePhase.Cooldown)
                ReleaseAttack(delta, canHit);
            if (cycle.Phase == MeleePhase.Chase && !near && role != EnemyRole.Shooter)
                Chase(delta, dt);
            idleAge += dt;
        }

        private bool TurnToward(Vector2 direction, float dt)
        {
            float angle = Vector2.SignedAngle(facing, direction);
            float step = TurnDegreesPerSecond * dt;
            if (Mathf.Abs(angle) <= step)
            {
                facing = direction;
                return true;
            }

            float turn = Mathf.Sign(angle) * step;
            facing = (Vector2)(Quaternion.Euler(0f, 0f, turn) * (Vector3)facing);
            return false;
        }

        private void Chase(Vector2 delta, float dt)
        {
            float speed = role switch
            {
                EnemyRole.Swarm => 3.6f,
                EnemyRole.Shield or EnemyRole.Protocol => 1.55f,
                EnemyRole.Cluster when partner != null && !partner.Alive => 3.45f,
                _ => 2.3f
            };
            float step = speed * SpeedMultiplier * dt;
            float distance = Mathf.Min(step, delta.magnitude - Range + 0.005f);
            if (distance <= 0f) return;
            Vector2 direction = delta.normalized;
            GetHurtCircle(out var center, out float radius);
            Physics2D.SyncTransforms();
            Physics2D.OverlapCircle(center, radius + step + ChaseClearance, solidFilter, chaseNeighbors);
            float forward = SafeChaseDistance(center, radius, direction, distance);
            Vector2 move = direction * forward;
            if (forward < distance * 0.8f)
            {
                int preferredSide = detourSide == 0 ? 1 : detourSide;
                Vector2 sideDirection = new Vector2(-direction.y, direction.x) * preferredSide;
                float lookAhead = radius * 2f;
                float room = SafeChaseWorldDistance(center, radius, sideDirection, lookAhead);
                if (room < (detourSide == 0 ? lookAhead : step * 2f) &&
                    SafeChaseWorldDistance(center, radius, -sideDirection, lookAhead) > room + ChaseClearance)
                    preferredSide = -preferredSide;
                if (TryDetour(center, radius, direction, step, preferredSide, out var sidestep) ||
                    TryDetour(center, radius, direction, step, -preferredSide, out sidestep))
                    move = sidestep;
            }
            transform.position += (Vector3)move;
        }

        private bool TryDetour(Vector2 center, float radius, Vector2 direction, float step, int side, out Vector2 move)
        {
            foreach (float angle in DetourAngles)
            {
                Vector2 heading = Quaternion.Euler(0f, 0f, angle * side) * (Vector3)direction;
                float distance = SafeChaseDistance(center, radius, heading, step);
                if (distance < step * 0.8f) continue;
                detourSide = side;
                move = heading * distance;
                return true;
            }
            move = Vector2.zero;
            return false;
        }

        private float SafeChaseDistance(Vector2 center, float radius, Vector2 direction, float distance)
        {
            float allowed = SafeChaseWorldDistance(center, radius, direction, distance);
            // Resolve circular enemy bodies exactly. At contact or overlap, allow only steps that separate the pair.
            foreach (var neighbor in chaseNeighbors)
            {
                if (neighbor == hurtbox || !neighbor.TryGetComponent<ProgramEnemy>(out var other)) continue;
                other.GetHurtCircle(out var otherCenter, out float otherRadius);
                Vector2 offset = center - otherCenter;
                float approach = Vector2.Dot(offset, direction);
                if (approach >= 0f) continue;
                float combinedRadius = radius + otherRadius;
                float separation = offset.sqrMagnitude - combinedRadius * combinedRadius;
                if (separation <= 0f) return 0f;
                float discriminant = approach * approach - separation;
                if (discriminant <= 0f) continue;
                float contact = -approach - Mathf.Sqrt(discriminant);
                allowed = Mathf.Min(allowed, Mathf.Max(0f, contact - ChaseClearance));
            }
            return allowed;
        }

        private float SafeChaseWorldDistance(Vector2 center, float radius, Vector2 direction, float distance)
        {
            Physics2D.CircleCast(center, radius, direction, solidFilter, sweepHits, distance);
            float allowed = distance;
            foreach (var hit in sweepHits)
            {
                if (hit.collider == null || hit.collider.TryGetComponent<ProgramEnemy>(out _)) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - ChaseClearance));
            }
            return allowed;
        }

        private void ReleaseAttack(Vector2 delta, bool inRange)
        {
            if (role == EnemyRole.Shooter)
            {
                GameSfx.Play(Sfx.ShooterFire);
                SpawnShot();
                return;
            }
            GameSfx.Play(role == EnemyRole.Swarm ? Sfx.SwarmAttack : Sfx.EnemySlash);
            if (!inRange) return;

            if (role == EnemyRole.Swarm)
                DealDamage(5, delta);
            else
            {
                float halfAngle = HasShield ? 1.18f : 1.22f;
                if (Vector2.Dot(facing, delta.normalized) >= Mathf.Cos(halfAngle) || delta.sqrMagnitude < 0.0001f)
                    DealDamage(MeleeCycle.Damage, delta);
            }
        }

        private bool Sweep(Vector2 origin, float radius, Vector2 direction, float distance,
            out RaycastHit2D first, bool ignoreEnemies)
        {
            Physics2D.CircleCast(origin, radius, direction, solidFilter, sweepHits, distance);
            first = default;
            foreach (var hit in sweepHits)
            {
                if (hit.collider == null || hit.collider == hurtbox) continue;
                if (ignoreEnemies && hit.collider.TryGetComponent<ProgramEnemy>(out _)) continue;
                if (first.collider == null || hit.distance < first.distance)
                    first = hit;
            }
            return first.collider != null;
        }

        private void SpawnShot()
        {
            if (Sweep(transform.position, ShotRadius, facing, ShotStart, out var hit, ignoreEnemies: true))
            {
                ResolveShotImpact(hit.collider, facing);
                return;
            }
            GameSfx.Play(Sfx.ProjectilePass);
            var bullet = new GameObject("Shooter projectile");
            bullet.transform.SetPositionAndRotation(transform.position + (Vector3)facing * ShotStart, Rotation(facing));
            var renderer = bullet.AddComponent<SpriteRenderer>();
            CopySorting(renderer);
            if (frames != null && frames.GetLength(0) > 3) renderer.sprite = frames[3, 0];
            shots.Add(new Shot { Visual = bullet.transform, Direction = facing, RemainingDistance = ShotRange - ShotStart });
        }

        private void StepShots(float dt)
        {
            for (int index = shots.Count - 1; index >= 0; index--)
            {
                var shot = shots[index];
                float step = Mathf.Min(dt * ShotSpeed, shot.RemainingDistance);
                bool collided = Sweep(shot.Visual.position, ShotRadius, shot.Direction, step, out var hit, ignoreEnemies: true);
                if (collided) ResolveShotImpact(hit.collider, shot.Direction);
                shot.RemainingDistance -= step;
                if (collided || shot.RemainingDistance <= 0f)
                {
                    Destroy(shot.Visual.gameObject);
                    shots.RemoveAt(index);
                }
                else shot.Visual.position += (Vector3)(shot.Direction * step);
            }
        }

        private void ResolveShotImpact(Collider2D collider, Vector2 direction)
        {
            if (collider.transform.IsChildOf(player.transform))
            {
                DealDamage(ProjectileDamage, direction, Sfx.ProjectileHit);
            }
            else GameSfx.Play(Sfx.ProjectileWall);
        }

        protected override int MitigateDamage(int amount)
        {
            if (amount <= 0) return 0;
            EnterCombat();
            if (ClusterLinked)
            {
                GameSfx.Play(Sfx.ClusterHit, 0.8f);
                return CombatRules.ScaledPoints(amount, 0.5f);
            }
            if (!HasShield || shield <= 0) return amount;
            shield -= Mathf.Min(shield, amount);
            GameSfx.Play(shield == 0 ? Sfx.EnemyShieldBreak : Sfx.EnemyShield);
            if (shield == 0)
            {
                breakAge = 0f;
                breakRotation = Rotation(facing);
                shieldCueUntil = Time.unscaledTime + ShieldCueTime;
            }
            return 0;
        }

        private void EnterCombat()
        {
            if (engaged) return;
            engaged = true;
            GameSfx.Play(role == EnemyRole.Swarm ? Sfx.SwarmRush : Sfx.EnemyEngage, minimumInterval: 0.14f);
            combatCueUntil = Time.unscaledTime + CombatCueTime;
        }

        protected override void OnDefeated()
        {
            deathAge = 0f;
            hurtbox.enabled = false;
            overhead.Hide();
            ClearEchoes();
            if (eliteLink != null) eliteLink.enabled = false;
            if (elitePulse != null) elitePulse.enabled = false;
            if (shotWarning != null) shotWarning.enabled = false;
        }

        protected override void LateUpdate()
        {
            if (!Alive)
            {
                UpdateDeath();
                return;
            }

            UpdateVisualRotation();
            base.LateUpdate();
            if (frames != null)
            {
                int row = cycle.Phase == MeleePhase.Windup ? 1
                    : cycle.Phase == MeleePhase.Cooldown && phaseAge < AttackVisualTime ? 2 : 0;
                int column = row == 0 ? (int)(idleAge * 8f) % Columns
                    : FrameAt(phaseAge, row == 1 ? Windup : AttackVisualTime);
                if (echoes.Count > 0)
                {
                    float echoAge = echoes[0].Age;
                    row = echoAge < EchoDelay ? 4 : 5;
                    column = row == 4 ? FrameAt(echoAge, EchoDelay) : FrameAt(echoAge - EchoDelay, EchoFlash);
                }
                if (HasShield && shield == 0) row += 3;
                display.sprite = frames[row, column];
                if (breakDisplay != null && frames.GetLength(0) > 6)
                {
                    breakDisplay.enabled = breakAge < BreakVisualTime;
                    if (breakDisplay.enabled)
                    {
                        breakDisplay.transform.rotation = breakRotation;
                        breakDisplay.sprite = frames[6, FrameAt(breakAge, BreakVisualTime)];
                    }
                }
            }
            if (role == EnemyRole.Shooter) UpdateShotWarning();
            UpdateEliteVisuals();
            UpdateOverhead();
        }

        private void UpdateVisualRotation()
        {
            if (role != EnemyRole.Swarm)
            {
                transform.rotation = Rotation(facing);
                return;
            }

            float dt = Time.deltaTime * BehaviorTimeScale;
            float targetSpeed = Stunned || BeingPulled ? 40f : cycle.Phase switch
            {
                MeleePhase.Windup => Mathf.Lerp(190f, 760f, Mathf.Clamp01(phaseAge / Windup)),
                MeleePhase.Cooldown => 310f,
                _ => 190f
            };
            rotorSpeed = Mathf.MoveTowards(rotorSpeed, targetSpeed, dt * 2100f);
            rotorAngle = Mathf.Repeat(rotorAngle - rotorSpeed * dt, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, rotorAngle);
        }

        private void UpdateDeath()
        {
            if (breakDisplay != null) breakDisplay.enabled = false;
            display.enabled = frames != null && deathAge < DeathVisualTime;
            if (!display.enabled) return;
            display.color = StateColor;
            int row = role switch
            {
                EnemyRole.Shooter => 4,
                EnemyRole.Shield or EnemyRole.Protocol => shield > 0 ? 7 : 8,
                _ => 3
            };
            display.sprite = frames[row, FrameAt(deathAge, DeathVisualTime)];
            if (Time.timeScale > 0f)
                deathAge += BattleVitals?.KillSlowMotionActive == true ? Time.deltaTime : Time.unscaledDeltaTime;
        }

        private void UpdateOverhead()
        {
            float now = Time.unscaledTime;
            float loop = Mathf.Repeat(now * 1.7f, 1f);
            EnemyCue cue;
            float progress;
            if (Stunned) (cue, progress) = (EnemyCue.Stun, loop);
            else if (shieldCueUntil > now)
                (cue, progress) = (EnemyCue.ShieldBreak, 1f - (shieldCueUntil - now) / ShieldCueTime);
            else if (!BeingPulled && cycle.Phase == MeleePhase.Cooldown && phaseAge < AttackVisualTime)
                (cue, progress) = (EnemyCue.Release, phaseAge / AttackVisualTime);
            else if (combatCueUntil > now)
                (cue, progress) = (EnemyCue.Combat, 1f - (combatCueUntil - now) / CombatCueTime);
            else if (!BeingPulled && cycle.Phase == MeleePhase.Windup)
                (cue, progress) = (EnemyCue.Windup, phaseAge / Windup);
            else if (Fractured) (cue, progress) = (EnemyCue.Fracture, loop);
            else if (SpeedMultiplier < 1f) (cue, progress) = (EnemyCue.Slow, loop);
            else (cue, progress) = (EnemyCue.None, 0f);

            overhead.Show((float)Health / MaxHealth, (float)shield / shieldCapacity, cue, progress);
        }

        private void UpdateShotWarning()
        {
            bool aiming = Alive && !Stunned && !BeingPulled && player != null
                && vitals.Health > 0 && cycle.Phase == MeleePhase.Windup;
            if (!aiming)
            {
                if (shotWarning != null) shotWarning.enabled = false;
                return;
            }

            if (shotWarning == null)
            {
                shotWarning = new GameObject("Shot warning").AddComponent<LineRenderer>();
                shotWarning.transform.SetParent(transform, false);
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    warningMaterial = new Material(shader);
                    shotWarning.sharedMaterial = warningMaterial;
                }
                else shotWarning.sharedMaterial = display.sharedMaterial;
                shotWarning.sortingLayerID = display.sortingLayerID;
                shotWarning.sortingOrder = display.sortingOrder - 1;
                shotWarning.positionCount = 2;
                shotWarning.widthMultiplier = 0.027f;
            }
            Vector2 start = (Vector2)transform.position + facing * ShotStart;
            float distance = ShotRange - ShotStart;
            if (Sweep(start, ShotRadius, facing, distance, out var hit, ignoreEnemies: true)
                && !hit.collider.transform.IsChildOf(player.transform))
                distance = hit.distance;
            Vector3 origin = transform.position;
            shotWarning.SetPosition(0, new Vector3(start.x, start.y, origin.z));
            shotWarning.SetPosition(1, shotWarning.GetPosition(0) + (Vector3)facing * distance);
            float strength = Mathf.Clamp01(phaseAge / Windup);
            shotWarning.widthMultiplier = Mathf.Lerp(0.035f, 0.069f, strength);
            shotWarning.startColor = new Color(0.57f, 1f, 0.85f, 0.55f + strength * 0.38f);
            shotWarning.endColor = new Color(0.57f, 1f, 0.85f, 0.25f + strength * 0.41f);
            shotWarning.enabled = true;
        }

        private void LoadFrames()
        {
            if (animationSheet == null) return;
            int rows = (HasShield ? 9 : role == EnemyRole.Shooter ? 5 : role == EnemyRole.Mirror ? 6 : 4)
                + (IsElite ? 1 : 0);
            if (animationSheet.width != FrameSize * Columns || animationSheet.height != FrameSize * rows)
            {
                Debug.LogError("ProgramEnemy animation sheet has unexpected frame dimensions.", this);
                return;
            }
            frames = new Sprite[rows, Columns];
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < Columns; column++)
                {
                    var rect = new Rect(column * FrameSize, (rows - row - 1) * FrameSize, FrameSize, FrameSize);
                    frames[row, column] = Sprite.Create(animationSheet, rect, Vector2.one * 0.5f,
                        PixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.zero, false);
                }
        }

        private void CopySorting(SpriteRenderer renderer)
        {
            renderer.sharedMaterial = display.sharedMaterial;
            renderer.sortingLayerID = display.sortingLayerID;
            renderer.sortingOrder = display.sortingOrder + 1;
        }

        public override void ResetTarget()
        {
            base.ResetTarget();
            cycle.Reset();
            phaseAge = idleAge = 0f;
            shield = shieldCapacity;
            breakAge = BreakVisualTime;
            engaged = false;
            deathAge = combatCueUntil = shieldCueUntil = 0f;
            rotorAngle = Rotation(spawnFacing).eulerAngles.z;
            rotorSpeed = 190f;
            detourSide = 0;
            hurtbox.enabled = true;
            display.enabled = true;
            display.color = StateColor;
            if (frames != null) display.sprite = frames[0, 0];
            if (breakDisplay != null) breakDisplay.enabled = false;
            if (shotWarning != null) shotWarning.enabled = false;
            transform.SetPositionAndRotation(spawn, Rotation(spawnFacing));
            facing = spawnFacing;
            overhead.Reset();
            ClearShots();
            ResetElite();
        }

        private void ClearShots()
        {
            foreach (var shot in shots)
                if (shot.Visual != null) Destroy(shot.Visual.gameObject);
            shots.Clear();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearShots();
            ClearEchoes();
            if (eliteLink != null) eliteLink.enabled = false;
        }

        private void OnDestroy()
        {
            overhead?.Dispose();
            ClearSplitChildren();
            if (warningMaterial != null) Destroy(warningMaterial);
            if (frames == null) return;
            foreach (var sprite in frames) Destroy(sprite);
        }

        private static int FrameAt(float elapsed, float duration) =>
            Mathf.Clamp(Mathf.FloorToInt(elapsed / duration * Columns), 0, Columns - 1);

        private static Quaternion Rotation(Vector2 direction) =>
            Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }
}
