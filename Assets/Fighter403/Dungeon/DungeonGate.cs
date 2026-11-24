using System;
using UnityEngine;

namespace Fighter403
{
    internal sealed class DungeonGate : MonoBehaviour
    {
        private const int AperturePixels = 60;
        private const int PillarWidth = 18;
        private const int PillarDepth = 38;
        private static readonly RectInt LeftPillar = new(0, 0, PillarWidth, PillarDepth);
        private static readonly RectInt RightPillar = new(PillarWidth + AperturePixels, 0, PillarWidth, PillarDepth);
        private static readonly RectInt GateFace = new(PillarWidth, 0, AperturePixels, DungeonRoomArt.WallFaceDepth);
        public const float Aperture = (float)AperturePixels / DungeonRoomArt.PixelsPerUnit;
        private DungeonDoorState state;
        private DungeonPassage passage;
        private DungeonRoom room;
        private SpriteRenderer gate;
        private SpriteRenderer foregroundLeft, foregroundRight, foregroundCenter;
        private Sprite openSprite, breachedSprite;
        private BoxCollider2D barrier;
        private Action breached;
        private DungeonDoorSignal signal;
        public bool CanBreach => state == DungeonDoorState.Concealed && passage?.Hidden == true && room.Cleared;

        public void Initialize(DungeonDoorState initial, DungeonRoom owner, DungeonPassage link, SpriteRenderer renderer,
            Sprite open, Sprite broken, Sprite[] frames, Action onBreach, int? foregroundOrder)
        {
            passage = link;
            room = owner;
            gate = renderer;
            openSprite = open;
            breachedSprite = broken;
            breached = onBreach;
            bool closed = initial is (DungeonDoorState.Locked or DungeonDoorState.Concealed);
            if (room != null && closed)
            {
                const int depth = DungeonRoomArt.WallFaceDepth;
                const float scale = DungeonRoomArt.PixelsPerUnit;
                barrier = gameObject.AddComponent<BoxCollider2D>();
                barrier.size = new Vector2(Aperture, depth / scale);
                barrier.offset = Vector2.up * ((DungeonRoomArt.Cell - depth) / (2f * scale));
            }
            if (foregroundOrder is int order)
            {
                // Doorposts and a closed door cover the player while the floor-level threshold stays below.
                foregroundLeft = DungeonRoomArt.AddCroppedRenderer("Left gatepost", gate, LeftPillar, order);
                foregroundRight = DungeonRoomArt.AddCroppedRenderer("Right gatepost", gate, RightPillar, order);
                if (closed) foregroundCenter = DungeonRoomArt.AddCroppedRenderer("Closed gate face", gate, GateFace, order);
            }
            state = initial;
            if (initial != DungeonDoorState.Locked) return;
            var light = new GameObject("Gate scan");
            light.transform.SetParent(transform, false);
            var lightSprite = light.AddComponent<SpriteRenderer>();
            lightSprite.sharedMaterial = gate.sharedMaterial;
            lightSprite.sortingOrder = (foregroundOrder ?? gate.sortingOrder) + 1;
            signal = light.AddComponent<DungeonDoorSignal>();
            signal.SetFrames(frames);
        }

        public void Unlock()
        {
            if (state != DungeonDoorState.Locked) return;
            state = DungeonDoorState.Open;
            OpenPassage(openSprite);
            if (barrier != null) barrier.enabled = false;
            if (signal != null) signal.gameObject.SetActive(false);
        }

        public bool TryBreach()
        {
            if (!CanBreach)
                return false;
            state = DungeonDoorState.Breached;
            passage.Breached = true;
            OpenPassage(breachedSprite);
            barrier.enabled = false;
            breached?.Invoke();
            return true;
        }

        private void OpenPassage(Sprite sprite)
        {
            gate.sprite = sprite;
            if (foregroundLeft == null) return;
            foregroundLeft.sprite = DungeonRoomArt.CropSprite(sprite, LeftPillar);
            foregroundRight.sprite = DungeonRoomArt.CropSprite(sprite, RightPillar);
            if (foregroundCenter != null) foregroundCenter.enabled = false;
        }
    }
}
