using System;
using Fighter403.Audio;
using UnityEngine;

namespace Fighter403
{
    internal sealed class DungeonRoomActor : MonoBehaviour
    {
        public const int FrameCount = 12;
        private const float SecondsPerFrame = .055f;
        private const float IdleSecondsPerFrame = .13f;
        private SpriteRenderer display;
        private Sprite[] frames;
        private Sprite[] idleFrames;
        private DungeonRoom room;
        private Action completion;
        private float elapsed;
        private int shownFrame;

        public bool Playing => completion != null;

        internal static bool HasProximityInteraction(DungeonRoomKind kind) => kind is
            DungeonRoomKind.Shop or DungeonRoomKind.Vault or DungeonRoomKind.Maintenance or
            DungeonRoomKind.Event or DungeonRoomKind.Hidden;

        internal Vector3 PromptPosition => transform.position + Vector3.up * (room.Kind switch
        {
            DungeonRoomKind.Shop => 1.65f,
            DungeonRoomKind.Event => 1.35f,
            _ => 1.1f,
        });

        private bool Treasure => room.Kind == DungeonRoomKind.Vault;

        public void Initialize(SpriteRenderer renderer, DungeonRoom owner, Sprite[] sprites, Sprite[] idle = null)
        {
            display = renderer;
            room = owner;
            frames = sprites is { Length: FrameCount } ? sprites : null;
            idleFrames = idle;
            ResetPose();
        }

        public bool TryPlay(Action onComplete)
        {
            if (Playing) return true;
            if (!isActiveAndEnabled || frames == null || Treasure && (room.TreasureOpened || room.Claimed)) return false;
            elapsed = 0f;
            SetFrame(0);
            completion = onComplete;
            return true;
        }

        public void Cancel()
        {
            if (!Playing) return;
            completion = null;
            ResetPose();
        }

        private void Update()
        {
            if (!Playing)
            {
                if (idleFrames == null || idleFrames.Length == 0) return;
                elapsed = Mathf.Repeat(elapsed + Time.unscaledDeltaTime, idleFrames.Length * IdleSecondsPerFrame);
                var idleSprite = idleFrames[Mathf.FloorToInt(elapsed / IdleSecondsPerFrame)];
                if (display.sprite != idleSprite) display.sprite = idleSprite;
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            int next = Mathf.Min(FrameCount - 1, Mathf.FloorToInt(elapsed / SecondsPerFrame));
            int cue = Treasure ? 4 : 5;
            if (shownFrame < cue && next >= cue)
                GameSfx.Play(Treasure ? Sfx.DungeonTreasureOpen : Sfx.DungeonMerchant);
            SetFrame(next);
            if (elapsed < FrameCount * SecondsPerFrame) return;

            if (Treasure) room.TreasureOpened = true;
            var completed = completion;
            completion = null;
            ResetPose();
            completed.Invoke();
        }

        private void OnDisable() => Cancel();

        private void ResetPose()
        {
            elapsed = 0f;
            SetFrame(Treasure && (room.TreasureOpened || room.Claimed) ? FrameCount - 1 : 0);
            if (idleFrames is { Length: > 0 }) display.sprite = idleFrames[0];
        }

        private void SetFrame(int frame)
        {
            shownFrame = frame;
            if (display != null && frames != null) display.sprite = frames[frame];
        }
    }
}
