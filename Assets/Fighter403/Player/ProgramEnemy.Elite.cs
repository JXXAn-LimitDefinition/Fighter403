using System.Collections.Generic;
using Fighter403.UI;
using UnityEngine;
using Fighter403.Audio;

namespace Fighter403.Player
{
    public sealed partial class ProgramEnemy
    {
        private const float ClusterDistance = 3f, ResonanceDuration = 3f;
        private const float EchoDelay = 1f, EchoFlash = 0.23f, PulseDuration = 0.48f;
        private static readonly Color EchoColor = new Color32(255, 118, 159, 255);
        private static readonly Color LinkColor = new Color32(101, 236, 250, 255);
        private static readonly Color ResonanceColor = new Color32(255, 181, 74, 255);
        private static readonly Color HealColor = new Color32(130, 255, 169, 255);
        [SerializeField] private ProgramEnemy splitPrefab;

        private float resonanceTime, healTime, pulseAge = PulseDuration;
        private float resonanceOne, resonanceTwo, resonanceThree;
        private bool pendingSplit, drawsClusterLink, clusterWasLinked, clusterSurged;
        private int splitGeneration;
        private SplitBudget splitBudget;
        private ProgramEnemy partner;
        private LineRenderer eliteLink;
        private SpriteRenderer elitePulse;
        private readonly List<ProgramEnemy> splitChildren = new(2);
        private readonly List<Echo> echoes = new();

        private sealed class SplitBudget
        {
            internal int Remaining = 2;
        }

        private sealed class Echo
        {
            internal CardAttackArea Area;
            internal CardCombat.FlurryBladeArea Blade, Cross;
            internal CardAimPreview Preview, CrossPreview;
            internal int Damage;
            internal float Age;
            internal Vector2 Direction;
            internal bool IsFlurry => CrossPreview != null;

            internal void Dispose()
            {
                Preview.Dispose();
                CrossPreview?.Dispose();
            }
        }

        private bool HasShield => role is EnemyRole.Shield or EnemyRole.Protocol;
        internal bool IsElite => role >= EnemyRole.Resonator;
        private bool ClusterLinked => role == EnemyRole.Cluster && partner != null && partner.Alive &&
            ((Vector2)(partner.transform.position - transform.position)).sqrMagnitude <= ClusterDistance * ClusterDistance;

        private int ResonanceStacks => (resonanceOne > resonanceTime ? 1 : 0) +
            (resonanceTwo > resonanceTime ? 1 : 0) + (resonanceThree > resonanceTime ? 1 : 0);

        private void DealDamage(int amount, Vector2 direction, Sfx healthImpact = Sfx.PlayerHurt) =>
            vitals.TakeDamageFrom(CombatRules.ScaledPoints(amount, 1f + 0.25f * ResonanceStacks), direction, healthImpact, this);

        private void ReceiveResonance()
        {
            float until = resonanceTime + ResonanceDuration;
            if (resonanceOne <= resonanceTwo && resonanceOne <= resonanceThree) resonanceOne = until;
            else if (resonanceTwo <= resonanceThree) resonanceTwo = until;
            else resonanceThree = until;
            pulseAge = 0f;
        }

        private bool IsAlly(CombatTarget target) =>
            vitals != null && target != this && target is ProgramEnemy other && other.vitals == vitals && other.Alive;

        protected override void OnHitResolved(int damage)
        {
            if (role == EnemyRole.Resonator && damage > 0)
            {
                GameSfx.Play(Sfx.Resonance);
                pulseAge = 0f;
                foreach (var target in Active)
                    if (IsAlly(target)) ((ProgramEnemy)target).ReceiveResonance();
            }
            if (role == EnemyRole.Splitter && Alive && damage >= 20 && splitBudget?.Remaining > 0)
                pendingSplit = true;
        }

        private void StepElite(float dt)
        {
            resonanceTime += dt;
            pulseAge += dt;
            switch (role)
            {
                case EnemyRole.Mirror:
                    if (Stunned || BeingPulled) ClearEchoes();
                    else StepEchoes(dt);
                    break;
                case EnemyRole.Cluster:
                    if (partner != null)
                    {
                        bool linked = ClusterLinked;
                        if (drawsClusterLink && linked != clusterWasLinked)
                            GameSfx.Play(linked ? Sfx.ClusterLink : Sfx.ClusterBreak);
                        if (!partner.Alive && !clusterSurged)
                        {
                            clusterSurged = true;
                            if (!drawsClusterLink && clusterWasLinked) GameSfx.Play(Sfx.ClusterBreak);
                            GameSfx.Play(Sfx.ClusterSurge);
                        }
                        clusterWasLinked = linked;
                        break;
                    }
                    foreach (var target in Active)
                    {
                        if (!IsAlly(target) || target is not ProgramEnemy { role: EnemyRole.Cluster, partner: null } other)
                            continue;
                        partner = other;
                        other.partner = this;
                        drawsClusterLink = true;
                        other.drawsClusterLink = false;
                        break;
                    }
                    break;
                case EnemyRole.Splitter:
                    if (pendingSplit && !BeingPulled) Split();
                    break;
                case EnemyRole.Protocol:
                    if (shield <= 0) break;
                    healTime += dt;
                    if (healTime < 1f) break;
                    healTime -= 1f;
                    foreach (var target in Active)
                    {
                        if (!IsAlly(target) || target.Heal(3) == 0) continue;
                        ((ProgramEnemy)target).pulseAge = pulseAge = 0f;
                        GameSfx.Play(Sfx.ProtocolHeal);
                    }
                    break;
            }
        }

        internal static void CopyPlayerAttack(CombatVitals battle, CardDefinition card, int points)
        {
            if (points <= 0 || card.HitShape == CardHitShape.None) return;
            if (battle.SandevistanActive) points = CombatRules.ScaledPoints(points, CombatVitals.SandevistanDamageMultiplier);
            foreach (var target in Active)
                if (target is ProgramEnemy { role: EnemyRole.Mirror } mirror && mirror.Alive &&
                    mirror.vitals == battle && !mirror.Stunned && !mirror.BeingPulled)
                    mirror.QueueEcho(card, Mathf.Max(1, CombatRules.ScaledPoints(points, 0.5f)));
        }

        private void QueueEcho(CardDefinition card, int damage)
        {
            Vector2 origin = transform.position;
            Vector2 aim = player.Position;
            Vector2 direction = aim == origin ? facing : (aim - origin).normalized;
            var echo = new Echo { Damage = damage, Direction = direction, Preview = CreateEchoPreview() };
            if (card.Kind == CardKind.ThousandSlashes && card.FinisherEffect.Length > 0 && card.FinisherEffect[0] != null)
            {
                echo.CrossPreview = CreateEchoPreview();
                CardCombat.GetFinisherBlades(aim, direction, Vector2.one / card.FinisherEffect[0].pixelsPerUnit,
                    extend: 0f, fade: 0f, out echo.Blade, out echo.Cross);
            }
            else
            {
                Vector2? end = null;
                if (card.HitShape == CardHitShape.Dash)
                {
                    float distance = card.Distance;
                    Physics2D.SyncTransforms();
                    Physics2D.CircleCast(origin, card.DashHalfWidth, direction, solidFilter, sweepHits, distance);
                    foreach (var hit in sweepHits)
                        if (hit.collider != null && !hit.collider.transform.IsChildOf(player.transform) &&
                            hit.collider.GetComponent<ProgramEnemy>() == null)
                            distance = Mathf.Min(distance, hit.distance);
                    if (distance <= 0.001f)
                    {
                        echo.Dispose();
                        return;
                    }
                    end = origin + direction * distance;
                }
                echo.Area = card.HitShape == CardHitShape.Lock
                    ? new CardAttackArea(aim, 0.55f)
                    : new CardAttackArea(card, card.HitShape == CardHitShape.Landing ? aim : origin, direction, end);
            }
            echoes.Add(echo);
            GameSfx.Play(Sfx.MirrorCopy);
            GameSfx.Play(Sfx.MirrorWarn, 0.75f);
            cycle.Reset();
            phaseAge = 0f;
            ShowEcho(echo);
        }

        private CardAimPreview CreateEchoPreview() => new(display.sortingLayerID, display.sortingOrder + 3);

        private void ShowEcho(Echo echo)
        {
            bool released = echo.Age >= EchoDelay;
            float power = Mathf.Clamp01(echo.Age / EchoDelay);
            float flash = 1f - Mathf.Clamp01((echo.Age - EchoDelay) / EchoFlash);
            Color color = released ? Color.Lerp(EchoColor, Color.white, flash) : EchoColor;
            color.a = released ? flash : Mathf.Lerp(0.55f, 1f, power);
            float width = released ? Mathf.Lerp(0.025f, 0.1f, flash) : Mathf.Lerp(0.028f, 0.066f, power);
            float z = transform.position.z;

            if (echo.IsFlurry)
            {
                echo.Preview.ShowArea(echo.Blade, z, color, width);
                echo.CrossPreview.ShowArea(echo.Cross, z, color, width);
            }
            else echo.Preview.ShowArea(echo.Area, z, color, width);
        }

        private void StepEchoes(float dt)
        {
            if (echoes.Count > 0) TurnToward(echoes[0].Direction, dt);
            for (int i = 0; i < echoes.Count;)
            {
                Echo echo = echoes[i];
                float previous = echo.Age;
                echo.Age += dt;
                if (previous < EchoDelay && echo.Age >= EchoDelay)
                {
                    GameSfx.Play(Sfx.MirrorAttack);
                    pulseAge = 0f;
                    var playerCollider = player.GetComponent<CircleCollider2D>();
                    Vector2 center = player.transform.TransformPoint(playerCollider.offset);
                    Vector3 scale = player.transform.lossyScale;
                    float radius = playerCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    bool hit = echo.IsFlurry ? echo.Blade.Overlaps(center, radius) || echo.Cross.Overlaps(center, radius)
                        : echo.Area.Overlaps(center, radius);
                    if (hit) DealDamage(echo.Damage, echo.Direction);
                }
                if (echo.Age >= EchoDelay + EchoFlash)
                {
                    echo.Dispose();
                    echoes.RemoveAt(i);
                    continue;
                }
                ShowEcho(echo);
                i++;
            }
        }

        private void ClearEchoes()
        {
            foreach (Echo echo in echoes) echo.Dispose();
            echoes.Clear();
        }

        private void Split()
        {
            pendingSplit = false;
            if (splitPrefab == null || splitBudget?.Remaining <= 0) return;
            Physics2D.SyncTransforms();
            float radius = splitPrefab.GetComponent<CircleCollider2D>().radius;
            float distance = hurtbox.radius + radius + 0.08f;
            Vector2 origin = transform.position;
            var available = new List<Vector2>(16);
            for (int i = 0; i < 16; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, 22.5f * i) * (Vector3)facing;
                if (Sweep(origin, radius, direction, distance, out _, ignoreEnemies: false)) continue;
                available.Add(origin + direction * distance);
            }

            Vector2 first = default, second = default;
            float minimumSeparation = 2f * radius + 0.08f;
            float bestSeparation = minimumSeparation * minimumSeparation;
            bool foundPair = false;
            for (int i = 0; i < available.Count - 1; i++)
                for (int j = i + 1; j < available.Count; j++)
                {
                    float separation = (available[i] - available[j]).sqrMagnitude;
                    if (separation <= bestSeparation + 0.0001f) continue;
                    (first, second) = (available[i], available[j]);
                    bestSeparation = separation;
                    foundPair = true;
                }
            if (!foundPair)
            {
                // Keep the split armed while the enemy is wedged against a wall or another body.
                pendingSplit = true;
                return;
            }

            splitBudget.Remaining--;
            GameSfx.Play(Sfx.EnemySplit);
            Retire();
            SpawnFragment(first);
            SpawnFragment(second);
            GameSfx.Play(Sfx.FragmentSpawn);
        }

        private void SpawnFragment(Vector2 point)
        {
            var child = Instantiate(splitPrefab, new Vector3(point.x, point.y, transform.position.z), Rotation(facing));
            child.splitGeneration = splitGeneration + 1;
            child.splitPrefab = splitPrefab;
            child.splitBudget = splitBudget;
            child.Initialize(player, vitals, solidFilter.layerMask);
            child.pulseAge = 0f;
            splitChildren.Add(child);
        }

        private void UpdateEliteVisuals()
        {
            if (IsElite && frames != null)
            {
                if (elitePulse == null && pulseAge < PulseDuration)
                {
                    elitePulse = new GameObject("Elite pulse").AddComponent<SpriteRenderer>();
                    elitePulse.transform.SetParent(transform, false);
                    CopySorting(elitePulse);
                }
                if (elitePulse != null)
                {
                    elitePulse.enabled = pulseAge < PulseDuration;
                    if (elitePulse.enabled)
                        elitePulse.sprite = frames[frames.GetLength(0) - 1, FrameAt(pulseAge, PulseDuration)];
                }
            }

            Color color = role == EnemyRole.Protocol ? HealColor : ResonanceColor;
            if (ClusterLinked && drawsClusterLink)
            {
                DrawLink(partner.transform.position, LinkColor);
                return;
            }
            if (eliteLink != null) eliteLink.enabled = false;
            if (!IsElite && pulseAge < PulseDuration)
                overhead.SetPulse(ResonanceStacks > 0 ? color : HealColor, 1f - pulseAge / PulseDuration);
            else overhead.SetPulse(Color.clear, 0f);
        }

        private void DrawLink(Vector3 destination, Color color)
        {
            if (eliteLink == null)
            {
                eliteLink = new GameObject("Cluster link").AddComponent<LineRenderer>();
                eliteLink.transform.SetParent(transform, false);
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    warningMaterial = new Material(shader);
                    eliteLink.sharedMaterial = warningMaterial;
                }
                else eliteLink.sharedMaterial = display.sharedMaterial;
                eliteLink.sortingLayerID = display.sortingLayerID;
                eliteLink.sortingOrder = display.sortingOrder - 1;
                eliteLink.positionCount = 3;
            }
            eliteLink.enabled = true;
            Vector3 start = transform.position;
            Vector3 delta = destination - start;
            eliteLink.SetPosition(0, start);
            eliteLink.SetPosition(1, (start + destination) * 0.5f + new Vector3(-delta.y, delta.x) *
                (0.1f * Mathf.Sin(idleAge * 9f)));
            eliteLink.SetPosition(2, destination);
            eliteLink.startColor = eliteLink.endColor = color;
            eliteLink.widthMultiplier = 0.032f;
        }

        private void ResetElite()
        {
            ClearEchoes();
            ClearSplitChildren();
            if (splitGeneration == 0 && role == EnemyRole.Splitter) splitBudget = new SplitBudget();
            pendingSplit = clusterWasLinked = clusterSurged = false;
            healTime = resonanceTime = resonanceOne = resonanceTwo = resonanceThree = 0f;
            pulseAge = PulseDuration;
            if (elitePulse != null) elitePulse.enabled = false;
            if (eliteLink != null) eliteLink.enabled = false;
        }

        private void ClearSplitChildren()
        {
            foreach (var child in splitChildren)
            {
                if (child == null) continue;
                child.ClearSplitChildren();
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            splitChildren.Clear();
        }

        internal void RemoveSpawnedFragments() => ClearSplitChildren();
    }
}
