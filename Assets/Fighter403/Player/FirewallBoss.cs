using System.Collections.Generic;
using UnityEngine;
using Fighter403.Audio;

namespace Fighter403.Player
{
    public sealed partial class FirewallBoss : CombatTarget
    {
        private enum Skill { Rain, Meteor, Deploy, Seal, Vortex }
        private static readonly Skill[] FirstPhase = { Skill.Rain, Skill.Meteor, Skill.Deploy, Skill.Seal, Skill.Vortex };
        private static readonly (Skill, Skill)[] SecondPhase =
        {
            (Skill.Rain, Skill.Deploy), (Skill.Meteor, Skill.Seal), (Skill.Vortex, Skill.Rain),
            (Skill.Deploy, Skill.Meteor), (Skill.Seal, Skill.Vortex)
        };
        private const float Faster = 1.35f, TransitionTime = 0.80f, SealRadius = 5.5f;
        private const float MeteorRadius = 1.47f, VortexRadius = 3.05f, CollapseRadius = 1.40f;
        private const float RainSpeed = 11.2f, PacketRadius = 0.22f;
        private const float PacketAnimationFps = 24f;
        private const float PacketPassClearance = 0.95f;
        private const float FirstVolley = 0.86f, VolleyInterval = 0.62f;
        internal static readonly Vector2 ArenaHalfSize = new(19.2f, 12.45f);
        internal static readonly Vector2 TrialSpawnOffset = Vector2.down * 5.1f;

        [SerializeField] private Texture2D animationSheet;
        [SerializeField] private Sprite arenaSurface;
        [SerializeField] private ProgramEnemy melee, swarm, elite;

        private sealed class Cast
        {
            internal Skill Skill;
            internal float Age, Speed, Duration;
            internal float LastRelease = float.NegativeInfinity;
            internal int Step, SafeGate, RainEdge, GapLane, LaneCount;
            internal Vector2 Center;
            internal SpriteRenderer Effect;
            internal GameSfx.Loop SoundLoop;
            internal readonly List<Mark> Marks = new();
            internal readonly List<Packet> Packets = new();
        }

        private sealed class Mark
        {
            internal Vector2 Position;
            internal float Impact;
            internal SpriteRenderer Warning;
            internal Vector4 WarningShape;
            internal SpriteRenderer Effect, Accent;
        }

        private sealed class Packet
        {
            internal Vector2 Direction;
            internal SpriteRenderer Effect;
            internal float AnimationStart;
            internal bool PassPlayed;
        }

        private readonly List<Cast> casts = new(2);
        private readonly List<ProgramEnemy> summons = new();
        private RoundPlayerMovement player;
        private CombatVitals vitals;
        private LayerMask obstacleMask;
        private CircleCollider2D body;
        private Vector2 center;
        private Skill? queuedSkill;
        private int pattern, rainSequence;
        private float delay, transition, recovery, idleAge, deathAge;
        private bool phaseTwo, groupHadHeavyAttack;
        public override string DisplayName => "CstonF0x1";
        internal override bool Anchored => true;
        protected override Vector3 DamageNumberOrigin => overhead.DamageNumberOrigin;
        internal bool PhaseTwo => phaseTwo;
        internal int ActiveSkillCount => casts.Count;

        protected override void Awake()
        {
            center = transform.position;
            body = GetComponent<CircleCollider2D>();
            InitializeVisuals();
            base.Awake();
        }

        internal void Initialize(RoundPlayerMovement target, CombatVitals targetVitals, LayerMask obstacles,
            TopDownCameraFollow cameraFollow, RectTransform overlayParent, bool encloseTrial)
        {
            player = target;
            vitals = targetVitals;
            obstacleMask = obstacles;
            shotCamera = cameraFollow;
            if (overlayParent != null) CreateScreenEffect(overlayParent);
            if (encloseTrial) CreateArena();
            PlayPixels(PixelEffect.Arrival, "Firewall startup sequence", center, .82f, 2.40f, 2);
        }

        protected override void OnHitResolved(int damage)
        {
            if (LastDamage > 0) coreHitAt = Time.unscaledTime;
            if (phaseTwo || Health == 0 || Health * 2 > MaxHealth) return;
            phaseTwo = true;
            transition = TransitionTime;
            pattern = 0;
            delay = 0.18f;
            recovery = 0f;
            CancelCasts();
            ClearPixelEffects();
            GameSfx.Play(Sfx.BossPhase);
            Punch(0.62f, 0.70f, Overload, 0.64f);
            Shock(center, ArenaHalfSize.magnitude, Overload, 0.70f);
            PlayPixels(PixelEffect.Overload, "Overload heat burst", center, TransitionTime, 2.85f, 4);
        }

        private void FixedUpdate()
        {
            if (!Alive || player == null || vitals == null) return;
            if (vitals.Health <= 0)
            {
                CancelCasts();
                return;
            }

            float dt = Time.fixedDeltaTime * BehaviorTimeScale;
            idleAge += dt;
            if (transition > 0f) { transition -= dt; return; }
            if (Stunned)
            {
                if (casts.Count > 0) ClearPixelEffects();
                CancelCasts();
                delay = Mathf.Max(delay, 0.45f);
                return;
            }

            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                cast.Age += dt * cast.Speed;
                StepCast(cast, dt);
                if (cast.Age < cast.Duration) continue;
                if (cast.Skill is Skill.Seal or Skill.Meteor or Skill.Vortex) groupHadHeavyAttack = true;
                if (cast.Skill == Skill.Vortex) ClearSpirals();
                ClearCast(cast);
                casts.RemoveAt(i);
                if (casts.Count == 0 && !queuedSkill.HasValue)
                {
                    recovery = groupHadHeavyAttack ? (phaseTwo ? 1.22f : 1.65f) : 0f;
                    groupHadHeavyAttack = false;
                    delay = recovery > 0f ? recovery : (phaseTwo ? 0.24f : 0.64f);
                }
            }

            delay -= dt;
            recovery = Mathf.Max(0f, recovery - dt);
            if (delay > 0f) return;
            if (queuedSkill.HasValue)
            {
                casts.Add(BeginCast(queuedSkill.Value));
                queuedSkill = null;
            }
            else if (casts.Count == 0)
            {
                int next = pattern++;
                if (phaseTwo)
                {
                    var (first, second) = SecondPhase[next % SecondPhase.Length];
                    casts.Add(BeginCast(first));
                    queuedSkill = second;
                    delay = 0.20f;
                }
                else casts.Add(BeginCast(FirstPhase[next % FirstPhase.Length]));
            }
        }

        private Cast BeginCast(Skill skill)
        {
            var cast = new Cast { Skill = skill, Speed = phaseTwo ? Faster : 1f, Center = center };
            switch (skill)
            {
                case Skill.Rain:
                    cast.RainEdge = rainSequence++ % 4;
                    Vector2 tangent = (cast.RainEdge & 1) == 0 ? Vector2.right : Vector2.up;
                    float extent = Vector2.Dot(tangent, ArenaHalfSize);
                    cast.LaneCount = Mathf.CeilToInt(extent * 2f / 1.24f);
                    float axis = Vector2.Dot(player.Position - center, tangent);
                    cast.GapLane = Mathf.Clamp(Mathf.FloorToInt((axis / extent + 1f) * 0.5f * cast.LaneCount) - 1, 1, cast.LaneCount - 3);
                    float crossing = 2f * Vector2.Dot(ArenaHalfSize, Vector2.one - tangent);
                    cast.Duration = FirstVolley + 2f * VolleyInterval + (crossing + 0.55f) / RainSpeed;
                    QueueRainRow(cast, 0);
                    break;
                case Skill.Meteor:
                    cast.Duration = 3.30f;
                    GameSfx.Play(Sfx.BossMeteorWarn);
                    QueueMeteors(cast);
                    break;
                case Skill.Deploy:
                    cast.Duration = 1.52f;
                    GameSfx.Play(Sfx.BossPortal);
                    QueueDeploy(cast);
                    break;
                case Skill.Seal:
                    GameSfx.Play(Sfx.BossSealWarn);
                    cast.Duration = 1.67f;
                    cast.SafeGate = (ClosestGate(player.Position - center) + (pattern % 2 == 0 ? 1 : 3)) % 4;
                    for (int gate = 0; gate < 4; gate++)
                    {
                        if (gate == cast.SafeGate) continue;
                        float angle = GateAngle(gate);
                        cast.Marks.Add(NewMark("Seal sector", center, WarningShape.Sector, SealRadius, 1.08f, angle));
                    }
                    break;
                case Skill.Vortex:
                    GameSfx.Play(Sfx.BossVortexOpen);
                    cast.Duration = 2.93f;
                    Vector2 offset = player.Position - center;
                    Vector2 direction = offset.sqrMagnitude < 0.01f ? Vector2.down : offset.normalized;
                    float distance = Mathf.Clamp(offset.magnitude, body.radius + CollapseRadius + 0.4f, ArenaHalfSize.magnitude);
                    cast.Center = ClampToArena(center + direction * distance, VortexRadius + 0.18f);
                    cast.Marks.Add(NewMark("Vortex radius", cast.Center, WarningShape.Vortex,
                        VortexRadius, 2.46f, innerRadius: CollapseRadius));
                    cast.Effect = AddSprite("Code vortex", cast.Center, SpriteAt(PixelEffect.Vortex, 0f, frames[9, 0]), -2);
                    break;
            }
            return cast;
        }

        private void StepCast(Cast cast, float dt)
        {
            switch (cast.Skill)
            {
                case Skill.Rain: StepRain(cast, dt); break;
                case Skill.Meteor: StepMeteors(cast); break;
                case Skill.Deploy: StepDeploy(cast); break;
                case Skill.Seal: StepSeal(cast); break;
                case Skill.Vortex: StepVortex(cast); break;
            }
        }

        private void QueueRainRow(Cast cast, int row)
        {
            GameSfx.Play(Sfx.BossRainWarn);
            int shift = row == 1 ? (cast.GapLane < cast.LaneCount / 2 ? 1 : -1) : 0;
            int gap = cast.GapLane + shift;
            Vector2 direction = -Polar(GateAngle(cast.RainEdge), 1f);
            float angle = GateAngle(cast.RainEdge) + 180f;
            for (int lane = 0; lane < cast.LaneCount; lane++)
            {
                if (lane == gap || lane == gap + 1) continue;
                Vector2 point = RainStart(cast, lane);
                var mark = NewMark("Packet launch", point + direction * 0.20f, WarningShape.Launch,
                    0.48f, FirstVolley + VolleyInterval * row, angle);
                mark.Position = point;
                mark.Accent = AddSprite("Packet charger", point + direction * 0.11f,
                    SpriteAt(PixelEffect.PacketCharge, 0f, frames[6, 0]), 0);
                mark.Accent.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                cast.Marks.Add(mark);
            }
        }

        private void StepRain(Cast cast, float dt)
        {
            float fireAt = FirstVolley + VolleyInterval * cast.Step;
            if (cast.Step < 3 && cast.Age >= fireAt)
            {
                Vector2 direction = -Polar(GateAngle(cast.RainEdge), 1f);
                for (int lane = 0; lane < cast.Marks.Count; lane++)
                {
                    Mark mark = cast.Marks[lane];
                    int offset = (lane * 3 + cast.Step) % PixelColumns;
                    var renderer = AddSprite("Data packet", mark.Position,
                        SpriteAt(PixelEffect.Packet, (float)offset / PixelColumns, frames[11, offset % Columns]), 5);
                    renderer.transform.rotation = mark.Accent.transform.rotation;
                    renderer.transform.localScale = Vector3.one * (pixelSprites != null ? 1.12f : 1.25f);
                    PlayPixels(PixelEffect.PacketLaunch, "Packet muzzle flare", mark.Position, .20f, 1.1f, 4,
                        mark.Accent.transform.eulerAngles.z);
                    cast.Packets.Add(new Packet { Direction = direction, Effect = renderer,
                        AnimationStart = cast.Age - offset / PacketAnimationFps });
                }
                ClearMarks(cast);
                GameSfx.Play(Sfx.BossRainFire);
                cast.LastRelease = cast.Age;
                if (cast.Step++ == 0) Punch(0.26f, 0.27f, Amber, 0.25f, direction);
                if (cast.Step < 3) QueueRainRow(cast, cast.Step);
            }

            foreach (Mark mark in cast.Marks)
            {
                PaintMark(mark, Amber, cast.Age, VolleyInterval);
                float charge = 1f - Mathf.Clamp01((mark.Impact - cast.Age) / VolleyInterval);
                mark.Accent.sprite = SpriteAt(PixelEffect.PacketCharge, charge, frames[6, Frame(charge)]);
            }
            for (int i = cast.Packets.Count - 1; i >= 0; i--)
            {
                var packet = cast.Packets[i];
                Vector2 from = packet.Effect.transform.position;
                Vector2 to = from + packet.Direction * (dt * RainSpeed * cast.Speed);
                float reach = PacketRadius + player.CombatRadius;
                Vector2 travel = to - from;
                float projection = travel.sqrMagnitude > 0f ? Vector2.Dot(player.Position - from, travel) / travel.sqrMagnitude : 0f;
                float closestDistanceSquared = (player.Position - (from + travel * Mathf.Clamp01(projection))).sqrMagnitude;
                bool hit = closestDistanceSquared <= reach * reach;
                if (hit)
                {
                    vitals.TakeDamageFrom(7, packet.Direction, Sfx.BossRainHit, this);
                    float angle = Mathf.Atan2(packet.Direction.y, packet.Direction.x) * Mathf.Rad2Deg;
                    PlayPixels(PixelEffect.PacketHit, "Packet contact spark", from + travel * Mathf.Clamp01(projection),
                        .27f, 1.20f, 6, angle);
                }
                else if (!packet.PassPlayed && projection is >= 0f and <= 1f &&
                    closestDistanceSquared <= (reach + PacketPassClearance) * (reach + PacketPassClearance))
                {
                    packet.PassPlayed = true;
                    GameSfx.Play(Sfx.BossRainPass, 0.65f, minimumInterval: 0.10f);
                }
                if (hit || !InsideArena(to, 0.35f))
                {
                    Remove(packet.Effect);
                    cast.Packets.RemoveAt(i);
                }
                else
                {
                    packet.Effect.transform.position = to;
                    float animation = (cast.Age - packet.AnimationStart) * PacketAnimationFps;
                    float progress = Mathf.Repeat(animation, PixelColumns) / PixelColumns;
                    packet.Effect.sprite = SpriteAt(PixelEffect.Packet, progress, frames[11, (int)animation % Columns]);
                }
            }
        }

        private Vector2 RainStart(Cast cast, int lane)
        {
            float lanePosition = (lane + 0.5f) / cast.LaneCount * 2f - 1f;
            return center + (cast.RainEdge switch
            {
                0 => new Vector2(ArenaHalfSize.x * lanePosition, ArenaHalfSize.y - 0.18f),
                1 => new Vector2(-ArenaHalfSize.x + 0.18f, ArenaHalfSize.y * lanePosition),
                2 => new Vector2(ArenaHalfSize.x * lanePosition, -ArenaHalfSize.y + 0.18f),
                _ => new Vector2(ArenaHalfSize.x - 0.18f, ArenaHalfSize.y * lanePosition)
            });
        }

        private void QueueMeteors(Cast cast)
        {
            Vector2 limit = ArenaHalfSize - Vector2.one * 0.94f;
            int columns = Mathf.RoundToInt(limit.x * 2f / 3.4f) + 1;
            int rows = Mathf.RoundToInt(limit.y * 2f / 3.65f) + 1;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                float x = Mathf.Lerp(-limit.x, limit.x, (float)column / (columns - 1));
                float y = Mathf.Lerp(-limit.y, limit.y, (float)row / (rows - 1));
                if (column > 0 && column < columns - 1) x += (row % 2 == 0 ? 0.30f : -0.30f);
                Vector2 point = center + new Vector2(x, y);
                if (InsideCircle(point, center, body.radius + 0.48f)) continue;
                int group = (column + row * 2 + pattern) % 4;
                float impact = 1.14f + group * 0.52f;
                var mark = NewMark("Code meteor target", point, WarningShape.Meteor,
                    MeteorRadius, impact, innerRadius: MeteorRadius * 0.57f);
                mark.Effect = AddSprite("Incoming code meteor", point,
                    SpriteAt(PixelEffect.MeteorFall, 0f, frames[5, 3]), 7);
                if (pixelSprites != null)
                    mark.Effect.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-3.95f, 1.61f) * Mathf.Rad2Deg);
                mark.Effect.enabled = false;
                mark.Accent = AddSprite("Code meteor impact", point,
                    SpriteAt(PixelEffect.MeteorBurst, 0f, frames[7, 0]), 3);
                mark.Accent.transform.localScale = Vector3.one * (pixelSprites != null ? 1.30f : 1.12f);
                mark.Accent.enabled = false;
                cast.Marks.Add(mark);
            }
        }

        private void StepMeteors(Cast cast)
        {
            bool impacted = false;
            foreach (Mark mark in cast.Marks)
            {
                float until = mark.Impact - cast.Age;
                if (until > 0f)
                {
                    PaintMark(mark, MeteorColor, cast.Age, 0.95f);
                    // The falling sprite lands on the timing shared by the ring and the damage.
                    mark.Effect.enabled = until <= 0.36f;
                    float descent = Mathf.Clamp01(until / 0.36f);
                    mark.Effect.transform.position = mark.Position + new Vector2(-1.61f, 3.95f) * Mathf.Sqrt(descent);
                    mark.Effect.transform.localScale = Vector3.one * Mathf.Lerp(1.42f, 1.83f, descent);
                    mark.Effect.sprite = SpriteAt(PixelEffect.MeteorFall, 1f - descent, frames[5, 3]);
                    continue;
                }

                mark.Effect.enabled = false;
                if (mark.Warning.enabled)
                {
                    if (InsideCircle(player.Position, mark.Position, MeteorRadius))
                        vitals.TakeDamageFrom(16, new Vector2(1.61f, -3.95f), source: this);
                    impacted = true;
                }
                float aftermath = -until / 0.54f;
                mark.Accent.enabled = aftermath < 1f;
                if (mark.Accent.enabled)
                    mark.Accent.sprite = SpriteAt(PixelEffect.MeteorBurst, aftermath, frames[7, Frame(aftermath)]);
                mark.Warning.enabled = false;
            }
            if (impacted)
            {
                cast.LastRelease = cast.Age;
                GameSfx.Play(Sfx.BossMeteorLand);
                Punch(0.37f, 0.29f, MeteorColor, 0.32f, Vector2.down);
            }
        }

        private void QueueDeploy(Cast cast)
        {
            float sign = pattern % 2 == 0 ? -1f : 1f;
            float sides = ArenaHalfSize.x * 0.74f;
            float depth = ArenaHalfSize.y * 0.54f * sign;
            AddPortal(cast, center + new Vector2(-sides, depth));
            AddPortal(cast, center + new Vector2(sides, depth));
            if (phaseTwo) AddPortal(cast, center + new Vector2(0f, -ArenaHalfSize.y * 0.74f * sign));
        }

        private void AddPortal(Cast cast, Vector2 point)
        {
            var mark = NewMark("Deployment gate", point, WarningShape.Portal, 1.27f, 0.92f);
            mark.Effect = AddSprite("Deployment aperture", point, SpriteAt(PixelEffect.DeployOpen, 0f, frames[8, 0]), 1);
            mark.Effect.transform.localScale = Vector3.one * (pixelSprites != null ? 1.55f : 1.24f);
            cast.Marks.Add(mark);
        }

        private void StepDeploy(Cast cast)
        {
            for (int i = summons.Count - 1; i >= 0; i--)
            {
                var add = summons[i];
                if (add == null) { summons.RemoveAt(i); continue; }
                if (!add.Alive && !add.GetComponent<SpriteRenderer>().enabled)
                {
                    Destroy(add.gameObject);
                    summons.RemoveAt(i);
                }
            }
            for (int i = 0; i < cast.Marks.Count; i++)
            {
                Mark mark = cast.Marks[i];
                PaintMark(mark, PortalColor, cast.Age, 0.92f);
                if (cast.Age < mark.Impact)
                {
                    float progress = cast.Age / mark.Impact;
                    mark.Effect.sprite = SpriteAt(PixelEffect.DeployOpen, progress, frames[8, Frame(progress)]);
                }
                else if (pixelSprites != null) mark.Effect.enabled = false;
                else mark.Effect.sprite = frames[8, Frame(cast.Age / cast.Duration)];
                if (cast.Step != 0 || cast.Age < mark.Impact) continue;
                ProgramEnemy prefab = i == 2 ? elite : i == 0 ? melee : swarm;
                if (prefab != null && CanSummon(prefab) && IsPortalClear(mark.Position))
                {
                    var add = Instantiate(prefab, mark.Position, Quaternion.identity);
                    add.Initialize(player, vitals, obstacleMask);
                    summons.Add(add);
                    GameSfx.Play(Sfx.BossDeploy);
                }
                if (pixelSprites == null) Shock(mark.Position, 1.35f, PortalColor, 0.38f);
                else PlayPixels(PixelEffect.DeployClose, "Deployment aperture closing", mark.Position,
                    .46f / cast.Speed, 1.6f, 2);
            }
            if (cast.Step == 0 && cast.Age >= 0.92f)
            {
                cast.Step++;
                cast.LastRelease = cast.Age;
                Punch(0.19f, 0.24f, PortalColor, 0.21f);
            }
        }

        private bool CanSummon(ProgramEnemy prefab)
        {
            int normalCount = 0, eliteCount = 0;
            foreach (var summon in summons)
            {
                if (summon == null || !summon.Alive) continue;
                if (elite != null && summon.Role == elite.Role) eliteCount++;
                else normalCount++;
            }
            return prefab == elite ? eliteCount < 1 : normalCount < (phaseTwo ? 4 : 3);
        }

        private bool IsPortalClear(Vector2 point)
        {
            if (InsideCircle(player.Position, point, 1.12f)) return false;
            foreach (var summon in summons)
                if (summon != null && summon.Alive && InsideCircle(summon.transform.position, point, 1.14f))
                    return false;
            return true;
        }

        private void StepSeal(Cast cast)
        {
            foreach (var mark in cast.Marks) PaintMark(mark, Overload, cast.Age, mark.Impact);
            if (cast.Step != 0 || cast.Age < cast.Marks[0].Impact) return;
            cast.Step++;
            cast.LastRelease = cast.Age;
            Vector2 delta = player.Position - center;
            if (delta.sqrMagnitude <= SealRadius * SealRadius && ClosestGate(delta) != cast.SafeGate)
                vitals.TakeDamageFrom(20, delta, source: this);
            for (int gate = 0; gate < 4; gate++)
            {
                if (gate == cast.SafeGate) continue;
                PlayPixels(PixelEffect.Seal, "Sector shutter discharge", center, .54f / cast.Speed,
                    SealRadius * 32f / 48f, 1, GateAngle(gate));
            }
            GameSfx.Play(Sfx.BossSealRelease);
            Punch(0.50f, 0.39f, Overload, 0.43f, -Polar(GateAngle(cast.SafeGate), 1f));
        }

        private void StepVortex(Cast cast)
        {
            var mark = cast.Marks[0];
            PaintMark(mark, VortexColor, cast.Age, 2.46f);
            cast.Effect.sprite = SpriteAt(PixelEffect.Vortex, Mathf.Repeat(cast.Age * 1.2f, 1f),
                frames[9, (int)(cast.Age * 14f) % Columns]);
            cast.Effect.transform.rotation = Quaternion.Euler(0f, 0f, cast.Age * 18f);
            float size = cast.Age < 2.46f ? Mathf.Lerp(0.7f, 1.95f, cast.Age / 2.46f)
                : Mathf.Lerp(1.95f, 0.05f, (cast.Age - 2.46f) / 0.30f);
            size = Mathf.Max(0.01f, size);
            cast.Effect.transform.localScale = Vector3.one * size;
            if (pixelSprites == null) UpdateVortexSpirals(cast);
            if (cast.Age < 0.56f || cast.Step > 0) return;
            Vector2 offset = cast.Center - player.Position;
            if (cast.Age < 2.46f)
            {
                if (cast.SoundLoop == null) cast.SoundLoop = GameSfx.StartLoop(Sfx.BossVortexPull, gameObject);
                if (offset.sqrMagnitude < VortexRadius * VortexRadius)
                    player.AddCombatDrift((offset.normalized + Vector2.Perpendicular(offset.normalized) * 0.28f)
                        * (phaseTwo ? 2.3f : 1.75f) * BehaviorTimeScale);
                return;
            }

            cast.Step++;
            cast.LastRelease = cast.Age;
            GameSfx.StopLoop(ref cast.SoundLoop);
            GameSfx.Play(Sfx.BossVortexEnd);
            if (offset.sqrMagnitude <= CollapseRadius * CollapseRadius) vitals.TakeDamageFrom(18, -offset, source: this);
            Punch(0.43f, 0.40f, VortexColor, 0.40f);
            if (pixelSprites == null) Shock(cast.Center, VortexRadius, VortexColor, 0.36f);
            else PlayPixels(PixelEffect.Collapse, "Code vortex collapse", cast.Center,
                .36f / cast.Speed, CollapseRadius * 32f / 18f, 1);
        }

        private static float GateAngle(int gate) => 90f + gate * 90f;
        private static int ClosestGate(Vector2 direction) =>
            (Mathf.RoundToInt((Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f) / 90f) + 4) % 4;

        private bool InsideArena(Vector2 point, float margin)
        {
            Vector2 local = point - center;
            return Mathf.Abs(local.x) <= ArenaHalfSize.x + margin && Mathf.Abs(local.y) <= ArenaHalfSize.y + margin;
        }

        private Vector2 ClampToArena(Vector2 point, float margin)
        {
            Vector2 p = point - center;
            return center + new Vector2(Mathf.Clamp(p.x, margin - ArenaHalfSize.x, ArenaHalfSize.x - margin),
                Mathf.Clamp(p.y, margin - ArenaHalfSize.y, ArenaHalfSize.y - margin));
        }

        private static bool InsideCircle(Vector2 point, Vector2 origin, float radius) =>
            (point - origin).sqrMagnitude <= radius * radius;

        private static void ClearMarks(Cast cast)
        {
            foreach (var mark in cast.Marks)
            {
                Remove(mark.Warning);
                Remove(mark.Effect);
                Remove(mark.Accent);
            }
            cast.Marks.Clear();
        }

        private static void ClearCast(Cast cast)
        {
            GameSfx.StopLoop(ref cast.SoundLoop);
            ClearMarks(cast);
            Remove(cast.Effect);
            foreach (var packet in cast.Packets) Remove(packet.Effect);
        }

        private void CancelCasts()
        {
            foreach (Cast cast in casts) ClearCast(cast);
            casts.Clear();
            queuedSkill = null;
            groupHadHeavyAttack = false;
            ClearSpirals();
        }

        private void ClearSummons()
        {
            foreach (var summon in summons)
            {
                if (summon == null) continue;
                summon.RemoveSpawnedFragments();
                summon.gameObject.SetActive(false);
                Destroy(summon.gameObject);
            }
            summons.Clear();
        }

        protected override void OnDefeated()
        {
            body.enabled = false;
            deathAge = 0f;
            CancelCasts();
            ClearPixelEffects();
            ClearSummons();
            overhead.Hide();
            GameSfx.Play(Sfx.BossDown);
            Punch(0.72f, 0.82f, Amber, 0.69f);
            Shock(center, ArenaHalfSize.magnitude, Amber, 0.82f);
            PlayPixels(PixelEffect.Defeat, "Firewall core break", center, .78f, 3.2f, 4);
        }

        public override void ResetTarget()
        {
            base.ResetTarget();
            phaseTwo = false;
            transition = recovery = idleAge = deathAge = 0f;
            coreHitAt = float.NegativeInfinity;
            delay = 1.1f;
            pattern = rainSequence = 0;
            CancelCasts();
            ClearSummons();
            ClearFeedback();
            body.enabled = true;
            core.enabled = true;
            overhead.Reset();
        }

        protected override void OnDisable()
        {
            CancelCasts();
            ClearSummons();
            ClearFeedback();
            base.OnDisable();
        }
    }
}
