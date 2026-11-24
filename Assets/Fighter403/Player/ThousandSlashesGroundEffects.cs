using System;
using System.Collections.Generic;
using Fighter403.UI;
using UnityEngine;

namespace Fighter403.Player
{
    internal sealed class ThousandSlashesGroundEffects : IDisposable
    {
        private const int FramesPerSecond = 24;
        private const string ResourcePath = "Fighter403/ThousandSlashesGround/";
        private readonly SpriteRenderer source;
        private readonly Sprite[] strikeFrames = LoadFrames("StrikeGround");
        private readonly Sprite[] finisherFrames = LoadFrames("FinisherGround");
        private readonly List<Mark> active = new();
        private readonly Stack<SpriteRenderer> available = new();

        internal ThousandSlashesGroundEffects(SpriteRenderer source)
        {
            this.source = source;
        }

        internal void Add(ThousandSlashesScreenEffect.SlashFrame slash, bool finisher, bool mirror = false)
        {
            Sprite[] frames = finisher ? finisherFrames : strikeFrames;
            if (frames.Length == 0) return;

            SpriteRenderer surface;
            if (!available.TryPop(out surface))
            {
                surface = new GameObject("Thousand Slashes ground mark").AddComponent<SpriteRenderer>();
                surface.gameObject.layer = source.gameObject.layer;
                surface.sortingLayerID = source.sortingLayerID;
                // Above the arena floor and below both the player and targets.
                surface.sortingOrder = 0;
                surface.sharedMaterial = source.sharedMaterial;
            }
            surface.transform.SetPositionAndRotation(slash.Position,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(slash.Direction.y, slash.Direction.x) * Mathf.Rad2Deg));
            surface.transform.localScale = new Vector3(slash.Scale.x, slash.Scale.y, 1f);
            surface.flipY = mirror;
            surface.sprite = frames[0];
            surface.gameObject.SetActive(true);
            active.Add(new Mark(surface, frames));
        }

        internal void Advance(float seconds)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var mark = active[i];
                mark.Age += seconds;
                int frame = Mathf.FloorToInt(mark.Age * FramesPerSecond);
                if (frame >= mark.Frames.Length)
                {
                    Recycle(mark.Surface);
                    active.RemoveAt(i);
                    continue;
                }
                mark.Surface.sprite = mark.Frames[frame];
                active[i] = mark;
            }
        }

        internal void Clear()
        {
            foreach (var mark in active) Recycle(mark.Surface);
            active.Clear();
        }

        public void Dispose()
        {
            Clear();
            while (available.TryPop(out var surface))
                if (surface != null) UnityEngine.Object.Destroy(surface.gameObject);
        }

        private void Recycle(SpriteRenderer surface)
        {
            if (surface == null) return;
            surface.gameObject.SetActive(false);
            available.Push(surface);
        }

        private static Sprite[] LoadFrames(string filename)
        {
            var frames = Resources.LoadAll<Sprite>(ResourcePath + filename);
            Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            return frames;
        }

        private struct Mark
        {
            internal readonly SpriteRenderer Surface;
            internal readonly Sprite[] Frames;
            internal float Age;

            internal Mark(SpriteRenderer surface, Sprite[] frames)
            {
                Surface = surface;
                Frames = frames;
                Age = 0f;
            }
        }
    }
}
