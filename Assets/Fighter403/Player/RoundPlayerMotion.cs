using UnityEngine;

namespace Fighter403.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RoundPlayerMovement), typeof(SpriteRenderer))]
    public sealed class RoundPlayerMotion : MonoBehaviour
    {
        private const int Directions = 8;
        private const int Neutral = 5;
        private const int PoseCount = 11;
        private const float PoseStep = 0.046f;
        private const float Tau = 2f * Mathf.PI;

        [SerializeField, Range(0f, 1.5f)] private float jellyStrength = 1f;
        [SerializeField, HideInInspector] private Sprite[] poses;

        private RoundPlayerMovement movement;
        private SpriteRenderer display;
        private int heading;
        private bool wasMoving;
        private float phase;
        private float activity;
        private float displacement;
        private float velocity;

        private void Awake()
        {
            movement = GetComponent<RoundPlayerMovement>();
            display = GetComponent<SpriteRenderer>();
        }

        internal void PlaySlashRecoil(bool finisher) => velocity -= finisher ? 7f : 3.8f;
        internal void PrepareSlash() => velocity -= 5.5f;

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f || poses == null || poses.Length != Directions * PoseCount)
                return;

            var amount = movement.MoveAmount;
            var moving = amount > 0.035f;
            var direction = movement.FacingDirection;
            var nextHeading = Mathf.RoundToInt(Mathf.Atan2(direction.y, direction.x) * (4f / Mathf.PI)) & 7;

            if (moving && !wasMoving)
            {
                phase = 0f;
                velocity += 6.2f * amount;
            }
            else if (!moving && wasMoving)
            {
                velocity -= 5.3f * activity;
            }
            else if (moving && heading != nextHeading)
            {
                var turn = Mathf.Abs(heading - nextHeading);
                velocity -= 1.15f * Mathf.Min(turn, Directions - turn) * amount;
            }

            heading = nextHeading;
            wasMoving = moving;
            activity = Mathf.MoveTowards(activity, moving ? amount : 0f, dt * (moving ? 9f : 16f));
            if (moving)
                phase = (phase + Tau * (2.6f + 0.55f * amount) * dt) % Tau;

            StepSpring(dt);
            var shape = jellyStrength * (displacement + 0.085f * activity * Mathf.Sin(phase));
            var pose = Mathf.Clamp(Neutral + Mathf.RoundToInt(shape / PoseStep), 0, PoseCount - 1);
            ShowPose(pose);
        }

        // Closed-form damped spring: changing the render frame rate does not change the rebound.
        private void StepSpring(float dt)
        {
            const float decay = 11f;
            const float frequency = 21f;
            velocity = Mathf.Clamp(velocity, -8f, 8f);
            var a = displacement;
            var b = (velocity + decay * a) / frequency;
            var angle = frequency * dt;
            var sin = Mathf.Sin(angle);
            var cos = Mathf.Cos(angle);
            var falloff = Mathf.Exp(-decay * dt);
            displacement = falloff * (a * cos + b * sin);
            velocity = falloff * frequency * (b * cos - a * sin) - decay * displacement;
        }

        private void ShowPose(int pose)
        {
            var sprite = poses[pose * Directions + heading];
            if (sprite != null && display.sprite != sprite)
                display.sprite = sprite;
        }

        private void OnDisable()
        {
            wasMoving = false;
            phase = activity = displacement = velocity = 0f;
            if (display != null && poses != null && poses.Length == Directions * PoseCount)
                ShowPose(Neutral);
        }
    }
}
