using System.Collections.Generic;
using UnityEngine;

namespace Fighter403.Player
{
    public sealed partial class FirewallBoss
    {
        private const int PixelFrameSize = 112;
        private const int PixelColumns = 12;

        private enum PixelEffect
        {
            Arrival, PacketCharge, Packet, PacketHit, MeteorFall, MeteorBurst, DeployOpen, DeployClose,
            Seal, Vortex, Collapse, Overload, Defeat, Vent, PacketLaunch, Count
        }

        private sealed class PixelSequence
        {
            internal PixelEffect Clip;
            internal SpriteRenderer Renderer;
            internal double Start;
            internal float Duration;
        }

        private readonly List<PixelSequence> pixelSequences = new();
        private Sprite[,] pixelSprites;
        private SpriteRenderer ventEffect;

        private void LoadPixelEffects()
        {
            var sheet = Resources.Load<Texture2D>("Fighter403/Bosses/FirewallEffects");
            int rows = (int)PixelEffect.Count;
            if (sheet == null || sheet.width != PixelColumns * PixelFrameSize || sheet.height != rows * PixelFrameSize)
                return;

            pixelSprites = new Sprite[rows, PixelColumns];
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < PixelColumns; column++)
            {
                Rect rect = new(column * PixelFrameSize, (rows - 1 - row) * PixelFrameSize, PixelFrameSize, PixelFrameSize);
                pixelSprites[row, column] = Sprite.Create(sheet, rect, Vector2.one * .5f, 32f, 0, SpriteMeshType.FullRect);
                pixelSprites[row, column].name = $"{(PixelEffect)row} {column:00}";
            }
            ventEffect = AddSprite("Core cooling vents", center, SpriteAt(PixelEffect.Vent, 0f), 2);
            ventEffect.gameObject.SetActive(false);
            ventEffect.transform.localScale = Vector3.one * 2.4f;
        }

        private Sprite SpriteAt(PixelEffect clip, float progress, Sprite fallback = null)
        {
            int column = Mathf.Min(PixelColumns - 1, (int)(Mathf.Clamp01(progress) * PixelColumns));
            return pixelSprites?[(int)clip, column] ?? fallback;
        }

        private void PlayPixels(PixelEffect clip, string label, Vector2 position, float duration, float scale,
            int order, float angle = 0f)
        {
            Sprite first = SpriteAt(clip, 0f);
            if (first == null) return;
            var renderer = AddSprite(label, position, first, order);
            renderer.transform.localScale = Vector3.one * scale;
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            pixelSequences.Add(new PixelSequence
            {
                Clip = clip, Renderer = renderer, Start = Time.timeAsDouble, Duration = duration
            });
        }

        private void TickPixelEffects()
        {
            double now = Time.timeAsDouble;
            for (int i = pixelSequences.Count - 1; i >= 0; i--)
            {
                var sequence = pixelSequences[i];
                float age = (float)(now - sequence.Start);
                if (age >= sequence.Duration)
                {
                    Remove(sequence.Renderer);
                    pixelSequences.RemoveAt(i);
                }
                else sequence.Renderer.sprite = SpriteAt(sequence.Clip, age / sequence.Duration);
            }

            if (ventEffect == null) return;
            bool venting = Alive && recovery > 0f;
            if (ventEffect.gameObject.activeSelf != venting) ventEffect.gameObject.SetActive(venting);
            if (venting)
            {
                ventEffect.sprite = SpriteAt(PixelEffect.Vent, Mathf.Repeat(idleAge * 1.8f, 1f));
                ventEffect.color = new Color(1f, 1f, 1f, Mathf.Clamp01(recovery * 3.2f));
            }
        }

        private void ClearPixelEffects()
        {
            foreach (var sequence in pixelSequences) Remove(sequence.Renderer);
            pixelSequences.Clear();
            if (ventEffect != null) ventEffect.gameObject.SetActive(false);
        }

        private void DisposePixelEffects()
        {
            if (pixelSprites == null) return;
            foreach (Sprite sprite in pixelSprites) Destroy(sprite);
        }
    }
}
