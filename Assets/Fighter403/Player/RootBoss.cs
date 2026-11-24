using System;
using System.Collections.Generic;
using Fighter403.Audio;
using Fighter403.UI;
using UnityEngine;

namespace Fighter403.Player
{
    internal sealed partial class RootBoss : DungeonBoss
    {
        internal const string SpritePath = "Fighter403/Bosses/Root";

        private enum Skill { LockHand, Sector, Audit, Fork, Kill }

        private static readonly Skill[][] Patterns =
        {
            new[] { Skill.LockHand, Skill.Sector, Skill.Audit },
            new[] { Skill.Fork, Skill.Sector, Skill.Kill, Skill.Sector },
            new[] { Skill.LockHand, Skill.Audit, Skill.Sector }
        };

        private const int ReviewFloor = 1200, ProcessFloor = 600;
        private const int ChildLimit = 8;
        private const float MoveSpeed = 2f, TransitionSeconds = 1.5f;
        private const float BlinkInterval = 8f, BlinkWarningSeconds = .6f;
        private const float SectorRadius = 4f, SectorHalfAngle = 45f;
        private const float AuditLockTime = .7f, AuditHalfWidth = .5f;
        private const float ChildRadius = .36f, ChildCloneSeconds = 6f, ChildBlastRadius = 1.5f;
        private const float GuardRadius = .72f, GuardBreakSeconds = 3f;
        private const int DeletionBandCount = 3;
        private const float DeletionInterval = 25f, DeletionBandSeconds = 2f, DeletionAfterglow = 3f;
        private const float CastRecovery = 1.3f;

        private static readonly Color PhaseViolet = new Color32(199, 152, 255, 255);
        private static readonly Color AuditBlue = new Color32(149, 224, 255, 255);
        private static readonly Color DeleteRed = new Color32(255, 77, 115, 255);

        private sealed class ChildProcess
        {
            internal readonly DungeonBossSummon Target;
            internal readonly MeleeCycle Attack = new();
            internal GameObject AttackWarning;
            internal PixelEffect KillMark;
            internal float Age;
            internal bool Replicated;

            internal ChildProcess(DungeonBossSummon target) => Target = target;
        }

        private sealed class Cast
        {
            internal readonly Skill Skill;
            internal readonly float Duration;
            internal readonly Vector2 Origin;
            internal Vector2 Direction;
            internal float Age;
            internal readonly List<GameObject> Marks = new();

            internal Cast(Skill skill, float duration, Vector2 origin, Vector2 direction)
            {
                Skill = skill;
                Duration = duration;
                Origin = origin;
                Direction = direction;
            }
        }

        private readonly List<ChildProcess> children = new(ChildLimit);
        private readonly List<DungeonBossSummon> guards = new(4);
        private readonly List<GameObject> deletionPreview = new(4);
        private readonly List<GameObject> deletionHazard = new(DeletionBandCount * 4);
        private readonly List<(GameObject Mark, float Remaining)> afterglows = new();

        private int phase, patternIndex, deletedBands;
        private float transition, recovery, vulnerable, blinkClock, deletionClock, deletionAge, exposure, visualAge;
        private Vector2 facing = Vector2.down, blinkTarget, deletionBlinkTarget;
        private GameObject blinkWarning, deletionBlinkWarning;
        private float deletionBlinkClock;
        private bool spawningGuards, hadGuards, deletionActive, deletionBlinkDone;
        private Cast cast;

        internal Action<int, float, System.Random> LockHand { private get; set; }
        internal Action ClearHandLocks { private get; set; }
        public override string DisplayName => "根权限管理员 Root";
        internal override bool Anchored => transition > 0f || spawningGuards || cast != null ||
            blinkWarning != null || deletionBlinkWarning != null;
        protected override string ArtResource => SpritePath;
        protected override int StartingHealth => 1800;
        protected override float HurtRadius => 1.35f;
        protected override bool CanExecute => phase == 2 && transition <= 0f && !spawningGuards && LiveGuards == 0;
        private Vector2 Position { get => transform.position; set => transform.position = value; }
        private int LiveGuards
        {
            get
            {
                int count = 0;
                foreach (var guard in guards)
                    if (guard != null && guard.Alive) count++;
                return count;
            }
        }

        protected override Color StateColor
        {
            get
            {
                if (transition > 0f) return Color.Lerp(PhaseViolet, Color.white, Mathf.PingPong(visualAge * 8f, 1f));
                if (cast?.Skill == Skill.Fork) return Color.Lerp(Color.white, PhaseViolet, Mathf.PingPong(cast.Age * 8f, 1f));
                if (cast?.Skill == Skill.Kill) return Color.Lerp(Color.white, DeleteRed, .5f * Mathf.PingPong(cast.Age * 5f, 1f));
                return Color.white;
            }
        }

        protected override void BeginEncounter()
        {
            phase = patternIndex = 0;
            recovery = 1.1f;
            blinkClock = BlinkInterval;
            BodyPixel(PixelCue.Arrival, 1.5f, 1.07f, Body.sortingOrder + 1);
            Announce("Root · 权限审查");
        }

        protected override int MitigateDamage(int amount)
        {
            if (transition > 0f || spawningGuards) return 0;
            if (phase == 0) return Mathf.Min(amount, Health - ReviewFloor);
            if (phase == 1) return Mathf.Min(amount, Health - ProcessFloor);
            int shielded = Mathf.RoundToInt(amount * (1f - .2f * LiveGuards));
            return Mathf.Max(1, shielded);
        }

        protected override void OnHitResolved(int damage)
        {
            if (phase == 0 && Health <= ReviewFloor)
                EnterPhase(1);
            else if (phase == 1 && Health <= ProcessFloor)
                EnterPhase(2);
        }

        private void EnterPhase(int nextPhase)
        {
            phase = nextPhase;
            transition = TransitionSeconds;
            recovery = .9f;
            patternIndex = 0;
            blinkClock = BlinkInterval;
            CancelCast();
            ClearRootEffects(releaseMaterial: false);
            ClearTelegraphs();
            afterglows.Clear();
            blinkWarning = null;
            ClearHandLocks?.Invoke();
            foreach (var child in children)
                if (child.Target != null) child.Target.Dismiss();
            children.Clear();

            spawningGuards = phase == 2;
            if (spawningGuards) deletionClock = DeletionInterval;
            GameSfx.Play(Sfx.BossPhase);
            ShotCamera?.Shake(.48f, .48f);
            BodyPixel(phase == 1 ? PixelCue.ForkPhase : PixelCue.SudoPhase, 1.65f, TransitionSeconds, Body.sortingOrder + 2);
            Announce(phase == 1 ? "Root · fork 炸弹" : "Root · sudo rm -rf");
        }

        protected override void TickEncounter(float dt)
        {
            visualAge += dt;
            TickAfterglows(dt);
            TickPixels(dt);
            UpdateBodyMotion(dt);
            if (transition > 0f)
            {
                transition = Mathf.Max(0f, transition - dt);
                if (transition <= 0f && spawningGuards) SpawnGuards();
                return;
            }

            if (hadGuards && LiveGuards == 0)
            {
                hadGuards = false;
                RemoveGuardShield();
                vulnerable = GuardBreakSeconds;
                CancelCast();
                BodyPixel(PixelCue.ShieldBreak, 1.65f, .71f, Body.sortingOrder + 3);
                GameSfx.Play(Sfx.EnemyShieldBreak);
                Announce("守护进程全部摧毁 · Root 僵直");
            }

            TickChildren(dt);
            if (Vitals.Health <= 0) return;
            if (phase == 2) TickDeletion(dt);
            if (Vitals.Health <= 0) return;
            if (vulnerable > 0f)
            {
                vulnerable -= dt;
                return;
            }

            if (Stunned)
            {
                CancelCast();
                recovery = Mathf.Max(recovery, .35f);
                return;
            }

            TickBlink(dt);
            if (cast != null)
            {
                TickCast(dt);
                return;
            }

            if (blinkWarning != null || deletionBlinkWarning != null) return;
            MoveTowardPlayer(dt);
            recovery -= dt;
            if (recovery > 0f) return;
            Skill[] pattern = Patterns[phase];
            Skill next = pattern[patternIndex];
            if (!(deletionActive && deletedBands == DeletionBandCount) &&
                blinkClock <= BlinkWarningSeconds + Windup(next)) return;
            BeginCast(next);
            patternIndex = (patternIndex + 1) % pattern.Length;
        }

        private void MoveTowardPlayer(float dt)
        {
            if (BeingPulled) return;
            Vector2 delta = Player.Position - Position;
            if (delta.sqrMagnitude < .001f) return;
            facing = delta.normalized;
            float step = Mathf.Clamp(delta.magnitude - 2.4f, 0f, MoveSpeed * dt * SpeedMultiplier);
            Vector2 half = deletionActive && deletedBands == DeletionBandCount
                ? ArenaHalfSize * .25f
                : ArenaHalfSize;
            Position = ClampToBounds(Position + facing * step, half, HurtRadius + .12f);
        }

        private void TickBlink(float dt)
        {
            if (deletionBlinkWarning != null || (deletionActive && deletedBands == DeletionBandCount)) return;
            blinkClock -= dt;
            if (blinkWarning == null)
            {
                if (blinkClock > BlinkWarningSeconds || cast != null) return;
                Vector2 offset = Position - ArenaCenter;
                float x = (offset.x > 0f ? -1f : 1f) * ArenaHalfSize.x * (.35f + .42f * RandomUnit());
                float y = ArenaHalfSize.y * (RandomUnit() * 1.4f - .7f);
                blinkTarget = ClampToBounds(ArenaCenter + new Vector2(x, y), ArenaHalfSize, HurtRadius + .12f);
                blinkClock = BlinkWarningSeconds;
                blinkWarning = BlinkMarker("Root 瞬移落点", blinkTarget);
                GameSfx.Play(Sfx.BossPortal);
            }
            else if (blinkClock <= 0f)
            {
                BlinkTo(blinkTarget);
                blinkClock = BlinkInterval;
            }
        }

        private void BlinkTo(Vector2 destination)
        {
            RemoveTelegraph(blinkWarning);
            blinkWarning = null;
            Pixel(PixelCue.BlinkOut, Position, 1.38f, .31f, Body.sortingOrder + 1);
            Position = destination;
            Pixel(PixelCue.BlinkIn, Position, 1.38f, .35f, Body.sortingOrder + 1);
            GameSfx.Play(Sfx.BossDeploy);
        }

        private void BeginCast(Skill skill)
        {
            var origin = Position;
            Vector2 towardPlayer = Player.Position - origin;
            if (towardPlayer.sqrMagnitude > .001f) facing = towardPlayer.normalized;
            switch (skill)
            {
                case Skill.LockHand:
                    LockHand?.Invoke(phase == 0 ? 1 : 2, phase == 0 ? 6f : 5f, Random);
                    BodyPixel(PixelCue.Lock, 1.38f, .77f, Body.sortingOrder + 4);
                    recovery = CastRecovery;
                    return;
                case Skill.Sector:
                    cast = new Cast(skill, Windup(skill), origin, facing);
                    cast.Marks.Add(SectorMark(origin, facing, false));
                    GameSfx.Play(Sfx.BossSealWarn);
                    break;
                case Skill.Audit:
                    cast = new Cast(skill, Windup(skill), origin, facing);
                    AddAuditWarnings(cast);
                    GameSfx.Play(Sfx.BossRainWarn);
                    break;
                case Skill.Fork:
                    cast = new Cast(skill, Windup(skill), origin, facing);
                    var forkMark = Marker("Root 分叉纹路", origin);
                    cast.Marks.Add(forkMark);
                    AttachedPixel(PixelCue.Fork, forkMark, origin, 1.46f, cast.Duration,
                        Body.sortingOrder + 2, Angle(facing));
                    GameSfx.Play(Sfx.BossPortal);
                    break;
                case Skill.Kill:
                    if (children.Count == 0)
                    {
                        recovery = .4f;
                        return;
                    }
                    cast = new Cast(skill, Windup(skill), origin, facing);
                    var killMark = Marker("Root 终止信号", origin);
                    cast.Marks.Add(killMark);
                    AttachedPixel(PixelCue.KillSignal, killMark, origin, 1.58f, cast.Duration,
                        Body.sortingOrder + 2);
                    UpdateChildKillMarks();
                    SetKillOverlay(.34f);
                    Announce("Root · kill -9");
                    GameSfx.Play(Sfx.BossMeteorWarn);
                    break;
            }
        }

        private static float Windup(Skill skill) => skill switch
        {
            Skill.Sector => .8f,
            Skill.Audit or Skill.Fork => 1f,
            Skill.Kill => 1.5f,
            _ => 0f
        };

        private void TickCast(float dt)
        {
            Cast active = cast;
            if (active.Skill == Skill.Audit && active.Age < AuditLockTime)
            {
                Vector2 toPlayer = Player.Position - active.Origin;
                if (toPlayer.sqrMagnitude > .001f) active.Direction = toPlayer.normalized;
                UpdateAuditWarnings(active);
            }

            active.Age += dt;
            CastFloorProgress(active);
            if (active.Skill == Skill.Kill)
            {
                SetKillOverlay(.35f + .3f * Mathf.PingPong(active.Age * 2f, 1f));
                UpdateChildKillMarks();
            }
            if (active.Age < active.Duration) return;

            ResolveCast(active);
            CancelCast();
            recovery = active.Skill switch
            {
                Skill.Fork => 3.6f,
                Skill.Kill => 1.6f,
                _ => CastRecovery
            };
        }

        private void ResolveCast(Cast active)
        {
            switch (active.Skill)
            {
                case Skill.Sector:
                    Vector2 offset = Player.Position - active.Origin;
                    if (InSector(active, offset)) Vitals.TakeDamageFrom(18, offset, source: this);
                    SectorMark(active.Origin, active.Direction, true);
                    Pixel(PixelCue.Breach, active.Origin, 1.4f, .37f, Body.sortingOrder + 2,
                        Angle(active.Direction));
                    GameSfx.Play(Sfx.BossSealRelease);
                    break;
                case Skill.Audit:
                    ReleaseAudit(active);
                    break;
                case Skill.Fork:
                    SpawnChild(active.Origin + Rotate(active.Direction, 105f) * 1.75f);
                    SpawnChild(active.Origin + Rotate(active.Direction, -105f) * 1.75f);
                    GameSfx.Play(Sfx.BossDeploy);
                    break;
                case Skill.Kill:
                    DetonateChildren();
                    break;
            }
        }

        private void CancelCast()
        {
            if (cast == null) return;
            foreach (var mark in cast.Marks) RemoveTelegraph(mark);
            if (cast.Skill == Skill.Kill)
            {
                SetKillOverlay(0f);
                foreach (var child in children) ClearChildKillMark(child);
            }
            cast = null;
        }

        private void AddAuditWarnings(Cast active)
        {
            active.Marks.Add(CreateRay("Root 审计预警", active.Origin, AuditBlue, false));
            if (phase == 2) active.Marks.Add(CreateRay("Root 侧向审计预警", active.Origin, PhaseViolet, false));
            UpdateAuditWarnings(active);
        }

        private void UpdateAuditWarnings(Cast active)
        {
            for (int i = 0; i < active.Marks.Count; i++)
                PositionRay(active.Marks[i], active.Origin, AuditDirection(active, i));
        }

        private Vector2 AuditDirection(Cast active, int index) =>
            phase == 2 ? Rotate(active.Direction, index == 0 ? -15f : 15f) : active.Direction;

        private GameObject CreateRay(string label, Vector2 origin, Color tint, bool released)
        {
            var mark = Telegraph(label, Rectangle(origin, new Vector2(.5f, AuditHalfWidth)), tint, released ? .48f : .12f);
            Floor(mark, released ? FloorCue.AuditStrike : FloorCue.AuditWarning, origin,
                new Vector2(1f, 2f * AuditHalfWidth), Vector2.right, tint, .27f, released);
            return mark;
        }

        private float PositionRay(GameObject ray, Vector2 origin, Vector2 direction)
        {
            Vector2 offset = origin - ArenaCenter;
            float edgeX = EdgeDistance(offset.x, direction.x, ArenaHalfSize.x);
            float edgeY = EdgeDistance(offset.y, direction.y, ArenaHalfSize.y);
            float length = Mathf.Max(.01f, Mathf.Min(edgeX, edgeY));
            ray.transform.localScale = new Vector3(length, 1f, 1f);
            ray.transform.SetPositionAndRotation(origin + direction * (length * .5f),
                Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            MoveFloor(ray);
            return length;
        }

        private static float EdgeDistance(float offset, float direction, float half) =>
            Mathf.Abs(direction) < .0001f ? float.PositiveInfinity : (Mathf.Sign(direction) * half - offset) / direction;

        private void ReleaseAudit(Cast active)
        {
            Vector2 offset = Player.Position - active.Origin;
            for (int i = 0; i < active.Marks.Count; i++)
            {
                Vector2 direction = AuditDirection(active, i);
                var ray = CreateRay("Root 审计射线", active.Origin, i == 0 ? AuditBlue : PhaseViolet, true);
                float length = PositionRay(ray, active.Origin, direction);
                afterglows.Add((ray, .27f));

                float along = Mathf.Clamp(Vector2.Dot(offset, direction), 0f, length);
                var normal = new Vector2(-direction.y, direction.x);
                float across = Mathf.Clamp(Vector2.Dot(offset, normal), -AuditHalfWidth, AuditHalfWidth);
                Vector2 nearest = along * direction + across * normal;
                if ((offset - nearest).sqrMagnitude <= Player.CombatRadius * Player.CombatRadius)
                    Vitals.TakeDamageFrom(14, direction, Sfx.BossRainHit, this);
            }
            Pixel(phase == 2 ? PixelCue.TwinAudit : PixelCue.Audit, active.Origin, 1.43f, .38f,
                Body.sortingOrder + 3, Angle(active.Direction));
            GameSfx.Play(Sfx.BossRainFire);
        }

        private bool InSector(Cast active, Vector2 offset)
        {
            float radius = Player.CombatRadius;
            if (offset.sqrMagnitude > (SectorRadius + radius) * (SectorRadius + radius)) return false;
            if (Vector2.Angle(offset, active.Direction) <= SectorHalfAngle) return true;
            Vector2 left = Rotate(active.Direction, SectorHalfAngle) * SectorRadius;
            Vector2 right = Rotate(active.Direction, -SectorHalfAngle) * SectorRadius;
            return CardAttackArea.SegmentTouches(offset, radius, Vector2.zero, left)
                || CardAttackArea.SegmentTouches(offset, radius, Vector2.zero, right);
        }

        private void SpawnChild(Vector2 position)
        {
            if (children.Count >= ChildLimit) return;
            var target = SpawnSummon("子进程", SwarmSprite,
                ClampToBounds(position, ArenaHalfSize, ChildRadius), 30, ChildRadius);
            children.Add(new ChildProcess(target));
            Pixel(PixelCue.ChildSpawn, target.transform.position, .74f, .39f, Body.sortingOrder + 1);
        }

        private void TickChildren(float dt)
        {
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var child = children[i];
                if (child.Target != null && child.Target.Alive) continue;
                RemoveTelegraph(child.AttackWarning);
                ClearChildKillMark(child);
                children.RemoveAt(i);
            }
            int count = children.Count;
            for (int i = 0; i < count; i++)
            {
                ChildProcess child = children[i];
                var target = child.Target;
                Vector2 position = target.transform.position;
                child.Age += dt;
                if (!child.Replicated && child.Age >= ChildCloneSeconds && children.Count < ChildLimit)
                {
                    child.Replicated = true;
                    Vector2 direction = Rotate(Vector2.right, RandomUnit() * 360f);
                    SpawnChild(position + direction * 1.15f);
                    Pixel(PixelCue.Clone, position, 1f, .46f, Body.sortingOrder + 1, Angle(direction));
                    GameSfx.Play(Sfx.BossDeploy, minimumInterval: .13f);
                }

                if (target.Stunned || target.BeingPulled)
                {
                    RemoveTelegraph(child.AttackWarning);
                    child.AttackWarning = null;
                    child.Attack.Reset();
                    continue;
                }
                Vector2 delta = Player.Position - position;
                const float range = 1.05f;
                bool near = delta.sqrMagnitude <= range * range;
                MeleePhase previous = child.Attack.Phase;
                if (child.Attack.Step(dt, near))
                {
                    RemoveTelegraph(child.AttackWarning);
                    child.AttackWarning = null;
                    if (near) Vitals.TakeDamageFrom(5, delta, source: target);
                    Pixel(PixelCue.ChildHit, position, 1.08f, .28f, Body.sortingOrder + 1, Angle(delta));
                    GameSfx.Play(Sfx.SwarmAttack, minimumInterval: .13f);
                    if (Vitals.Health <= 0) return;
                }
                if (previous == MeleePhase.Chase && child.Attack.Phase == MeleePhase.Windup)
                {
                    child.AttackWarning = Telegraph("子进程攻击预警", Circle(position, range, 10), PhaseViolet, .08f);
                    AttachedPixel(PixelCue.ChildWarning, child.AttackWarning, position, 1.01f,
                        MeleeCycle.WindupSeconds, -7, Angle(delta));
                }
                if (child.Attack.Phase == MeleePhase.Chase && !near)
                {
                    float step = Mathf.Min(3.6f * dt * target.SpeedMultiplier, delta.magnitude - range);
                    target.transform.position = ClampToBounds(position + delta.normalized * step,
                        ArenaHalfSize, ChildRadius);
                }
            }
        }

        private void DetonateChildren()
        {
            int living = 0;
            foreach (var child in children)
            {
                if (child.Target == null || !child.Target.Alive) continue;
                Vector2 origin = child.Target.transform.position;
                Vector2 offset = Player.Position - origin;
                float radius = ChildBlastRadius + Player.CombatRadius;
                if (offset.sqrMagnitude <= radius * radius) Vitals.TakeDamageFrom(10, offset, source: this);
                RemoveTelegraph(child.AttackWarning);
                ClearChildKillMark(child);
                Pixel(PixelCue.Detonation, origin, 1.08f, .51f, Body.sortingOrder + 2);
                HealTrail(origin);
                child.Target.Dismiss();
                living++;
            }
            children.Clear();
            if (living == 0) return;
            Heal(living * 30);
            GameSfx.Play(Sfx.BossMeteorLand);
            ShotCamera?.Shake(.25f, .28f);
        }

        private void SpawnGuards()
        {
            spawningGuards = false;
            hadGuards = true;
            CreateGuardArt();
            for (int i = 0; i < guardShields.Length; i++)
            {
                Vector2 corner = ArenaCenter + GuardOffset(i);
                guards.Add(SpawnSummon("守护进程", guardSprite, corner, 50, GuardRadius, anchored: true));
                Pixel(PixelCue.GuardianRaise, corner, 1.07f, .75f, Body.sortingOrder + 1);
            }
            ShowGuardShield();
            GameSfx.Play(Sfx.BossDeploy);
            Announce("四个守护进程为 Root 分担伤害");
        }

        private void TickDeletion(float dt)
        {
            deletionClock -= dt;
            if (!deletionActive)
            {
                if (deletionClock > 0f) return;
                deletionActive = true;
                deletionClock = DeletionInterval;
                deletionAge = exposure = 0f;
                deletedBands = 0;
                deletionBlinkDone = false;
                DrawDeletionBand(0, false);
                GameSfx.Play(Sfx.BossSealWarn);
                Announce("Root · rm -rf / · 前往内圈");
                return;
            }

            deletionAge += dt;
            TickDeletionBlink(dt);
            if (deletedBands < DeletionBandCount && deletionAge >= (deletedBands + 1) * DeletionBandSeconds)
            {
                ClearMarks(deletionPreview);
                DrawDeletionBand(deletedBands, true);
                deletedBands++;
                GameSfx.Play(Sfx.BossSealRelease);
                if (deletedBands < DeletionBandCount)
                    DrawDeletionBand(deletedBands, false);
            }

            Vector2 offset = Player.Position - ArenaCenter;
            float zone = Mathf.Max(Mathf.Abs(offset.x) / ArenaHalfSize.x,
                Mathf.Abs(offset.y) / ArenaHalfSize.y);
            if (deletedBands > 0 && zone > 1f - deletedBands * .25f)
            {
                exposure += dt;
                if (exposure >= 1f)
                {
                    exposure -= 1f;
                    Vitals.TakeDamageFrom(8, -offset, source: this);
                }
            }
            else
                exposure = 0f;

            if (deletionAge < DeletionBandCount * DeletionBandSeconds + DeletionAfterglow) return;
            deletionActive = false;
            ClearMarks(deletionHazard);
            RemoveTelegraph(deletionBlinkWarning);
            deletionBlinkWarning = null;
        }

        private void TickDeletionBlink(float dt)
        {
            if (deletionBlinkDone || vulnerable > 0f || Stunned ||
                deletionAge < DeletionBandCount * DeletionBandSeconds - BlinkWarningSeconds) return;
            if (deletionBlinkWarning == null)
            {
                CancelCast();
                RemoveTelegraph(blinkWarning);
                blinkWarning = null;
                Vector2 safeOffset = ArenaHalfSize * .17f;
                deletionBlinkTarget = ArenaCenter + new Vector2((2f * RandomUnit() - 1f) * safeOffset.x,
                    (2f * RandomUnit() - 1f) * safeOffset.y);
                deletionBlinkWarning = BlinkMarker("Root 内圈瞬移落点", deletionBlinkTarget);
                deletionBlinkClock = BlinkWarningSeconds;
                GameSfx.Play(Sfx.BossPortal);
                return;
            }

            deletionBlinkClock -= dt;
            if (deletionBlinkClock > 0f) return;
            RemoveTelegraph(deletionBlinkWarning);
            deletionBlinkWarning = null;
            deletionBlinkDone = true;
            BlinkTo(deletionBlinkTarget);
            blinkClock = BlinkInterval;
            recovery = Mathf.Max(recovery, .5f);
        }

        private void DrawDeletionBand(int band, bool activated)
        {
            Vector2 outer = ArenaHalfSize * (1f - band * .25f);
            Vector2 inner = ArenaHalfSize * (.75f - band * .25f);
            float fill = activated ? .28f : .11f;
            Color tint = activated ? DeleteRed : new Color32(255, 135, 150, 218);
            List<GameObject> marks = activated ? deletionHazard : deletionPreview;
            string label = activated ? "被删除的数据区" : "数据区删除预警";
            Vector2 horizontalHalf = new(outer.x, (outer.y - inner.y) * .5f);
            Vector2 verticalHalf = new((outer.x - inner.x) * .5f, inner.y);
            Vector2 top = ArenaCenter + Vector2.up * ((outer.y + inner.y) * .5f);
            Vector2 right = ArenaCenter + Vector2.right * ((outer.x + inner.x) * .5f);
            AddDeletionStrip(marks, label, top, horizontalHalf, tint, fill, Vector2.up, activated);
            AddDeletionStrip(marks, label, 2f * ArenaCenter - top, horizontalHalf, tint, fill, Vector2.down, activated);
            AddDeletionStrip(marks, label, right, verticalHalf, tint, fill, Vector2.right, activated);
            AddDeletionStrip(marks, label, 2f * ArenaCenter - right, verticalHalf, tint, fill, Vector2.left, activated);
        }

        private void ClearMarks(List<GameObject> marks)
        {
            foreach (var mark in marks) RemoveTelegraph(mark);
            marks.Clear();
        }

        protected override void OnBossDefeated()
        {
            Pixel(PixelCue.Death, Position, 1.9f, .95f, Body.sortingOrder + 4, detached: true);
            ShotCamera?.Shake(.62f, .53f);
            Announce("根权限已夺取");
        }

        protected override void OnEncounterStopped()
        {
            ClearHandLocks?.Invoke();
            RemoveGuardShield();
            ClearRootEffects(releaseMaterial: true);
            DestroyKillOverlay();
            if (guardSprite != null) Destroy(guardSprite);
            if (guardTexture != null) Destroy(guardTexture);
        }

        private Vector2 ClampToBounds(Vector2 position, Vector2 half, float margin)
        {
            half = Vector2.Max(Vector2.zero, half - Vector2.one * margin);
            return ArenaCenter + new Vector2(
                Mathf.Clamp(position.x - ArenaCenter.x, -half.x, half.x),
                Mathf.Clamp(position.y - ArenaCenter.y, -half.y, half.y));
        }

        private float RandomUnit() => (float)Random.NextDouble();

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            return new Vector2(direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }
    }
}
