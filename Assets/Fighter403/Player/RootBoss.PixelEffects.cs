using System.Collections.Generic;
using UnityEngine;

namespace Fighter403.Player
{
    internal sealed partial class RootBoss
    {
        // Sequence order matches Tools/Fighter403/build-root-boss-effects.mjs.
        private enum PixelCue
        {
            Arrival, Lock, Breach, Audit, TwinAudit, BlinkTarget, BlinkOut, BlinkIn, Fork,
            Clone, ChildSpawn, ChildHit, KillSignal, Detonation, HealPacket, GuardianRaise,
            ShieldTile, GuardCrack, ShieldBreak, DeletionWarning, DeletedBand, ForkPhase,
            SudoPhase, Death, ChildWarning, KillReticle
        }

        private enum FloorCue { SectorWarning, SectorStrike, AuditWarning, AuditStrike, DeletionWarning, DeletedBand }

        private const string EffectsTexture = "Fighter403/Bosses/RootEffects";
        private const int EffectFrames = 12, EffectCell = 96, EffectColumns = 16;
        private const float EffectPpu = 24f;
        private static readonly Sprite[,] RootFrames = new Sprite[(int)PixelCue.KillReticle + 1, EffectFrames];
        private static readonly int CueId = Shader.PropertyToID("_Cue");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int DimensionsId = Shader.PropertyToID("_Dimensions");
        private static readonly int ToneId = Shader.PropertyToID("_Tone");
        private static Texture2D effectAtlas;

        private readonly List<PixelEffect> rootPixels = new();
        private readonly List<FloorEffect> rootFloors = new();
        private Material floorMaterial;

        private sealed class PixelEffect
        {
            internal GameObject Object;
            internal SpriteRenderer Renderer;
            internal PixelCue Cue;
            internal float Age, Duration;
            internal bool Loop, FollowBody, Travel;
            internal Vector2 Start;
            internal Transform Attachment;
        }

        private sealed class FloorEffect
        {
            internal GameObject Object, Anchor;
            internal SpriteRenderer Renderer;
            internal Vector2 Size;
            internal FloorCue Cue;
            internal float Age, Duration, Progress;
            internal bool Timed;
            internal Color Tone;
            internal readonly MaterialPropertyBlock Properties = new();
        }

        private static Sprite FindRootFrame(PixelCue cue, int frame)
        {
            var existing = RootFrames[(int)cue, frame];
            if (existing != null) return existing;
            if (effectAtlas == null) effectAtlas = Resources.Load<Texture2D>(EffectsTexture);
            if (effectAtlas == null) return null;
            int index = (int)cue * EffectFrames + frame;
            int column = index % EffectColumns;
            int row = index / EffectColumns;
            var area = new Rect(column * EffectCell, effectAtlas.height - (row + 1) * EffectCell,
                EffectCell, EffectCell);
            var sprite = Sprite.Create(effectAtlas, area, Vector2.one * .5f, EffectPpu, 0, SpriteMeshType.FullRect);
            sprite.name = $"Root / {cue} {frame + 1}";
            RootFrames[(int)cue, frame] = sprite;
            return sprite;
        }

        private PixelEffect Pixel(PixelCue cue, Vector2 position, float scale, float seconds,
            int order, float angle = 0f, bool loop = false, bool detached = false)
        {
            Sprite sprite = FindRootFrame(cue, 0);
            if (sprite == null) return null;
            var instance = new GameObject($"Root FX / {cue}");
            instance.transform.SetParent(transform.parent, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            instance.transform.localScale = Vector3.one * scale;
            var renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = Body.sortingLayerID;
            renderer.sortingOrder = order;
            var effect = new PixelEffect
            {
                Object = instance, Renderer = renderer, Cue = cue, Duration = seconds, Loop = loop
            };
            if (detached)
            {
                instance.AddComponent<RootDeathPixels>().Play(renderer, seconds);
                return effect;
            }
            rootPixels.Add(effect);
            return effect;
        }

        private PixelEffect AttachedPixel(PixelCue cue, GameObject mark, Vector2 position, float scale,
            float seconds, int order, float angle = 0f, bool loop = false)
        {
            var effect = Pixel(cue, position, scale, seconds, order, angle, loop);
            if (effect != null)
            {
                effect.Attachment = mark.transform;
                effect.Object.transform.SetParent(mark.transform, true);
            }
            return effect;
        }

        private void BodyPixel(PixelCue cue, float scale, float seconds, int order, float angle = 0f)
        {
            var effect = Pixel(cue, Position, scale, seconds, order, angle);
            if (effect != null) effect.FollowBody = true;
        }

        private void HealTrail(Vector2 from)
        {
            Vector2 toward = Position - from;
            var effect = Pixel(PixelCue.HealPacket, from, .78f, .48f, Body.sortingOrder + 3, Angle(toward));
            if (effect == null) return;
            effect.Travel = true;
            effect.Start = from;
        }

        private void TickPixels(float dt)
        {
            for (int i = rootPixels.Count - 1; i >= 0; i--)
            {
                PixelEffect effect = rootPixels[i];
                effect.Age += dt;
                if (effect.Object == null || (!effect.Loop && effect.Age >= effect.Duration) ||
                    (!ReferenceEquals(effect.Attachment, null) &&
                     (effect.Attachment == null || !effect.Attachment.gameObject.activeSelf)))
                {
                    DestroyEffect(effect.Object);
                    rootPixels.RemoveAt(i);
                    continue;
                }
                float progress = effect.Loop ? Mathf.Repeat(effect.Age / effect.Duration, 1f) :
                    Mathf.Clamp01(effect.Age / effect.Duration);
                int index = Mathf.Min(EffectFrames - 1, (int)(progress * EffectFrames));
                effect.Renderer.sprite = FindRootFrame(effect.Cue, index);
                if (effect.Travel)
                    effect.Object.transform.position = Vector2.Lerp(effect.Start, Position, progress * progress);
                else if (effect.FollowBody)
                    effect.Object.transform.position = Position;
            }
            for (int i = rootFloors.Count - 1; i >= 0; i--)
            {
                FloorEffect effect = rootFloors[i];
                effect.Age += dt;
                if (effect.Object == null || effect.Anchor == null || !effect.Anchor.activeSelf ||
                    (effect.Timed && effect.Age >= effect.Duration))
                {
                    DestroyEffect(effect.Object);
                    rootFloors.RemoveAt(i);
                    continue;
                }
                UpdateFloor(effect);
            }
        }

        private FloorEffect Floor(GameObject anchor, FloorCue cue, Vector2 position, Vector2 size, Vector2 direction,
            Color tone, float duration = 0f, bool timed = false)
        {
            if (floorMaterial == null)
            {
                var shader = Resources.Load<Shader>("Fighter403RootFloorEffect");
                if (shader == null) return null;
                floorMaterial = new Material(shader);
            }

            var instance = new GameObject($"Root FX / {cue}");
            instance.transform.SetParent(anchor.transform, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, Angle(direction)));
            instance.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = FloorQuad;
            renderer.sortingLayerID = Body.sortingLayerID;
            renderer.sortingOrder = cue == FloorCue.DeletedBand ? -10 : -9;
            renderer.sharedMaterial = floorMaterial;
            var effect = new FloorEffect
            {
                Anchor = anchor, Object = instance, Renderer = renderer, Size = size, Cue = cue,
                Tone = tone, Duration = duration, Timed = timed
            };
            anchor.GetComponent<MeshRenderer>().enabled = false;
            anchor.GetComponent<LineRenderer>().enabled = false;
            rootFloors.Add(effect);
            UpdateFloor(effect);
            return effect;
        }

        private static Sprite floorQuad;
        private static Sprite FloorQuad
        {
            get
            {
                if (floorQuad != null) return floorQuad;
                var white = Texture2D.whiteTexture;
                floorQuad = Sprite.Create(white, new Rect(0, 0, white.width, white.height), Vector2.one * .5f,
                    white.width, 0, SpriteMeshType.FullRect);
                floorQuad.name = "Root floor footprint";
                return floorQuad;
            }
        }

        private static void UpdateFloor(FloorEffect effect)
        {
            float progress = effect.Timed ? effect.Age / effect.Duration : effect.Progress;
            effect.Properties.SetFloat(CueId, (int)effect.Cue);
            effect.Properties.SetVector(ProgressId, new Vector4(progress, effect.Age, SectorRadius, AuditHalfWidth));
            effect.Properties.SetVector(DimensionsId, new Vector4(effect.Size.x, effect.Size.y, 0f, 0f));
            effect.Properties.SetColor(ToneId, effect.Tone);
            effect.Renderer.SetPropertyBlock(effect.Properties);
        }

        private void CastFloorProgress(Cast active)
        {
            foreach (FloorEffect effect in rootFloors)
            {
                if (effect.Timed || !active.Marks.Contains(effect.Anchor)) continue;
                effect.Progress = Mathf.Clamp01(active.Age / active.Duration);
                UpdateFloor(effect);
            }
        }

        private void MoveFloor(GameObject ray)
        {
            foreach (FloorEffect effect in rootFloors)
            {
                if (effect.Anchor != ray) continue;
                effect.Size.x = ray.transform.localScale.x;
                effect.Object.transform.localPosition = Vector3.zero;
                effect.Object.transform.localRotation = Quaternion.identity;
                UpdateFloor(effect);
                return;
            }
        }

        private void ClearRootEffects(bool releaseMaterial)
        {
            foreach (PixelEffect effect in rootPixels) DestroyEffect(effect.Object);
            foreach (FloorEffect effect in rootFloors) DestroyEffect(effect.Object);
            rootPixels.Clear();
            rootFloors.Clear();
            if (!releaseMaterial || floorMaterial == null) return;
            Destroy(floorMaterial);
            floorMaterial = null;
        }

        private static void DestroyEffect(GameObject effect)
        {
            if (effect == null) return;
            effect.SetActive(false);
            Destroy(effect);
        }

        private static float Angle(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        internal static Sprite DeathFrame(float progress)
        {
            int frame = Mathf.Min(EffectFrames - 1, (int)(progress * EffectFrames));
            return FindRootFrame(PixelCue.Death, frame);
        }
    }
}
