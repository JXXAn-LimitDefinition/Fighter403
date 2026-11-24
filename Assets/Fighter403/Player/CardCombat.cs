using System;
using System.Collections;
using System.Collections.Generic;
using Fighter403.UI;
using UnityEngine;
using Fighter403.Audio;

namespace Fighter403.Player
{
    [RequireComponent(typeof(RoundPlayerMovement), typeof(SpriteRenderer))]
    public sealed class CardCombat : MonoBehaviour
    {
        private const float RangeFlashSeconds = 0.09f;
        // The Phase Dash sprites reach their full displacement on source tick 20.
        private const int DashEffectTravelFrames = 20;
        private const int DashEffectFrameStride = 2;
        private const float DashEffectTravelDistance = 88f / 26f;
        private const int FlurryFinisherWindupFrames = 9;
        private const int FlurryFinisherHoldFrames = 12;
        private const int FlurryReturnTravelFrames = 9;
        private const float FlurryFinisherZoom = 1.14f;

        private RoundPlayerMovement movement;
        private readonly Cast[] casts = { new(), new() };
        private SpriteRenderer playerSprite;
        private RoundPlayerMotion playerMotion;
        private ThousandSlashesGroundEffects flurryGround;
        private SandevistanAfterimages afterimages;
        private int blockTints;
        private Color savedTint;
        private const float BlackHoleArtRadius = 5f;
        private const float BigBangArtRadius = 6f;
        private readonly List<RaycastHit2D> displacementHits = new();
        internal event Action EnemyKilled;
        internal SandevistanVfxSettings SandevistanVisuals { get; private set; }

        private void Awake()
        {
            movement = GetComponent<RoundPlayerMovement>();
            playerSprite = GetComponent<SpriteRenderer>();
            playerMotion = GetComponent<RoundPlayerMotion>();
        }

        private Vector2 Origin => transform.position;

        internal sealed class Cast
        {
            internal SpriteRenderer Front, Back;
            internal CardAimPreview Outline;
            internal readonly List<(CombatTarget Target, Vector2 Start)> DisplacedTargets = new();
            internal readonly Dictionary<CombatTarget, Vector2> PairedPullOffsets = new();
            internal Vector2? FlurryReturnPosition;
            internal GameSfx.Loop SoundLoop;
            internal bool GuardTint, DrivesMotion, Paired;
            internal double StartedAt;
            internal int DelayFrames;
            internal float Elapsed => (float)(Time.timeAsDouble - StartedAt) - DelayFrames / 60f;
            internal int Tick => Mathf.FloorToInt((float)((Time.timeAsDouble - StartedAt) * 60d) + 0.0001f) - DelayFrames;
        }

        internal Cast PrepareCast(int index, bool drivesMotion, bool paired, double startedAt, int delayFrames)
        {
            var cast = casts[index];
            cast.DrivesMotion = drivesMotion;
            cast.Paired = paired;
            cast.StartedAt = startedAt;
            cast.DelayFrames = delayFrames;
            cast.PairedPullOffsets.Clear();
            cast.Outline ??= new CardAimPreview(playerSprite.sortingLayerID, playerSprite.sortingOrder + 2);
            return cast;
        }

        private void LateUpdate()
        {
            // Freeze with each hit and keep fading once the hand slows combat time again.
            if (Time.deltaTime > 0f) flurryGround?.Advance(Time.unscaledDeltaTime);
        }

        internal CombatTarget Nearest(Predicate<CombatTarget> eligible = null)
        {
            CombatTarget result = null;
            float distance = float.PositiveInfinity;
            foreach (var dummy in CombatTarget.Active)
            {
                if (!dummy.Alive || (eligible != null && !eligible(dummy))) continue;
                float d = ((Vector2)dummy.transform.position - Origin).sqrMagnitude;
                if (d > distance || (d == distance && result != null)) continue;
                distance = d;
                result = dummy;
            }
            return result;
        }

        internal bool CanTarget(CombatTarget target, CardDefinition card) => target != null && target.Alive &&
            (card.Kind == CardKind.ThousandSlashes ||
                CombatRules.InRange(((Vector2)target.transform.position - Origin).sqrMagnitude, card.Distance));

        internal bool TryGetFlurryDestination(CombatTarget target, int obstacleMask, out Vector2 destination,
            bool crossTarget = false)
        {
            target.GetHurtCircle(out Vector2 center, out float radius);
            return TryGetFlurryDestination(center, radius, obstacleMask, out destination, crossTarget);
        }

        private bool TryGetFlurryDestination(Vector2 center, float radius, int obstacleMask,
            out Vector2 destination, bool crossTarget, Vector2? from = null)
        {
            Vector2 outward = (from ?? movement.Position) - center;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : -movement.FacingDirection;
            if (crossTarget) outward = Quaternion.Euler(0f, 0f, 27f) * -outward;
            return movement.TryGetBlinkDestination(center, radius, outward, obstacleMask, out destination);
        }

        internal Vector2 AimDirection(CombatTarget target, Vector2 origin)
        {
            Vector2 offset = (Vector2)target.transform.position - origin;
            return offset.sqrMagnitude > 0.00000001f ? offset.normalized : movement.FacingDirection;
        }

        internal IEnumerator Play(Cast cast, CardDefinition card, Vector2 direction, CombatTarget target, CombatVitals vitals,
            float multiplier, Action<CardDefinition> onHit, Vector2? dashDestination, int dashObstacleMask) => card.Kind switch
            {
                CardKind.LimitedRedBlackHole => PlayBlackHole(cast, card, vitals, multiplier, onHit, dashObstacleMask),
                CardKind.InfiniteBlueWhiteHole => PlayWhiteHole(cast, card, vitals, onHit, dashObstacleMask),
                CardKind.BigBang => PlayBigBang(cast, card, vitals, onHit),
                _ => PlayCard(cast, card, direction, target, vitals, multiplier, onHit, dashDestination, dashObstacleMask)
            };

        private IEnumerator PlayCard(Cast cast, CardDefinition card, Vector2 direction, CombatTarget target, CombatVitals vitals,
            float multiplier, Action<CardDefinition> onHit, Vector2? dashDestination, int dashObstacleMask)
        {
            while (cast.Tick < 0) yield return null;
            var kind = card.Kind;
            var shape = card.HitShape;
            Vector2 origin = Origin;
            Vector2 dashStart = movement.Position;
            if (card.IsDirectional && !dashDestination.HasValue)
            {
                var aimTarget = target != null && target.Alive ? target : Nearest();
                if (aimTarget != null) direction = AimDirection(aimTarget, kind == CardKind.PhaseDash ? dashStart : origin);
            }
            direction = direction.sqrMagnitude < 0.001f ? movement.FacingDirection : direction.normalized;

            Vector2 landing = target != null ? (Vector2)target.transform.position : origin;
            Vector2 safeDestination = dashStart;
            DungeonBreakableProp dashEndProp = null;
            float dashDistance = 0f;
            if (kind == CardKind.PhaseDash)
            {
                dashDistance = card.Distance;
                if (dashDestination.HasValue)
                {
                    Vector2 offset = dashDestination.Value - dashStart;
                    dashDistance = Mathf.Min(card.Distance, offset.magnitude);
                    if (offset.sqrMagnitude > 0.00000001f) direction = offset.normalized;
                }

                safeDestination = movement.GetDashDestination(dashStart, direction, dashDistance, dashObstacleMask,
                    out dashEndProp, physicalDash: cast.DrivesMotion);
                dashDistance = Vector2.Distance(dashStart, safeDestination);
                if (cast.DrivesMotion)
                    movement.BeginCardDash(direction, dashDistance, card.ActiveEndFrame / 60f, dashObstacleMask);
            }
            else if (cast.DrivesMotion && kind != CardKind.DualThread)
            {
                movement.BeginCardMotion(direction, false);
            }

            var hitTargets = new HashSet<CombatTarget>();
            var dashHitProps = kind == CardKind.PhaseDash ? new HashSet<DungeonBreakableProp>() : null;
            var dashPrevious = dashStart;
            int nextPulse = 0;
            bool movementSoundStarted = false;
            if (kind == CardKind.Starfall) GameSfx.Play(Sfx.StarfallWarn);
            if (kind == CardKind.SlashPreparation) GameSfx.Play(Sfx.SlashPrep, 0.72f);
            if (kind == CardKind.PhaseDash && dashDistance > 0.001f) GameSfx.Play(Sfx.DashStart);
            int points = CombatRules.ScaledPoints(card.Damage, multiplier);
            ProgramEnemy.CopyPlayerAttack(vitals, card, points);
            bool feedbackSent = false;
            float duration = card.EndFrame / 60f;
            float lastRangeFlash = float.NegativeInfinity;
            float dashStoppedAt = float.PositiveInfinity;
            CardAttackArea shownArea = default;
            while (true)
            {
                float elapsed = cast.Elapsed;
                int tick = Mathf.Min(cast.Tick, card.EndFrame - 1);
                Vector2 visualOrigin = Origin;
                Vector2 dashCurrent = movement.Position;
                bool landed = false;
                bool dashing = false;
                if (kind == CardKind.PhaseDash)
                {
                    DungeonBreakableProp stepProp = null;
                    if (cast.Paired)
                    {
                        // Resolve the sweep on the shared impact frame even if physics has not yet moved the body.
                        float progress = Mathf.Clamp01(elapsed * 60f / card.ActiveEndFrame);
                        float travelled = Mathf.Max(0f, Vector2.Dot(dashPrevious - dashStart, direction));
                        float stepDistance = Mathf.Max(0f, dashDistance * progress * progress - travelled);
                        dashCurrent = movement.GetDashDestination(dashPrevious, direction, stepDistance, dashObstacleMask,
                            out stepProp);
                        dashing = (cast.DrivesMotion && movement.DashInProgress) || progress < 1f;
                    }
                    else dashing = movement.DashInProgress;
                    if (tick >= card.ActiveStartFrame)
                    {
                        int propDamage = PropDamage(points, vitals);
                        if (dashCurrent != dashPrevious)
                        {
                            shownArea = new CardAttackArea(card, dashPrevious, direction, dashCurrent);
                            landed = HitArea(card, shownArea, points, hitTargets, vitals, out bool damaged);
                            landed |= HitProps(shownArea, propDamage, dashPrevious, direction, dashHitProps);
                            if (damaged) GameSfx.Play(Sfx.DashHit, 0.68f);
                            dashPrevious = dashCurrent;
                            lastRangeFlash = elapsed;
                        }

                        // A physical obstacle can stop the body outside the attack capsule.
                        var contact = stepProp != null ? stepProp : cast.DrivesMotion ? movement.DashPropContact : null;
                        if (contact == null && !dashing && (dashCurrent - safeDestination).sqrMagnitude < 0.0025f)
                            contact = dashEndProp;
                        landed |= HitProp(contact, propDamage, direction, dashHitProps);
                    }
                    if (!movementSoundStarted && dashing && dashDistance > 0.001f && tick >= card.SlashStartFrame)
                    {
                        movementSoundStarted = true;
                        GameSfx.Play(Sfx.DashPass);
                    }
                    if (!dashing && float.IsPositiveInfinity(dashStoppedAt))
                    {
                        dashStoppedAt = elapsed;
                        if (dashDistance > 0.001f) GameSfx.Play(Sfx.DashStop);
                    }
                }
                else
                {
                    if (!movementSoundStarted && tick >= card.SlashStartFrame)
                    {
                        movementSoundStarted = true;
                        if (kind == CardKind.TriangleRift) GameSfx.Play(Sfx.RiftSlash);
                        if (kind is CardKind.QuickSlash or CardKind.SlashPreparation or CardKind.Slash)
                            GameSfx.Play(Sfx.LightSlash);
                    }
                    // Process each pulse even when a rendered frame passes several timeline ticks.
                    while (nextPulse < card.Pulses &&
                        tick >= card.ActiveStartFrame + nextPulse * card.ActiveFrames / card.Pulses)
                    {
                        nextPulse++;
                        if (kind == CardKind.MatrixGuard)
                        {
                            GameSfx.Play(Sfx.GuardUp);
                            vitals.AddBlock(CombatRules.ScaledPoints(card.Block, multiplier));
                            vitals.PauseBlockDecay(card.EffectSeconds);
                            cast.GuardTint = true;
                            if (blockTints++ == 0) savedTint = playerSprite.color;
                            playerSprite.color = card.GuardTint;
                        }
                        else if (kind == CardKind.Counterattack)
                        {
                            GameSfx.Play(Sfx.GuardUp);
                            vitals.AddBlock(card.Block);
                            vitals.ActivateCounterattack(card.EffectSeconds);
                        }
                        else if (kind == CardKind.GuardShift)
                        {
                            GameSfx.Play(Sfx.DualStart);
                            vitals.ActivateCounterattackMoment(card.EffectSeconds);
                        }
                        else if (kind == CardKind.Sandevistan)
                        {
                            GameSfx.Play(Sfx.SandevistanStart);
                            vitals.ActivateSandevistan(card.EffectSeconds);
                            SandevistanVisuals = card.SandevistanVfx;
                            if (afterimages == null)
                                afterimages = gameObject.AddComponent<SandevistanAfterimages>();
                            afterimages.Play(playerSprite, vitals, SandevistanVisuals);
                        }
                        else if (kind == CardKind.DualThread)
                        {
                            GameSfx.Play(Sfx.DualStart);
                            vitals.ActivateDualThread(card.EffectSeconds);
                        }
                        else if (shape == CardHitShape.Lock)
                        {
                            if (CanTarget(target, card)) landed |= HitTarget(target, points, vitals);
                        }
                        else if (shape != CardHitShape.None)
                        {
                            hitTargets.Clear();
                            Vector2 center = shape == CardHitShape.Landing ? landing
                                : shape == CardHitShape.Circle ? visualOrigin : origin;
                            shownArea = new CardAttackArea(card, center, direction);
                            if (kind == CardKind.OrbitSpin) GameSfx.Play(Sfx.SpinSlash);
                            if (kind == CardKind.Starfall) GameSfx.Play(Sfx.StarfallLand);
                            landed |= HitArea(card, shownArea, points, hitTargets, vitals, out bool damaged);
                            landed |= HitProps(shownArea, PropDamage(points, vitals), center, direction);
                            if (damaged && kind == CardKind.Starfall) GameSfx.Play(Sfx.StarfallHit, 0.4f);
                            lastRangeFlash = elapsed;
                        }
                    }
                }

                if (landed && !feedbackSent)
                {
                    feedbackSent = true;
                    onHit?.Invoke(card);
                }

                if (elapsed >= duration && lastRangeFlash != elapsed &&
                    !dashing) break;

                var rangeColor = CardHoverVisual.AttributeColor(card.Class);
                if (elapsed - lastRangeFlash < RangeFlashSeconds)
                {
                    float fade = 1f - (elapsed - lastRangeFlash) / RangeFlashSeconds;
                    var flashColor = Color.Lerp(rangeColor, Color.white, fade * 0.65f);
                    flashColor.a = fade;
                    cast.Outline.ShowArea(shownArea, transform.position.z, flashColor, Mathf.Lerp(0.025f, 0.064f, fade));
                }
                else if (tick < card.ActiveStartFrame && (shape is CardHitShape.Landing or CardHitShape.Dash))
                {
                    rangeColor.a = 0.62f;
                    var warning = new CardAttackArea(card, shape == CardHitShape.Landing ? landing : dashStart,
                        direction, shape == CardHitShape.Dash ? safeDestination : null);
                    cast.Outline.ShowArea(warning, transform.position.z, rangeColor);
                }
                else cast.Outline.Hide();

                Vector2 anchor = shape switch
                {
                    CardHitShape.Dash => dashStart,
                    CardHitShape.Triangle or CardHitShape.Arc => origin,
                    CardHitShape.Landing => landing,
                    _ => visualOrigin
                };
                Vector2 rotation = card.IsDirectional ? direction : Vector2.right;
                float stretch = 1f;
                int effectFrame = tick - card.SlashStartFrame;
                if (shape == CardHitShape.Dash && effectFrame >= 0)
                {
                    float travelled = Mathf.Max(0f, Vector2.Dot(dashCurrent - dashStart, direction));
                    float travelLength = dashing ? dashDistance : travelled;
                    effectFrame = travelLength > 0.0001f
                        ? DashEffectFrame(travelled, travelLength, elapsed - dashStoppedAt, direction, ref anchor, out stretch)
                        : -1;
                }
                Draw(ref cast.Front, card.SlashFront, effectFrame, anchor, rotation, stretch, 1);
                Draw(ref cast.Back, card.SlashBack, effectFrame, anchor, rotation, stretch, -1);
                yield return null;
            }

            FinishCast(cast);
        }

        private IEnumerator PlayBlackHole(Cast cast, CardDefinition card, CombatVitals vitals, float multiplier,
            Action<CardDefinition> onHit, int obstacleMask)
        {
            while (cast.Tick < 0) yield return null;
            Vector2 center = Origin;
            if (cast.DrivesMotion) movement.BeginCardMotion(movement.FacingDirection);
            var area = new CardAttackArea(card, center, Vector2.right);
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstacleMask);
            int pullStart = card.ActiveStartFrame / 5;
            int impact = card.ActiveStartFrame;
            float effectScale = card.Distance / BlackHoleArtRadius;
            int points = CombatRules.ScaledPoints(card.Damage, multiplier);
            var pairedCast = cast.Paired ? casts[cast == casts[0] ? 1 : 0] : null;
            ProgramEnemy.CopyPlayerAttack(vitals, card, points);
            bool openingSoundPlayed = false;
            bool captured = false;
            bool resolved = false;
            while (true)
            {
                int tick = cast.Tick;
                if (!openingSoundPlayed && tick >= card.SlashStartFrame)
                {
                    openingSoundPlayed = true;
                    GameSfx.Play(Sfx.BlackHoleOpen);
                }
                if (!captured && tick >= pullStart)
                {
                    captured = true;
                    cast.SoundLoop = GameSfx.StartLoop(Sfx.BlackHolePull, gameObject);
                    foreach (var enemy in CombatTarget.Active)
                    {
                        if (!enemy.Alive) continue;
                        enemy.GetHurtCircle(out Vector2 point, out float radius);
                        if (!area.Overlaps(point, radius)) continue;
                        enemy.PullingEffects++;
                        cast.DisplacedTargets.Add((enemy, point));
                        if (pairedCast != null) pairedCast.PairedPullOffsets[enemy] = point - center;
                    }
                }
                if (captured && !resolved)
                {
                    float progress = impact > pullStart ? Mathf.InverseLerp(pullStart, impact, cast.Elapsed * 60f) : 1f;
                    float pull = progress * progress * (3f - 2f * progress);
                    foreach (var (enemy, start) in cast.DisplacedTargets)
                        if (enemy != null && enemy.Alive && !enemy.Anchored)
                            MoveTargetTowards(enemy, Vector2.LerpUnclamped(start, center, pull), filter);
                    if (tick >= impact)
                    {
                        resolved = true;
                        GameSfx.StopLoop(ref cast.SoundLoop);
                        GameSfx.Play(Sfx.BlackHoleCollapse);
                        bool landed = false;
                        bool damaged = false;
                        foreach (var (enemy, _) in cast.DisplacedTargets)
                        {
                            if (enemy == null || !enemy.Alive) continue;
                            enemy.Stun(card.EffectSeconds);
                            landed |= HitTarget(enemy, points, vitals, playBodyImpact: false);
                            damaged |= enemy.LastDamage > 0;
                        }
                        ReleaseDisplacedTargets(cast);
                        landed |= HitProps(area, PropDamage(points, vitals), center, movement.FacingDirection);
                        if (damaged) GameSfx.Play(Sfx.BlackHoleHit, 0.4f);
                        if (landed) onHit?.Invoke(card);
                    }
                }

                if (tick >= card.EndFrame && resolved) break;
                Draw(ref cast.Back, card.SlashBack, tick - card.SlashStartFrame, center, Vector2.right, 1f, -1, effectScale);
                yield return null;
            }
            FinishCast(cast);
        }

        private IEnumerator PlayWhiteHole(Cast cast, CardDefinition card, CombatVitals vitals,
            Action<CardDefinition> onHit, int obstacleMask)
        {
            while (cast.Tick < 0) yield return null;
            Vector2 center = Origin;
            Vector2 facing = movement.FacingDirection;
            if (cast.DrivesMotion) movement.BeginCardMotion(facing);
            var area = new CardAttackArea(card, center, Vector2.right);
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstacleMask);
            float effectScale = card.Distance / BlackHoleArtRadius;
            bool released = false;
            float appliedDistance = 0f;
            while (true)
            {
                int tick = cast.Tick;
                if (!released && tick >= card.ActiveStartFrame)
                {
                    released = true;
                    vitals.AddBlock(card.Block);
                    GameSfx.Play(Sfx.WhiteHoleRelease);
                    foreach (var enemy in CombatTarget.Active)
                    {
                        if (!enemy.Alive || enemy.Anchored) continue;
                        enemy.GetHurtCircle(out Vector2 point, out float radius);
                        if (!area.Overlaps(point, radius)) continue;
                        enemy.PullingEffects++;
                        // Use the original bearing if the paired black hole pulled this target to its center.
                        Vector2 start = cast.PairedPullOffsets.TryGetValue(enemy, out Vector2 offset)
                            ? center + offset : point;
                        cast.DisplacedTargets.Add((enemy, start));
                    }
                    if (cast.DisplacedTargets.Count > 0) onHit?.Invoke(card);
                }
                else if (cast.DisplacedTargets.Count > 0)
                {
                    // Both cards in a dual-thread cast resolve their impact before the enemies move outward.
                    float progress = Mathf.InverseLerp(card.ActiveStartFrame, card.ActiveEndFrame, tick);
                    float remaining = 1f - progress;
                    float pushDistance = card.PushDistance * (1f - remaining * remaining * remaining);
                    float step = pushDistance - appliedDistance;
                    if (step > 0f)
                    {
                        foreach (var (enemy, start) in cast.DisplacedTargets)
                        {
                            if (enemy == null || !enemy.Alive) continue;
                            enemy.GetHurtCircle(out Vector2 position, out _);
                            Vector2 offset = start - center;
                            Vector2 outward = offset.sqrMagnitude > 0.000001f ? offset.normalized : facing;
                            MoveTargetTowards(enemy, position + outward * step, filter, passDisplacedEnemies: true);
                        }
                    }
                    appliedDistance = pushDistance;
                    if (tick >= card.ActiveEndFrame) ReleaseDisplacedTargets(cast);
                }

                if (tick >= card.EndFrame && cast.DisplacedTargets.Count == 0) break;
                Draw(ref cast.Front, card.SlashFront, tick - card.SlashStartFrame, center, Vector2.right, 1f, 1, effectScale);
                yield return null;
            }
            FinishCast(cast);
        }

        private IEnumerator PlayBigBang(Cast cast, CardDefinition card, CombatVitals vitals, Action<CardDefinition> onHit)
        {
            while (cast.Tick < 0) yield return null;
            Vector2 center = Origin;
            if (cast.DrivesMotion) movement.BeginCardMotion(movement.FacingDirection);
            float radius = BigBangArtRadius;
            foreach (var enemy in CombatTarget.Active)
            {
                if (!enemy.Alive) continue;
                enemy.GetHurtCircle(out Vector2 point, out float hurtRadius);
                radius = Mathf.Max(radius, Vector2.Distance(center, point) + hurtRadius);
            }

            bool resolved = false;
            while (true)
            {
                int tick = cast.Tick;
                if (!resolved && tick >= card.ActiveStartFrame)
                {
                    resolved = true;
                    GameSfx.Play(Sfx.BigBangBurst);
                    ResolveBigBang(vitals, card.Damage);
                    onHit?.Invoke(card);
                }

                if (tick >= card.EndFrame) break;
                Draw(ref cast.Front, card.SlashFront, tick - card.SlashStartFrame, center, Vector2.right,
                    1f, 1, radius / BigBangArtRadius);
                yield return null;
            }
            FinishCast(cast);
        }

        private void ResolveBigBang(CombatVitals vitals, int damage)
        {
            var targets = CombatTarget.Active.ToArray();
            // Resolve summons before their bosses can despawn them.
            Array.Sort(targets, static (left, right) => (left is FirewallBoss or DungeonBoss).CompareTo(right is FirewallBoss or DungeonBoss));
            int healingPercent = 0;
            foreach (var enemy in targets)
            {
                if (enemy == null || !enemy.Alive) continue;
                var (threshold, healing) = enemy switch
                {
                    FirewallBoss or DungeonBoss or ProgramEnemy { IsDungeonBoss: true } => (10, 20),
                    ProgramEnemy { IsElite: true } => (25, 10),
                    _ => (50, 5)
                };
                if (!enemy.ExecuteBelowHealth(threshold)) continue;
                healingPercent += healing;
                EnemyKilled?.Invoke();
            }

            if (healingPercent > 0)
            {
                vitals.HealPercent(healingPercent / 100f);
                return;
            }

            foreach (var enemy in targets)
                if (enemy != null && enemy.Hit(damage, 1f, playBodyImpact: false, bonusDamage: vitals.CounterattackBonus) && !enemy.Alive)
                    EnemyKilled?.Invoke();
            vitals.AddBlock(vitals.MaxBlock);
        }

        private void MoveTargetTowards(CombatTarget enemy, Vector2 destination, ContactFilter2D filter,
            bool passDisplacedEnemies = false)
        {
            if (enemy.Anchored) return;
            enemy.GetHurtCircle(out Vector2 center, out float radius);
            Vector2 offset = destination - center;
            float distance = offset.magnitude;
            if (distance < 0.0001f) return;
            Vector2 direction = offset / distance;
            float allowed = distance;
            Physics2D.SyncTransforms();
            Physics2D.CircleCast(center, radius, direction, filter, displacementHits, distance);
            foreach (var hit in displacementHits)
            {
                var obstacle = hit.collider.transform;
                if (obstacle == enemy.transform || obstacle.IsChildOf(enemy.transform)) continue;
                if (passDisplacedEnemies && hit.collider.TryGetComponent<CombatTarget>(out var other) &&
                    other.BeingPulled && !other.Anchored) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - 0.025f));
            }
            enemy.transform.position += (Vector3)(direction * allowed);
        }

        private static void ReleaseDisplacedTargets(Cast cast)
        {
            foreach (var (target, _) in cast.DisplacedTargets)
                if (target != null) target.PullingEffects--;
            cast.DisplacedTargets.Clear();
        }

        internal IEnumerator PlayFlurry(Cast cast, CardDefinition card, CombatTarget target, CombatVitals vitals,
            float multiplier, Action<CardDefinition, bool, float> onHit, int obstacleMask,
            ThousandSlashesScreenEffect screen)
        {
            while (cast.Tick < 0) yield return null;
            if (target == null || !target.Alive)
            {
                FinishCast(cast);
                yield break;
            }
            GameSfx.Play(Sfx.FlurryStart);
            screen?.Begin();
            flurryGround ??= new ThousandSlashesGroundEffects(playerSprite);
            Vector2 start = movement.Position;
            Vector2 strikePosition = start;
            Vector2 approach = AimDirection(target, start);
            target.GetHurtCircle(out Vector2 strikeCenter, out float targetRadius);
            int openingStrikes = card.Pulses - 1;
            int strikeSpan = openingStrikes == 0 ? 0 : Mathf.Max(openingStrikes,
                card.ActiveFrames - FlurryReturnTravelFrames - FlurryFinisherWindupFrames - FlurryFinisherHoldFrames);
            int vanishTick = Mathf.Max(0, card.ActiveStartFrame - 2);
            int blackoutTick = Mathf.Max(1, card.ActiveStartFrame - 9);
            int returnTick = card.ActiveStartFrame + Mathf.RoundToInt(strikeSpan);
            int windupTick = -1;
            int finisherTick = -1;
            int points = CombatRules.ScaledPoints(card.Damage, multiplier);
            int finisherPoints = CombatRules.ScaledPoints(card.FinisherDamage, multiplier);
            ProgramEnemy.CopyPlayerAttack(vitals, card, finisherPoints > 0 ? finisherPoints : points);
            int strikes = 0;
            FlurryStrike last = null, previous = null;
            Vector2 exit = start;
            Vector2 retreat = -approach;
            bool arrived = false, finished = false;
            float returnStartedAt = -1f;

            Face(approach);
            if (cast.DrivesMotion) playerMotion?.PrepareSlash();

            void Face(Vector2 direction)
            {
                if (cast.DrivesMotion) movement.BeginCardMotion(direction);
            }

            void Move(Vector2 position)
            {
                strikePosition = position;
                if (cast.DrivesMotion) movement.SetCardPosition(position);
            }

            while (isActiveAndEnabled)
            {
                float elapsed = cast.Elapsed;
                int tick = cast.Tick;
                screen?.SetBlackout(tick < card.ActiveStartFrame
                    ? Mathf.SmoothStep(0f, 1f, tick / (float)blackoutTick) : 0f);

                if (!arrived && tick >= vanishTick)
                {
                    if (target != null && target.Alive) target.GetHurtCircle(out strikeCenter, out targetRadius);
                    else if (!cast.Paired) break;
                    if (!TryGetFlurryDestination(strikeCenter, targetRadius, obstacleMask, out Vector2 landing,
                            false, strikePosition)) break;
                    cast.FlurryReturnPosition = start;
                    Move(landing);
                    screen?.Focus(target != null ? target.transform : null);
                    arrived = true;
                }

                float progress = strikes / (float)Mathf.Max(1, openingStrikes);
                int strikeTick = card.ActiveStartFrame +
                    Mathf.RoundToInt(strikeSpan * progress * (1.55f - 0.55f * progress));
                if (strikes < openingStrikes && tick >= strikeTick)
                {
                    if (target != null && target.Alive)
                        target.GetHurtCircle(out strikeCenter, out targetRadius);
                    if (!TryGetFlurryDestination(strikeCenter, targetRadius, obstacleMask, out Vector2 destination, true, strikePosition))
                    {
                        returnTick = tick;
                        strikes = openingStrikes;
                    }
                    else
                    {
                        Vector2 slashDirection = destination - strikePosition;
                        float hitProgress = strikes / (float)Mathf.Max(1, openingStrikes - 1);
                        float reach = Mathf.Lerp(1.55f, 2.55f, hitProgress);
                        previous = last;
                        last = new FlurryStrike(card.SlashFront, tick, strikeCenter,
                            slashDirection.sqrMagnitude > 0.0001f ? slashDirection.normalized : approach,
                            new Vector2(reach, 1.08f), points);
                        flurryGround.Add(last.FrameAt(tick, transform.position.z), finisher: false, mirror: (strikes & 1) != 0);
                        Face(strikeCenter - destination);
                        Move(destination);
                        if (cast.DrivesMotion) playerMotion?.PlaySlashRecoil(false);
                        GameSfx.Play(Sfx.FlurrySlash);
                        ApplyFlurryHits(last, tick, vitals);
                        onHit?.Invoke(card, false, hitProgress);
                        strikes++;

                        if (strikes == openingStrikes)
                            returnTick = Mathf.Max(returnTick, tick + 2);
                    }
                }

                if (arrived && strikes >= openingStrikes && tick >= returnTick && returnStartedAt < 0f)
                {
                    exit = strikePosition;
                    Vector2 backToStart = start - exit;
                    if (backToStart.sqrMagnitude > 0.0001f) retreat = backToStart.normalized;
                    returnStartedAt = elapsed;
                    Face(retreat);
                    screen?.Focus(target != null ? target.transform : null, start);
                }

                if (returnStartedAt >= 0f && cast.FlurryReturnPosition.HasValue)
                {
                    float travel = Mathf.Clamp01((elapsed - returnStartedAt) * 60f / FlurryReturnTravelFrames);
                    Vector2 retreatPosition = Vector2.Lerp(exit, start, 1f - (1f - travel) * (1f - travel));
                    Move(retreatPosition);
                    if (travel >= 1f)
                    {
                        cast.FlurryReturnPosition = null;
                        Vector2 windupDirection = strikeCenter - start;
                        Face(windupDirection.sqrMagnitude > 0.0001f ? windupDirection : approach);
                        if (cast.DrivesMotion) playerMotion?.PrepareSlash();
                        windupTick = tick;
                        finisherTick = tick + FlurryFinisherWindupFrames;
                    }
                }

                if (!finished && finisherTick >= 0)
                {
                    float windup = (tick - windupTick) / (float)FlurryFinisherWindupFrames;
                    float zoom = windup < 0.42f
                        ? Mathf.SmoothStep(1f, 0.92f, windup / 0.42f)
                        : Mathf.SmoothStep(0.92f, FlurryFinisherZoom, Mathf.InverseLerp(0.42f, 0.8f, windup));
                    screen?.SetZoom(zoom);
                }

                if (!finished && finisherTick >= 0 && tick >= finisherTick)
                {
                    if (target != null && target.Alive)
                        target.GetHurtCircle(out strikeCenter, out _);
                    Vector2 finishDirection = strikeCenter - start;
                    if (finishDirection.sqrMagnitude <= 0.0001f) finishDirection = approach;
                    finishDirection.Normalize();
                    Face(finishDirection);
                    previous = last;
                    last = new FlurryStrike(card.FinisherEffect, tick, strikeCenter, finishDirection,
                        screen?.FinisherScale(finishDirection) ?? Vector2.one, finisherPoints, finisher: true);
                    flurryGround.Add(last.FrameAt(tick, transform.position.z), finisher: true);
                    if (cast.DrivesMotion) playerMotion?.PlaySlashRecoil(true);
                    GameSfx.Play(Sfx.FlurryFinishSlash);
                    ApplyFlurryHits(last, tick, vitals);
                    onHit?.Invoke(card, true, 1f);
                    finished = true;
                    finisherTick = tick;
                }

                if (finished)
                {
                    int afterImpact = tick - finisherTick;
                    int releaseFrames = FlurryFinisherHoldFrames + card.RecoveryFrames;
                    if (afterImpact >= releaseFrames) break;
                    screen?.SetZoom(Mathf.SmoothStep(FlurryFinisherZoom, 1f,
                        Mathf.InverseLerp(4, releaseFrames, afterImpact)));
                }

                if (returnStartedAt >= 0f)
                {
                    Vector2 head = strikePosition;
                    float length = Vector2.Distance(exit, head);
                    int returnFrame = Mathf.FloorToInt((elapsed - returnStartedAt) * 60f);
                    int trailFrame = Mathf.Min(2, returnFrame / 3) + Mathf.Max(0, returnFrame - FlurryReturnTravelFrames);
                    Draw(ref cast.Back, card.BlinkEffect, length > 0.01f ? trailFrame : -1,
                        (exit + head) * 0.5f, retreat, length * 26f / 38f, -1);
                }
                ApplyFlurryHits(previous, tick, vitals);
                ApplyFlurryHits(last, tick, vitals);
                screen?.SetSlashes(previous?.FrameAt(tick, transform.position.z) ?? default,
                    last?.FrameAt(tick, transform.position.z) ?? default);
                yield return null;
            }
            screen?.End(blendCamera: true);
            FinishCast(cast);
        }

        private void ApplyFlurryHits(FlurryStrike strike, int tick, CombatVitals vitals)
        {
            if (strike == null || !strike.TryGetBlades(tick, out var blade, out var cross)) return;
            // Defeating a boss can unregister its summons during damage resolution.
            foreach (var target in CombatTarget.Active.ToArray())
            {
                if (!target.Alive || strike.Hit.Contains(target)) continue;
                target.GetHurtCircle(out var center, out float radius);
                if (!blade.Overlaps(center, radius) && !cross.Overlaps(center, radius)) continue;
                strike.Hit.Add(target);
                if (HitTarget(target, strike.Damage, vitals, playBodyImpact: false) &&
                    target.LastDamage > 0 && !strike.PlayedHitSound)
                {
                    strike.PlayedHitSound = true;
                    GameSfx.Play(strike.IsFinisher ? Sfx.FlurryFinishHit : Sfx.FlurryHit, 0.76f);
                }
            }

            int propDamage = PropDamage(strike.Damage, vitals);
            if (propDamage <= 0) return;
            var props = DungeonBreakableProp.Active;
            for (int i = props.Count - 1; i >= 0; i--)
            {
                var prop = props[i];
                prop.GetHurtCircle(out var center, out float radius);
                if (blade.Overlaps(center, radius) || cross.Overlaps(center, radius))
                    HitProp(prop, propDamage, strike.Direction, strike.HitProps);
            }
        }

        private sealed class FlurryStrike
        {
            private readonly Sprite[] frames;
            private readonly int start;
            private readonly Vector2 center, direction, scale;
            internal Vector2 Direction => direction;
            private readonly bool finisher;
            internal bool IsFinisher => finisher;
            internal bool PlayedHitSound;
            internal readonly int Damage;
            internal readonly HashSet<CombatTarget> Hit = new();
            internal readonly HashSet<DungeonBreakableProp> HitProps = new();

            internal FlurryStrike(Sprite[] frames, int start, Vector2 center, Vector2 direction,
                Vector2 scale, int damage, bool finisher = false)
            {
                this.frames = frames;
                this.start = start;
                this.center = center;
                this.direction = direction;
                this.scale = scale;
                this.finisher = finisher;
                Damage = damage;
            }

            internal ThousandSlashesScreenEffect.SlashFrame FrameAt(int tick, float z)
            {
                int index = tick - start;
                if (frames == null || index < 0 || index >= frames.Length) return default;
                return new ThousandSlashesScreenEffect.SlashFrame(frames[index], new Vector3(center.x, center.y, z), direction, scale);
            }

            internal bool TryGetBlades(int tick, out FlurryBladeArea blade, out FlurryBladeArea cross)
            {
                blade = cross = default;
                var frame = FrameAt(tick, 0f);
                if (frame.Sprite == null) return false;

                // Match the blade envelopes used to bake the 60 fps PixelFX atlases.
                float age = (tick - start) / 60f;
                Vector2 unitsPerPixel = scale / frame.Sprite.pixelsPerUnit;
                if (finisher)
                {
                    float fade = Mathf.Clamp01((age - 0.04f) / 0.172f);
                    if (fade >= 1f) return false;
                    GetFinisherBlades(center, direction, unitsPerPixel, Extend(age / 0.057f), fade, out blade, out cross);
                }
                else
                {
                    float fade = Mathf.Clamp01((age - 0.033f) / 0.093f);
                    if (fade >= 1f) return false;
                    float length = 66f + 21f * Extend(age / 0.047f);
                    blade = new FlurryBladeArea(center, direction, unitsPerPixel, 0f, length,
                        16f * (1f - fade * 0.94f), 3f * fade);
                }
                return true;
            }

            private static float Extend(float t)
            {
                float remainder = 1f - Mathf.Clamp01(t);
                return 1f - remainder * remainder * remainder * remainder;
            }
        }

        internal static void GetFinisherBlades(Vector2 center, Vector2 direction, Vector2 unitsPerPixel,
            float extend, float fade, out FlurryBladeArea blade, out FlurryBladeArea cross)
        {
            float width = 13f * (1f - fade * 0.95f);
            blade = new FlurryBladeArea(center, direction, unitsPerPixel, -45f, 161f + 8f * extend, width * 1.14f);
            cross = new FlurryBladeArea(center, direction, unitsPerPixel, 45f, 144f + 7f * extend, width);
        }

        internal readonly struct FlurryBladeArea
        {
            private readonly Vector2 tail, upper, tip, lower;
            private readonly bool active;

            internal FlurryBladeArea(Vector2 center, Vector2 direction, Vector2 unitsPerPixel,
                float angle, float length, float width, float offset = 0f)
            {
                active = true;
                Vector2 side = new(-direction.y, direction.x);
                float radians = angle * Mathf.Deg2Rad;
                Vector2 axis = new(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 normal = new(-axis.y, axis.x);

                Vector2 Place(float x, float y)
                {
                    Vector2 local = Vector2.Scale(axis * (x + offset) + normal * y, unitsPerPixel);
                    return center + direction * local.x + side * local.y;
                }

                tail = Place(-length * 0.46f, 0f);
                upper = Place(length * 0.07f, width * 0.42f);
                tip = Place(length * 0.54f, 0f);
                lower = Place(length * 0.07f, -width * 0.58f);
            }

            internal bool Overlaps(Vector2 point, float radius)
            {
                if (!active) return false;
                bool inside = InsideEdge(tail, upper, point) && InsideEdge(upper, tip, point) &&
                    InsideEdge(tip, lower, point) && InsideEdge(lower, tail, point);
                return inside || CardAttackArea.SegmentTouches(point, radius, tail, upper) ||
                    CardAttackArea.SegmentTouches(point, radius, upper, tip) ||
                    CardAttackArea.SegmentTouches(point, radius, tip, lower) ||
                    CardAttackArea.SegmentTouches(point, radius, lower, tail);
            }

            internal void WriteOutline(Vector3[] points, float z)
            {
                points[0] = new Vector3(tail.x, tail.y, z);
                points[1] = new Vector3(upper.x, upper.y, z);
                points[2] = new Vector3(tip.x, tip.y, z);
                points[3] = new Vector3(lower.x, lower.y, z);
            }

            private static bool InsideEdge(Vector2 from, Vector2 to, Vector2 point)
            {
                Vector2 edge = to - from;
                Vector2 offset = point - from;
                return edge.x * offset.y <= edge.y * offset.x;
            }
        }

        private void ReturnFromFlurry(Cast cast)
        {
            if (cast.DrivesMotion && cast.FlurryReturnPosition.HasValue)
                movement.SetCardPosition(cast.FlurryReturnPosition.Value);
            cast.FlurryReturnPosition = null;
        }

        private bool HitTarget(CombatTarget target, int points, CombatVitals vitals, bool playBodyImpact = true)
        {
            float multiplier = vitals.SandevistanActive ? CombatVitals.SandevistanDamageMultiplier : 1f;
            if (!target.Hit(points, multiplier, playBodyImpact, vitals.CounterattackBonus)) return false;
            if (!target.Alive) EnemyKilled?.Invoke();
            return true;
        }

        internal void ResolveCounterattacks(CombatVitals vitals)
        {
            while (vitals.TryTakeCounterattack(out var hit))
            {
                if (hit.Source == null || !hit.Source.Hit(hit.Points, 1f, bonusDamage: vitals.CounterattackBonus)) continue;
                if (!hit.Source.Alive) EnemyKilled?.Invoke();
            }
        }

        private static int PropDamage(int points, CombatVitals vitals) => points <= 0 ? 0 :
            (vitals.SandevistanActive ? CombatRules.ScaledPoints(points, CombatVitals.SandevistanDamageMultiplier) : points) +
            vitals.CounterattackBonus;

        private static bool HitProps(CardAttackArea area, int damage, Vector2 origin, Vector2 direction,
            HashSet<DungeonBreakableProp> hit = null)
        {
            if (damage <= 0) return false;
            bool landed = false;
            var props = DungeonBreakableProp.Active;
            for (int i = props.Count - 1; i >= 0; i--)
            {
                var prop = props[i];
                prop.GetHurtCircle(out Vector2 center, out float radius);
                if (!area.Overlaps(center, radius)) continue;
                Vector2 outward = center - origin;
                landed |= HitProp(prop, damage, outward.sqrMagnitude > 0.0001f ? outward.normalized : direction, hit);
            }
            return landed;
        }

        private static bool HitProp(DungeonBreakableProp prop, int damage, Vector2 direction,
            HashSet<DungeonBreakableProp> hit)
        {
            if (prop == null || !prop.IsIntact || damage <= 0 || hit?.Contains(prop) == true) return false;
            if (!prop.Hit(damage, direction)) return false;
            hit?.Add(prop);
            return true;
        }

        private bool HitArea(CardDefinition card, CardAttackArea area,
            int points, HashSet<CombatTarget> hitTargets, CombatVitals vitals, out bool damaged)
        {
            bool landed = false;
            damaged = false;
            bool playBodyImpact = card.Kind is not (CardKind.PhaseDash or CardKind.Starfall);
            foreach (var enemy in CombatTarget.Active.ToArray())
            {
                if (!enemy.Alive || hitTargets.Contains(enemy)) continue;
                enemy.GetHurtCircle(out Vector2 point, out float radius);
                if (!area.Overlaps(point, radius) || !HitTarget(enemy, points, vitals, playBodyImpact)) continue;

                hitTargets.Add(enemy);
                landed = true;
                damaged |= enemy.LastDamage > 0;
                switch (card.Kind)
                {
                    case CardKind.TriangleRift: enemy.Fracture(card.FractureBonus, card.EffectSeconds); break;
                    case CardKind.OrbitSpin: enemy.Slow(card.EffectSeconds); break;
                    case CardKind.Starfall: enemy.Stun(card.EffectSeconds); break;
                }
            }
            return landed;
        }

        private static int DashEffectFrame(float travelled, float travelLength, float recovery, Vector2 direction,
            ref Vector2 anchor, out float stretch)
        {
            float sourceTick = DashEffectTravelFrames * Mathf.Sqrt(Mathf.Clamp01(travelled / travelLength));
            int frame = Mathf.RoundToInt(sourceTick / DashEffectFrameStride) * DashEffectFrameStride;
            float spriteProgress = frame / (float)DashEffectTravelFrames;
            stretch = travelLength / DashEffectTravelDistance;
            anchor += direction * (travelled - travelLength * spriteProgress * spriteProgress);
            return frame + (recovery > 0f ? Mathf.FloorToInt(recovery * 60f) : 0);
        }

        private void Draw(ref SpriteRenderer layer, Sprite[] frames, int index, Vector2 anchor,
            Vector2 direction, float stretch, int order, float scale = 1f)
        {
            if (frames == null || index < 0 || index >= frames.Length)
            {
                if (layer != null) layer.enabled = false;
                return;
            }
            if (layer == null)
            {
                layer = new GameObject(order > 0 ? "Card effect front" : "Card effect back").AddComponent<SpriteRenderer>();
                layer.gameObject.layer = gameObject.layer;
                layer.sortingLayerID = playerSprite.sortingLayerID;
                layer.sortingOrder = playerSprite.sortingOrder + order;
                layer.sharedMaterial = playerSprite.sharedMaterial;
            }
            layer.sprite = frames[index];
            layer.transform.position = new Vector3(anchor.x, anchor.y, transform.position.z);
            layer.transform.localScale = new Vector3(stretch * scale, scale, 1f);
            layer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            layer.enabled = true;
        }

        private void FinishCast(Cast cast)
        {
            GameSfx.StopLoop(ref cast.SoundLoop);
            ReturnFromFlurry(cast);
            ReleaseDisplacedTargets(cast);
            cast.PairedPullOffsets.Clear();
            if (cast.DrivesMotion && movement != null) movement.EndCardMotion();
            cast.DrivesMotion = false;
            cast.Outline?.Hide();
            if (cast.Front != null) cast.Front.enabled = false;
            if (cast.Back != null) cast.Back.enabled = false;
            if (!cast.GuardTint) return;
            cast.GuardTint = false;
            if (--blockTints == 0 && playerSprite != null) playerSprite.color = savedTint;
        }

        internal void ResetMotion()
        {
            foreach (var cast in casts) FinishCast(cast);
            if (movement != null) movement.EndCardMotion();
        }

        private void OnDisable()
        {
            ResetMotion();
            afterimages?.Clear();
            flurryGround?.Clear();
        }
        private void OnDestroy()
        {
            foreach (var cast in casts)
            {
                cast.Outline?.Dispose();
                if (cast.Front != null) Destroy(cast.Front.gameObject);
                if (cast.Back != null) Destroy(cast.Back.gameObject);
            }
            flurryGround?.Dispose();
        }
    }
}
