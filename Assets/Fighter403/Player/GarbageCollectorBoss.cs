using System.Collections.Generic;
using Fighter403.Audio;
using UnityEngine;

namespace Fighter403.Player
{
    internal sealed partial class GarbageCollectorBoss : DungeonBoss
    {
        private enum Action
        {
            Roam,
            ScanWarning,
            Scan,
            ChargeWarning,
            Charge,
            WallStagger,
            StopWarning,
            Stopped
        }

        internal const string SpritePath = "Fighter403/Bosses/GC0";

        private const int Columns = 6;
        private const int Rows = 4;
        private const float RoamSpeed = 2.5f;
        private const float WallDepth = (float)DungeonRoomArt.WallFaceDepth / DungeonRoomArt.PixelsPerUnit;
        private const float TurnSpeed = 300f;
        private const float ScanWarningSeconds = 1.2f;
        private const float ScanSeconds = 1.45f;
        private const float ScanHalfWidth = 0.20f;
        private const float ScanSafeDistance = 3.7f;
        private const int ScanDamage = 15;
        private const float ChargeWarningSeconds = 0.9f;
        private const float ChargeSpeed = 24f;
        private const int ChargeDamage = 20;
        private const float WallStaggerSeconds = 1.5f;
        private const float StopInterval = 20f;
        private const float StopWarningSeconds = 0.8f;
        private const int FragmentHealth = 20;
        private const float FragmentRadius = 0.35f;
        private const float FragmentSpeed = 2.85f;
        private const float ConsumeFlashSeconds = 0.35f;

        private static readonly Color GridTint = new(0.27f, 0.39f, 0.53f, 0.44f);
        private static readonly Color ScanTint = new(1f, 0.40f, 0.15f);
        private static readonly Color ScanBeamTint = new(1f, 0.82f, 0.40f);
        private static readonly Color ChargeTint = new(1f, 0.49f, 0.20f);
        private static readonly Color StopTint = new(0.55f, 0.86f, 1f);

        private readonly bool[,] markedCells = new bool[Columns, Rows];
        private readonly List<DungeonBossSummon> fragments = new();
        private readonly List<GameObject> actionMarks = new();
        private readonly List<Vector2Int> reachableSafeZones = new();
        private Vector2 floorHalfSize;
        private Vector2 lowerCorner;
        private Vector2 cellSize;
        private Vector2 roamTarget;
        private Vector2 chargeDirection;
        private Vector2 chargeDestination;
        private Action action;
        private float remaining;
        private float timeToStop;
        private float consumeFlashRemaining;
        private float lastScanX;
        private float lastScanY;
        private int roamIndex;
        private int patternIndex;
        private int horizontalSign;
        private int verticalSign;
        private bool phaseTwo;
        private bool crossScan;
        private bool hitDuringAction;

        public override string DisplayName => "清道夫 GC-0";
        internal bool StopWarningActive => Alive && action == Action.StopWarning;
        internal override bool Anchored => action is Action.ChargeWarning or Action.Charge or Action.WallStagger
            or Action.StopWarning or Action.Stopped;
        protected override string ArtResource => SpritePath;
        protected override int StartingHealth => 700;
        protected override float HurtRadius => 1.55f;

        protected override Color StateColor
        {
            get
            {
                if (consumeFlashRemaining > 0f)
                    return Color.Lerp(Color.white, new Color(0.63f, 1f, 0.76f),
                        Mathf.Clamp01(consumeFlashRemaining / ConsumeFlashSeconds));
                if (action == Action.Stopped) return new Color(0.74f, 0.91f, 1f);
                if (action == Action.WallStagger) return new Color(1f, 0.78f, 0.52f);
                if (action is Action.ScanWarning or Action.ChargeWarning or Action.StopWarning)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(remaining * 27f);
                    return Color.Lerp(Color.white, action == Action.StopWarning ? StopTint : ScanBeamTint, pulse * 0.62f);
                }

                return phaseTwo ? new Color(1f, 0.91f, 0.82f) : Color.white;
            }
        }

        protected override void BeginEncounter()
        {
            actionMarks.Clear();
            fragments.Clear();
            phaseTwo = false;
            roamIndex = Random.Next(4);
            patternIndex = 0;
            timeToStop = StopInterval;
            consumeFlashRemaining = 0f;
            floorHalfSize = ArenaHalfSize - Vector2.one * WallDepth;
            lowerCorner = ArenaCenter - floorHalfSize;
            cellSize = new Vector2(floorHalfSize.x * 2f / Columns, floorHalfSize.y * 2f / Rows);
            DrawCacheGrid();
            EnterRoam(1.2f);
            PlayEffect(Effect.Entrance, transform.position, 1.05f);
        }

        protected override void TickEncounter(float dt)
        {
            if (Stunned) return;
            if (!phaseTwo && Health < MaxHealth / 2)
            {
                phaseTwo = true;
                PlayEffect(Effect.PhaseShift, transform.position, .94f);
                Announce("分代回收");
                GameSfx.Play(Sfx.BossPhase);
                ShotCamera?.Shake(0.23f, 0.28f);
            }

            UpdateConsumeFlash(dt);
            timeToStop -= dt;
            remaining -= dt;

            // Start the HUD warning just before the fixed combat interval.
            if (timeToStop <= StopWarningSeconds && action != Action.StopWarning && action != Action.Stopped)
            {
                BeginStopWarning();
                return;
            }

            if (action != Action.Stopped) UpdateFragments(dt);

            switch (action)
            {
                case Action.Roam:
                    Roam(dt);
                    if (remaining <= 0f) BeginNextAttack();
                    break;
                case Action.ScanWarning:
                    if (remaining <= 0f) BeginScan();
                    break;
                case Action.Scan:
                    UpdateScan();
                    if (remaining <= 0f) EnterRoam(1.25f);
                    break;
                case Action.ChargeWarning:
                    TurnTowards(chargeDirection, dt);
                    if (remaining <= 0f) BeginCharge();
                    break;
                case Action.Charge:
                    UpdateCharge(dt);
                    break;
                case Action.WallStagger:
                    if (remaining <= 0f) EnterRoam(0.9f);
                    break;
                case Action.StopWarning:
                    if (remaining <= 0f) BeginStop();
                    break;
                case Action.Stopped:
                    if (remaining <= 0f) EnterRoam(1.1f);
                    break;
            }
            AnimateActionEffects(dt);
        }

        protected override void OnBossDefeated() => PlayEffect(Effect.Defeat, transform.position, 1.3f);

        protected override void OnEncounterStopped()
        {
            action = Action.Roam;
            actionMarks.Clear();
            fragments.Clear();
            ClearActionEffects();
            ClearTransientEffects(keepDefeat: !Alive);
        }

        protected override int MitigateDamage(int amount)
        {
            float multiplier = action switch
            {
                Action.WallStagger => 1.30f,
                Action.Stopped => 1.50f,
                _ => 1f
            };
            return CombatRules.ScaledPoints(amount, multiplier);
        }

        private void EnterRoam(float delay)
        {
            if (action == Action.Stopped) SetFragmentPositionLocks(false);
            ClearActionMarks();
            action = Action.Roam;
            remaining = delay;
            NextRoamTarget();
        }

        private void NextRoamTarget()
        {
            float x = (roamIndex == 0 || roamIndex == 3 ? -1f : 1f) * ArenaHalfSize.x * 0.55f;
            float y = (roamIndex >= 2 ? -1f : 1f) * ArenaHalfSize.y * 0.48f;
            roamTarget = ArenaCenter + new Vector2(x, y);
            roamIndex = (roamIndex + 1) % 4;
        }

        private void Roam(float dt)
        {
            if (BeingPulled) return;
            Vector2 center = transform.position;
            if ((roamTarget - center).sqrMagnitude < 0.04f) NextRoamTarget();
            TurnTowards(roamTarget - center, dt);
            transform.position = Vector2.MoveTowards(center, roamTarget, RoamSpeed * SpeedMultiplier * dt);
        }

        private void TurnTowards(Vector2 direction, float dt)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float nextAngle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, angle, TurnSpeed * dt);
            transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
        }

        private void BeginNextAttack()
        {
            int next = patternIndex % 4;
            float leadTime = timeToStop - StopWarningSeconds;
            if (next == 2)
            {
                // Leave room for the entire windup, charge, and vulnerability window.
                Vector2 center = transform.position;
                Vector2 direction = GetChargeDirection(center);
                float travel = Vector2.Distance(center, FindWall(center, direction));
                float chargeSeconds = travel / (ChargeSpeed * 0.6f);
                if (leadTime < ChargeWarningSeconds + chargeSeconds + WallStaggerSeconds + 0.04f) return;
            }
            else if (next != 1 && leadTime < ScanWarningSeconds + ScanSeconds + 0.04f) return;

            ++patternIndex;
            switch (next)
            {
                case 0:
                    BeginScanWarning();
                    break;
                case 1:
                    SpawnFragments();
                    remaining = 1.6f;
                    break;
                case 2:
                    BeginChargeWarning();
                    break;
                default:
                    BeginScanWarning();
                    break;
            }
        }

        private void DrawCacheGrid()
        {
            Vector2 inset = Vector2.one * 0.045f;
            for (int x = 0; x < Columns; ++x)
                for (int y = 0; y < Rows; ++y)
                {
                    Vector2 cell = lowerCorner + Vector2.Scale(new Vector2(x, y), cellSize);
                    Telegraph("缓存格", Rectangle(cell + inset, cell + cellSize - inset), GridTint, 0f);
                }
        }

        private void BeginScanWarning()
        {
            action = Action.ScanWarning;
            remaining = ScanWarningSeconds;
            crossScan = phaseTwo;
            horizontalSign = Random.Next(2) == 0 ? 1 : -1;
            verticalSign = Random.Next(2) == 0 ? 1 : -1;
            Vector2Int safeZone = ChooseSafeZone(crossScan);
            Vector2 inset = Vector2.one * 0.095f;

            for (int x = 0; x < Columns; ++x)
                for (int y = 0; y < Rows; ++y)
                {
                    bool safe = crossScan
                        ? (x == safeZone.x || x == safeZone.x + 1) && y == safeZone.y
                        : x == safeZone.x;
                    markedCells[x, y] = !safe;
                    if (safe) continue;
                    Vector2 cell = lowerCorner + Vector2.Scale(new Vector2(x, y), cellSize);
                    actionMarks.Add(Telegraph("标记清扫", Rectangle(cell + inset, cell + cellSize - inset), ScanTint, 0.09f));
                    AddScanCellEffect(cell + cellSize * .5f, x, y);
                }

            GameSfx.Play(Sfx.BossMeteorWarn);
        }

        private Vector2Int ChooseSafeZone(bool paired)
        {
            reachableSafeZones.Clear();
            Vector2 position = Player.Position;
            int candidatesX = paired ? Columns - 1 : Columns;
            int candidatesY = paired ? Rows : 1;
            Vector2 pad = Vector2.one * 0.3f;
            Vector2 size = paired ? new Vector2(cellSize.x * 2f, cellSize.y) : new Vector2(cellSize.x, floorHalfSize.y * 2f);

            for (int x = 0; x < candidatesX; ++x)
                for (int y = 0; y < candidatesY; ++y)
                {
                    Vector2 min = lowerCorner + Vector2.Scale(new Vector2(x, y), cellSize) + pad;
                    Vector2 max = min + size - pad * 2f;
                    Vector2 nearest = new Vector2(Mathf.Clamp(position.x, min.x, max.x),
                        Mathf.Clamp(position.y, min.y, max.y));
                    float distanceSquared = (position - nearest).sqrMagnitude;
                    if (distanceSquared <= ScanSafeDistance * ScanSafeDistance) reachableSafeZones.Add(new Vector2Int(x, y));
                }

            if (reachableSafeZones.Count > 0) return reachableSafeZones[Random.Next(reachableSafeZones.Count)];
            Vector2 relative = position - lowerCorner;
            return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(relative.x / cellSize.x), 0, candidatesX - 1),
                Mathf.Clamp(Mathf.FloorToInt(relative.y / cellSize.y), 0, candidatesY - 1));
        }

        private void BeginScan()
        {
            action = Action.Scan;
            remaining = ScanSeconds;
            hitDuringAction = false;
            lastScanX = ArenaCenter.x - horizontalSign * floorHalfSize.x;
            lastScanY = ArenaCenter.y - verticalSign * floorHalfSize.y;
            BeginSweepEffects();
            GameSfx.Play(Sfx.BossRainPass);
        }

        private void UpdateScan()
        {
            float progress = 2f * Mathf.Clamp01(1f - remaining / ScanSeconds) - 1f;
            float x = ArenaCenter.x + horizontalSign * floorHalfSize.x * progress;
            float y = ArenaCenter.y + verticalSign * floorHalfSize.y * progress;
            MoveSweepEffects(x, y);

            Vector2 position = Player.Position;
            if (!hitDuringAction && IsMarked(position))
            {
                bool hitVertical = IsInsideSweep(position.x, lastScanX, x);
                bool hitHorizontal = crossScan && IsInsideSweep(position.y, lastScanY, y);
                if (hitVertical || hitHorizontal)
                {
                    hitDuringAction = true;
                    Vector2 direction = hitVertical ? new Vector2(horizontalSign, 0f) : new Vector2(0f, verticalSign);
                    Vitals.TakeDamageFrom(ScanDamage, direction, Sfx.BossRainHit, this);
                    PlayEffect(Effect.ScanHit, position, .34f, AngleOf(direction));
                }
            }

            lastScanX = x;
            lastScanY = y;
        }

        private bool IsMarked(Vector2 position)
        {
            Vector2 relative = position - lowerCorner;
            int x = Mathf.Clamp(Mathf.FloorToInt(relative.x / cellSize.x), 0, Columns - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(relative.y / cellSize.y), 0, Rows - 1);
            return markedCells[x, y];
        }

        private static bool IsInsideSweep(float coordinate, float start, float end) =>
            coordinate >= Mathf.Min(start, end) - ScanHalfWidth && coordinate <= Mathf.Max(start, end) + ScanHalfWidth;

        private void BeginChargeWarning()
        {
            action = Action.ChargeWarning;
            remaining = ChargeWarningSeconds;
            Vector2 center = transform.position;
            chargeDirection = GetChargeDirection(center);
            chargeDestination = FindWall(center, chargeDirection);
            actionMarks.Add(Telegraph("回收冲撞",
                ChargeFootprint(center, chargeDestination, chargeDirection, HurtRadius), ChargeTint, .14f));
            AddChargeChevrons(center);
            GameSfx.Play(Sfx.EnemyWindup);
        }

        private static Vector2[] ChargeFootprint(Vector2 start, Vector2 end, Vector2 direction, float radius)
        {
            const int arcSegments = 8;
            var points = new Vector2[(arcSegments + 1) * 2];
            Vector2 perpendicular = new(-direction.y, direction.x);
            for (int i = 0; i <= arcSegments; ++i)
            {
                float angle = Mathf.PI * (i / (float)arcSegments - 0.5f);
                Vector2 offset = radius * (direction * Mathf.Cos(angle) + perpendicular * Mathf.Sin(angle));
                points[i] = end + offset;
                points[i + arcSegments + 1] = start - offset;
            }

            return points;
        }

        private Vector2 GetChargeDirection(Vector2 center)
        {
            Vector2 direction = Player.Position - center;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : (Vector2)transform.up;
        }

        private Vector2 FindWall(Vector2 center, Vector2 direction)
        {
            Vector2 limit = floorHalfSize - Vector2.one * HurtRadius;
            Vector2 relative = center - ArenaCenter;
            float x = WallDistance(relative.x, direction.x, limit.x);
            float y = WallDistance(relative.y, direction.y, limit.y);
            return center + direction * Mathf.Max(0f, Mathf.Min(x, y));
        }

        private static float WallDistance(float start, float direction, float limit)
        {
            if (Mathf.Abs(direction) < 0.0001f) return float.PositiveInfinity;
            return (Mathf.Sign(direction) * limit - start) / direction;
        }

        private void BeginCharge()
        {
            action = Action.Charge;
            hitDuringAction = false;
            ClearActionEffects();
            var thrust = AddActionEffect(Effect.ChargeThrusters, transform.position, -1, AngleOf(chargeDirection));
            thrust.Follow = transform;
            GameSfx.Play(Sfx.DashStart);
        }

        private void UpdateCharge(float dt)
        {
            Vector2 previous = transform.position;
            Vector2 center = Vector2.MoveTowards(previous, chargeDestination, ChargeSpeed * SpeedMultiplier * dt);
            transform.position = center;

            if (!hitDuringAction && !Player.DashInProgress)
            {
                Vector2 travel = center - previous;
                float distanceSquared = travel.sqrMagnitude;
                float t = distanceSquared > 0.0001f
                    ? Mathf.Clamp01(Vector2.Dot(Player.Position - previous, travel) / distanceSquared)
                    : 0f;
                float combinedRadius = HurtRadius + Player.CombatRadius;
                if ((Player.Position - previous - travel * t).sqrMagnitude <= combinedRadius * combinedRadius)
                {
                    Vitals.TakeDamageFrom(ChargeDamage, chargeDirection, source: this);
                    PlayEffect(Effect.ChargeHit, Player.Position, .5f, AngleOf(chargeDirection));
                    hitDuringAction = true;
                }
            }

            if (center != chargeDestination) return;
            ClearActionMarks();
            action = Action.WallStagger;
            remaining = WallStaggerSeconds;
            PlayEffect(Effect.WallImpact, center + chargeDirection * HurtRadius, .72f, AngleOf(chargeDirection));
            AddActionEffect(Effect.WallStagger, center, 3);
            GameSfx.Play(Sfx.DashStop);
            ShotCamera?.Shake(0.30f, 0.24f);
        }

        private void SpawnFragments()
        {
            int count = phaseTwo ? 4 : 3;
            int firstSide = Random.Next(4);
            for (int i = 0; i < count; ++i)
            {
                int side = (firstSide + i) % 4;
                float along = Mathf.Lerp(-0.78f, 0.78f, (float)Random.NextDouble());
                Vector2 offset = side switch
                {
                    0 => new Vector2(-floorHalfSize.x + 0.6f, along * floorHalfSize.y),
                    1 => new Vector2(along * floorHalfSize.x, floorHalfSize.y - 0.6f),
                    2 => new Vector2(floorHalfSize.x - 0.6f, along * floorHalfSize.y),
                    _ => new Vector2(along * floorHalfSize.x, -floorHalfSize.y + 0.6f)
                };
                DungeonBossSummon fragment = SpawnSummon("缓存碎片", SwarmSprite, ArenaCenter + offset,
                    FragmentHealth, FragmentRadius);
                if (fragment != null)
                {
                    fragments.Add(fragment);
                    Vector2 edge = side is 0 or 2 ? Vector2.up : Vector2.right;
                    PlayEffect(Effect.FragmentSpawn, fragment.transform.position, .61f, AngleOf(edge));
                    TrackFragmentEffect(fragment);
                }
            }

            GameSfx.Play(Sfx.FragmentSpawn);
        }

        private void UpdateFragments(float dt)
        {
            Vector2 center = transform.position;
            float consumeDistance = HurtRadius + FragmentRadius;
            for (int i = fragments.Count - 1; i >= 0; --i)
            {
                DungeonBossSummon fragment = fragments[i];
                if (fragment == null || !fragment.Alive)
                {
                    fragments.RemoveAt(i);
                    continue;
                }

                if (fragment.Stunned || fragment.BeingPulled) continue;

                Vector2 position = Vector2.MoveTowards(fragment.transform.position, center, FragmentSpeed * dt);
                fragment.transform.position = position;
                if ((position - center).sqrMagnitude > consumeDistance * consumeDistance) continue;
                fragments.RemoveAt(i);
                ForgetFragmentEffect(fragment);
                fragment.Dismiss();
                Heal(phaseTwo ? 50 : 40);
                ShowConsumeFlash(center, position - center);
            }
        }

        private void ShowConsumeFlash(Vector2 center, Vector2 fromFragment)
        {
            PlayEffect(Effect.FragmentConsume, center, ConsumeFlashSeconds + .16f, AngleOf(fromFragment), follow: transform);
            consumeFlashRemaining = ConsumeFlashSeconds;
            GameSfx.Play(Sfx.ProtocolHeal, minimumInterval: 0.12f);
        }

        private void UpdateConsumeFlash(float dt)
        {
            consumeFlashRemaining = Mathf.Max(0f, consumeFlashRemaining - dt);
        }

        private void BeginStopWarning()
        {
            ClearActionMarks();
            action = Action.StopWarning;
            remaining = StopWarningSeconds;
            AddActionEffect(Effect.StopWarning, transform.position, -1);
            Announce("Stop-the-World");
            GameSfx.Play(Sfx.BossSealWarn);
        }

        private void BeginStop()
        {
            ClearActionMarks();
            action = Action.Stopped;
            SetFragmentPositionLocks(true);
            remaining = phaseTwo ? 2.5f : 3f;
            timeToStop += StopInterval;
            PlayEffect(Effect.StopWave, transform.position, .67f);
            AddActionEffect(Effect.StopLock, transform.position, 3);
            foreach (DungeonBossSummon fragment in fragments)
            {
                if (fragment == null || !fragment.Alive) continue;
                var marker = AddActionEffect(Effect.FragmentLock, fragment.transform.position, 3);
                marker.Follow = fragment.transform;
            }
            GameSfx.Play(Sfx.BossSealRelease);
        }

        private void SetFragmentPositionLocks(bool locked)
        {
            foreach (DungeonBossSummon fragment in fragments)
                if (fragment != null && fragment.Alive) fragment.PositionLocked = locked;
        }

        private void ClearActionMarks()
        {
            foreach (GameObject mark in actionMarks)
                RemoveTelegraph(mark);
            actionMarks.Clear();
            ClearActionEffects();
        }

        private static Vector2[] Rectangle(Vector2 min, Vector2 max) =>
            new[] { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) };

    }
}
