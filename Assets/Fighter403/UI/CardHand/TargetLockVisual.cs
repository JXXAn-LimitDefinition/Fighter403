using Fighter403.Player;
using UnityEngine;

namespace Fighter403.UI
{
    [DisallowMultipleComponent, DefaultExecutionOrder(10)]
    internal sealed class TargetLockVisual : MonoBehaviour
    {
        private const int FrameWidth = 96;
        private const int FrameHeight = 96;
        private const int Columns = 6;
        private const int FrameCount = 34;
        private const int LockOnFrameCount = 7;
        private const float FramesPerSecond = 30f;
        private const float PixelsPerUnit = 100f;
        private const float TargetRadiusPixels = 24f;

        private SpriteRenderer display;
        private CombatTarget target;
        private Sprite[] frames;
        private bool locked;
        private float elapsed;

        internal void SetLocked(bool value)
        {
            EnsureInitialized();
            if (display == null) return;
            locked = value;
            elapsed = 0f;
            display.enabled = value;
            if (!value) return;
            FollowTarget();
            display.sprite = frames[0];
        }

        private void Awake() => EnsureInitialized();

        private void LateUpdate()
        {
            if (!locked) return;
            if (!target.Alive)
            {
                SetLocked(false);
                return;
            }
            FollowTarget();
            elapsed += Time.unscaledDeltaTime;
            int frame = Mathf.FloorToInt(elapsed * FramesPerSecond);
            if (frame >= LockOnFrameCount)
            {
                int idleFrameCount = frames.Length - LockOnFrameCount;
                frame = LockOnFrameCount + (frame - LockOnFrameCount) % idleFrameCount;
            }
            if (display.sprite != frames[frame]) display.sprite = frames[frame];
        }

        private void FollowTarget()
        {
            target.GetHurtCircle(out var center, out float radius);
            Transform reticle = display.transform;
            reticle.SetPositionAndRotation(new Vector3(center.x, center.y, transform.position.z), Quaternion.identity);
            Vector3 parentScale = transform.lossyScale;
            float size = radius * PixelsPerUnit / TargetRadiusPixels;
            reticle.localScale = new Vector3(size / Mathf.Max(Mathf.Abs(parentScale.x), 0.001f),
                size / Mathf.Max(Mathf.Abs(parentScale.y), 0.001f), 1f);
        }

        private void EnsureInitialized()
        {
            if (display != null) return;
            var sheet = Resources.Load<Texture2D>("Fighter403TargetLock");
            if (sheet == null)
            {
                Debug.LogWarning("Target lock sprite sheet Fighter403TargetLock was not found.", this);
                enabled = false;
                return;
            }

            target = GetComponent<CombatTarget>();
            var targetDisplay = GetComponent<SpriteRenderer>();
            display = new GameObject("Target lock reticle", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            display.transform.SetParent(transform, false);
            display.gameObject.layer = gameObject.layer;
            display.sortingLayerID = targetDisplay.sortingLayerID;
            display.sortingOrder = targetDisplay.sortingOrder + 100;
            display.sharedMaterial = targetDisplay.sharedMaterial;
            display.enabled = false;

            frames = new Sprite[FrameCount];
            Vector2 pivot = new(0.5f, 0.5f);
            for (int i = 0; i < FrameCount; ++i)
            {
                int column = i % Columns;
                int row = i / Columns;
                var rect = new Rect(column * FrameWidth, sheet.height - (row + 1) * FrameHeight,
                    FrameWidth, FrameHeight);
                frames[i] = Sprite.Create(sheet, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                frames[i].name = $"Target lock {i:00}";
            }
        }

        private void OnDisable()
        {
            locked = false;
            if (display != null) display.enabled = false;
        }

        private void OnDestroy()
        {
            if (frames == null) return;
            foreach (var frame in frames)
                if (frame != null) Destroy(frame);
        }
    }
}
