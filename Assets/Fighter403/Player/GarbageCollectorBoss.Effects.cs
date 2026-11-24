using System.Collections.Generic;
using UnityEngine;

namespace Fighter403.Player
{
    internal sealed partial class GarbageCollectorBoss
    {
        private enum Effect
        {
            Entrance, ScanCell, ScanBeam, ScanIntersection, ScanHit, ChargeChevron, ChargeThrusters,
            ChargeHit, WallImpact, WallStagger, FragmentSpawn, FragmentConsume, FragmentBreak,
            StopWarning, StopWave, StopLock, FragmentLock, PhaseShift, Defeat
        }

        private static readonly Vector3 PixelScale = Vector3.one / 24f;
        private static readonly Color RoomMarkWhite = new(1f, 1f, 1f, .93f);
        private readonly Dictionary<Effect, Sprite[]> effectFrames = new();
        private readonly List<EffectTrack> currentEffects = new();
        private readonly List<EffectTrack> transientEffects = new();
        private readonly List<FragmentTrack> fragmentEffects = new();
        private EffectTrack verticalSweep, horizontalSweep, sweepIntersection;
        private float chargeAge;

        private sealed class EffectTrack
        {
            internal SpriteRenderer Renderer;
            internal Sprite[] Frames;
            internal Effect Kind;
            internal Transform Follow;
            internal float Age, Delay, Duration;

            internal void ShowFrame(float progress)
            {
                if (Renderer == null || Frames == null) return;
                if (Follow != null) Renderer.transform.position = Follow.position;
                int frame = Mathf.Min(Frames.Length - 1, Mathf.FloorToInt(Mathf.Clamp01(progress) * Frames.Length));
                Renderer.sprite = Frames[frame];
            }

            internal void Remove()
            {
                if (Renderer == null) return;
                Renderer.gameObject.SetActive(false);
                Object.Destroy(Renderer.gameObject);
            }
        }

        private sealed class FragmentTrack
        {
            internal DungeonBossSummon Summon;
            internal Vector2 LastPosition;
        }

        private static (int width, int height, int frameCount) SheetLayout(Effect effect) => effect switch
        {
            Effect.Entrance => (160, 160, 20),
            Effect.ScanCell => (120, 120, 12),
            Effect.ScanBeam => (28, 64, 12),
            Effect.ScanIntersection => (72, 72, 12),
            Effect.ScanHit => (88, 88, 12),
            Effect.ChargeChevron => (56, 72, 12),
            Effect.ChargeThrusters => (144, 112, 12),
            Effect.ChargeHit => (122, 96, 13),
            Effect.WallImpact => (180, 180, 18),
            Effect.WallStagger => (120, 120, 14),
            Effect.FragmentSpawn => (88, 72, 16),
            Effect.FragmentConsume => (138, 138, 18),
            Effect.FragmentBreak => (86, 86, 14),
            Effect.StopWarning => (158, 158, 16),
            Effect.StopWave => (192, 192, 18),
            Effect.StopLock => (140, 140, 16),
            Effect.FragmentLock => (34, 34, 8),
            Effect.PhaseShift => (194, 194, 20),
            Effect.Defeat => (216, 216, 24),
            _ => throw new System.ArgumentOutOfRangeException(nameof(effect))
        };

        private Sprite[] GetEffectFrames(Effect effect)
        {
            if (effectFrames.TryGetValue(effect, out var frames)) return frames;
            var sheet = Resources.Load<Texture2D>("Fighter403/Bosses/GC0Effects/" + effect);
            var (width, height, frameCount) = SheetLayout(effect);
            int columns = Mathf.Min(6, frameCount);
            if (sheet == null || sheet.width != width * columns || sheet.height != height * Mathf.CeilToInt((float)frameCount / columns))
            {
                Debug.LogError($"GC-0 is missing the {effect} effect sheet or its dimensions are incorrect.", this);
                effectFrames.Add(effect, null);
                return null;
            }
            frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; ++i)
            {
                var region = new Rect(i % columns * width, sheet.height - (i / columns + 1) * height, width, height);
                frames[i] = Sprite.Create(sheet, region, new Vector2(.5f, .5f), 1f, 0, SpriteMeshType.FullRect);
                frames[i].name = $"GC-0 {effect} {i + 1}";
            }
            effectFrames.Add(effect, frames);
            return frames;
        }

        private EffectTrack CreateEffect(Effect effect, Vector2 position, int order, float angle, Vector2 size)
        {
            var track = new EffectTrack { Kind = effect, Frames = GetEffectFrames(effect) };
            if (track.Frames == null) return track;
            var instance = new GameObject($"GC-0 {effect}");
            instance.transform.SetParent(transform.parent, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            var (width, height, _) = SheetLayout(effect);
            instance.transform.localScale = size == Vector2.zero ? PixelScale :
                new Vector3(size.x / width, size.y / height, 1f);
            track.Renderer = instance.AddComponent<SpriteRenderer>();
            track.Renderer.sortingLayerID = Body.sortingLayerID;
            track.Renderer.sortingOrder = order;
            track.Renderer.color = RoomMarkWhite;
            track.ShowFrame(0f);
            return track;
        }

        private EffectTrack AddActionEffect(Effect effect, Vector2 position, int order, float angle = 0f, Vector2 size = default)
        {
            var track = CreateEffect(effect, position, order, angle, size);
            currentEffects.Add(track);
            return track;
        }

        private void PlayEffect(Effect effect, Vector2 position, float seconds, float angle = 0f, Transform follow = null)
        {
            int order = effect is Effect.Entrance or Effect.StopWave or Effect.PhaseShift ? -1 : 5;
            var track = CreateEffect(effect, position, order, angle, Vector2.zero);
            track.Duration = seconds;
            track.Follow = follow;
            transientEffects.Add(track);
        }

        private void AddScanCellEffect(Vector2 position, int column, int row)
        {
            var effect = AddActionEffect(Effect.ScanCell, position, -9, size: cellSize - Vector2.one * .20f);
            effect.Delay = (column + row) * .018f;
        }

        private void BeginSweepEffects()
        {
            verticalSweep = AddTiledSweep(new Vector2(lastScanX, ArenaCenter.y),
                horizontalSign > 0 ? 0f : 180f, 2f * floorHalfSize.y);
            if (!crossScan) return;
            horizontalSweep = AddTiledSweep(new Vector2(ArenaCenter.x, lastScanY),
                verticalSign > 0 ? 90f : -90f, 2f * floorHalfSize.x);
            sweepIntersection = AddActionEffect(Effect.ScanIntersection, new Vector2(lastScanX, lastScanY), -6);
        }

        private EffectTrack AddTiledSweep(Vector2 position, float angle, float length)
        {
            var effect = AddActionEffect(Effect.ScanBeam, position, -7, angle);
            if (effect.Renderer == null) return effect;
            var (width, _, _) = SheetLayout(Effect.ScanBeam);
            var scale = PixelScale;
            scale.x = ScanHalfWidth / 3f;
            effect.Renderer.transform.localScale = scale;
            effect.Renderer.drawMode = SpriteDrawMode.Tiled;
            effect.Renderer.size = new Vector2(width, length / scale.y);
            return effect;
        }

        private void MoveSweepEffects(float x, float y)
        {
            if (verticalSweep?.Renderer != null) verticalSweep.Renderer.transform.position = new Vector2(x, ArenaCenter.y);
            if (horizontalSweep?.Renderer != null) horizontalSweep.Renderer.transform.position = new Vector2(ArenaCenter.x, y);
            if (sweepIntersection?.Renderer != null) sweepIntersection.Renderer.transform.position = new Vector2(x, y);
        }

        private void AddChargeChevrons(Vector2 start)
        {
            float length = Vector2.Distance(start, chargeDestination);
            int count = Mathf.Clamp(Mathf.FloorToInt(length / 2.4f), 1, 15);
            for (int i = 0; i < count; i++)
            {
                float progress = (i + .5f) / count;
                var effect = AddActionEffect(Effect.ChargeChevron, Vector2.Lerp(start, chargeDestination, progress),
                    -9, AngleOf(chargeDirection), new Vector2(Mathf.Min(1.8f, length / count + .30f), HurtRadius * 2.22f));
                effect.Delay = .42f * progress;
            }
        }

        private void AnimateActionEffects(float dt)
        {
            if (action == Action.Charge) chargeAge += dt;
            else chargeAge = 0f;

            float progress = action switch
            {
                Action.ScanWarning => 1f - remaining / ScanWarningSeconds,
                Action.Scan => 1f - remaining / ScanSeconds,
                Action.ChargeWarning => 1f - remaining / ChargeWarningSeconds,
                Action.Charge => Mathf.Repeat(chargeAge * 3.5f, 1f),
                Action.WallStagger => 1f - remaining / WallStaggerSeconds,
                Action.StopWarning => 1f - remaining / StopWarningSeconds,
                Action.Stopped => 1f - remaining / (phaseTwo ? 2.5f : 3f),
                _ => 0f
            };
            foreach (EffectTrack effect in currentEffects)
            {
                if (effect.Renderer == null) continue;
                float frameProgress = effect.Kind switch
                {
                    Effect.ScanCell when action == Action.Scan => .88f,
                    Effect.ScanBeam => Mathf.Repeat(progress * 4.5f, 1f),
                    Effect.ScanIntersection => Mathf.Repeat(progress * 4f, 1f),
                    _ => Mathf.Clamp01((progress - effect.Delay) / (1f - effect.Delay))
                };
                if (effect.Kind == Effect.ScanCell && action == Action.Scan)
                {
                    Vector2 cell = effect.Renderer.transform.position;
                    bool passed = (lastScanX - cell.x) * horizontalSign > cellSize.x * .5f;
                    if (crossScan) passed &= (lastScanY - cell.y) * verticalSign > cellSize.y * .5f;
                    effect.Renderer.color = new Color(1f, 1f, 1f, passed ? .35f : .84f);
                }
                effect.ShowFrame(frameProgress);
            }
        }

        private void TrackFragmentEffect(DungeonBossSummon fragment) =>
            fragmentEffects.Add(new FragmentTrack { Summon = fragment, LastPosition = fragment.transform.position });

        private void ForgetFragmentEffect(DungeonBossSummon fragment) =>
            fragmentEffects.RemoveAll(effect => ReferenceEquals(effect.Summon, fragment));

        protected override void LateUpdate()
        {
            base.LateUpdate();
            for (int i = fragmentEffects.Count - 1; i >= 0; i--)
            {
                var fragment = fragmentEffects[i];
                if (fragment.Summon == null || !fragment.Summon.Alive)
                {
                    PlayEffect(Effect.FragmentBreak, fragment.LastPosition, .49f);
                    fragmentEffects.RemoveAt(i);
                    continue;
                }
                fragment.LastPosition = fragment.Summon.transform.position;
            }

            for (int i = transientEffects.Count - 1; i >= 0; i--)
            {
                var effect = transientEffects[i];
                effect.Age += Time.deltaTime;
                if (effect.Age >= effect.Duration)
                {
                    effect.Remove();
                    transientEffects.RemoveAt(i);
                }
                else effect.ShowFrame(effect.Age / effect.Duration);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearActionEffects();
            ClearTransientEffects(keepDefeat: false);
            foreach (var frames in effectFrames.Values)
            {
                if (frames == null) continue;
                foreach (var frame in frames) Destroy(frame);
            }
            effectFrames.Clear();
        }

        private void ClearActionEffects()
        {
            foreach (var effect in currentEffects) effect.Remove();
            currentEffects.Clear();
            verticalSweep = horizontalSweep = sweepIntersection = null;
        }

        private void ClearTransientEffects(bool keepDefeat)
        {
            fragmentEffects.Clear();
            for (int i = transientEffects.Count - 1; i >= 0; i--)
            {
                var effect = transientEffects[i];
                if (keepDefeat && effect.Kind == Effect.Defeat) continue;
                effect.Remove();
                transientEffects.RemoveAt(i);
            }
        }

        private static float AngleOf(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }
}
