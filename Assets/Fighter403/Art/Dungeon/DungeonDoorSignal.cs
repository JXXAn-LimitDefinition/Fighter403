using UnityEngine;

namespace Fighter403
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DungeonDoorSignal : MonoBehaviour
    {
        public const int FramesPerSecond = 10;
        [SerializeField] private Sprite[] frames;
        private SpriteRenderer signal;
        private int lastFrame = -1;

        public void SetFrames(Sprite[] sprites)
        {
            frames = sprites;
            lastFrame = -1;
            GetComponent<SpriteRenderer>().sprite = sprites != null && sprites.Length > 0 ? sprites[0] : null;
        }

        private void Awake() => signal = GetComponent<SpriteRenderer>();

        private void Update()
        {
            if (frames == null || frames.Length == 0)
                return;

            int frame = (int)(Time.timeAsDouble * FramesPerSecond % frames.Length);
            if (frame == lastFrame)
                return;

            signal.sprite = frames[frame];
            lastFrame = frame;
        }
    }
}
