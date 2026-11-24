using Unity.Cinemachine;
using UnityEngine;

namespace Fighter403.Player
{
    // Apply prediction framing after the Cinemachine brain updates the camera.
    [DisallowMultipleComponent, DefaultExecutionOrder(110)]
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        private const float CombatBlendSeconds = 0.48f;
        private const float LockReturnSeconds = 0.32f;

        [SerializeField] private Transform target;

        [SerializeField] private Vector2 deadZone = new Vector2(2.5f, 1.5f);
        [SerializeField, Min(0.01f)] private float lockFramingSmoothTime = 0.12f;
        private CinemachineBrain brain;
        private Camera view;
        private CinemachineTargetGroup lockGroup;
        private CinemachineCamera lockCamera;
        private CinemachineGroupFraming lockFraming;
        private CameraTarget normalTargets;
        private Vector3 previousFramedPosition;
        private float previousFramedSize;
        private Vector3 lockPositionVelocity;
        private float lockZoomVelocity;
        private float lockDampingWeight;
        private Transform predictionTarget;
        private Transform combatFocus;
        private Vector2? combatIncludePoint;
        private bool recenter;
        private Vector3 shakeOffset;
        private float shakeStart, shakeDuration, shakeAmplitude;
        private float? unzoomedSize;
        private float combatZoom = 1f;
        private CombatShot combatShot;
        private int combatBlendOverride = -1;
        private float combatBlendStart;
        private float trialEntranceStart = float.NegativeInfinity;
        private float? trialEntranceBaseSize;
        private Vector2 trialCenter, trialHalfSize;

        private void Awake()
        {
            TryGetComponent(out brain);
            view = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            previousFramedPosition = transform.position;
            previousFramedSize = view.orthographicSize;
        }

        internal float CombatViewSize => BaseViewSize * combatZoom * (trialEntranceBaseSize.HasValue ? 1.12f : 1f);

        internal bool IsLockFramingActive => lockFraming != null && lockFraming.isActiveAndEnabled &&
            brain != null && brain.isActiveAndEnabled && ReferenceEquals(brain.ActiveVirtualCamera, lockCamera);

        private float BaseViewSize => brain != null && brain.isActiveAndEnabled && brain.ActiveVirtualCamera != null &&
            (brain.State.BlendHint & CameraState.BlendHints.NoLens) == 0
                ? brain.State.Lens.OrthographicSize
                : unzoomedSize ?? trialEntranceBaseSize ?? view.orthographicSize;

        internal void SetCombatZoom(float scale)
        {
            combatZoom = scale;
            if (!Mathf.Approximately(scale, 1f))
                unzoomedSize ??= BaseViewSize;
            else if (unzoomedSize.HasValue)
            {
                view.orthographicSize = BaseViewSize;
                unzoomedSize = null;
            }
        }

        internal void SetPredictionTarget(Transform value)
        {
            if (value == null && predictionTarget != null) recenter = true;
            if (value != null) ReleaseCombatBlend();
            predictionTarget = value;
            UpdateLockFraming();
        }

        internal void SetCombatFocus(Transform value, Vector2? includePoint = null)
        {
            if (value == null && combatFocus != null) recenter = true;
            if (value != null) ReleaseCombatBlend();
            combatFocus = value;
            combatIncludePoint = includePoint;
            UpdateLockFraming();
        }

        internal void SetLockTarget(Transform player, CombatTarget enemy)
        {
            if (player != null && enemy != null)
            {
                if (lockGroup == null)
                    lockGroup = new GameObject("Lock-on Target Group").AddComponent<CinemachineTargetGroup>();

                enemy.GetHurtCircle(out _, out float radius);
                lockGroup.Targets.Clear();
                lockGroup.AddMember(player, 1f, 0.5f);
                lockGroup.AddMember(enemy.transform, 1f, radius);
                lockGroup.DoUpdate();
            }
            else if (lockGroup != null) lockGroup.Targets.Clear();

            UpdateLockFraming();
        }

        private void UpdateLockFraming()
        {
            bool useGroup = isActiveAndEnabled && brain != null && brain.isActiveAndEnabled &&
                lockGroup != null && lockGroup.Targets.Count > 0 && predictionTarget == null && combatFocus == null;
            if (lockFraming == null)
            {
                if (!useGroup || brain.ActiveVirtualCamera is not CinemachineCamera camera) return;
                lockCamera = camera;
                lockFraming = lockCamera.gameObject.AddComponent<CinemachineGroupFraming>();
                lockFraming.Damping = 0f;
                lockFraming.FramingSize = 0.7f;
                lockFraming.CenterOffset = new Vector2(0f, 0.12f);
                lockFraming.OrthoSizeRange = new Vector2(lockCamera.Lens.OrthographicSize, float.MaxValue);
                lockFraming.enabled = false;
            }
            if (lockFraming.enabled == useGroup) return;

            // Keep the normal Position Composer tracking the player underneath the group framing.
            if (useGroup)
            {
                normalTargets = lockCamera.Target;
                lockCamera.LookAt = lockGroup.transform;
            }
            else
            {
                lockCamera.Target.CustomLookAtTarget = normalTargets.CustomLookAtTarget;
                lockCamera.Target.LookAtTarget = normalTargets.LookAtTarget;
            }
            lockFraming.enabled = useGroup;
        }

        internal void BlendFromCombatFocus()
        {
            if (combatFocus == null && !combatIncludePoint.HasValue) return;
            if (brain == null || !brain.isActiveAndEnabled || brain.ActiveVirtualCamera == null)
            {
                SetCombatFocus(null);
                return;
            }

            combatFocus = null;
            combatIncludePoint = null;
            recenter = false;
            UpdateLockFraming();
            var state = CameraState.Default;
            state.RawPosition = transform.position - shakeOffset;
            state.RawOrientation = transform.rotation;
            state.ReferenceUp = transform.up;
            state.Lens = LensSettings.FromCamera(view);
            combatShot = new CombatShot(state);
            combatBlendStart = Time.unscaledTime;
            combatBlendOverride = brain.SetCameraOverride(combatBlendOverride, 0, combatShot, null, 0f, -1f);
        }

        private void Update()
        {
            UpdateLockFraming();
            if (combatBlendOverride < 0) return;
            float progress = (Time.unscaledTime - combatBlendStart) / CombatBlendSeconds;
            if (progress >= 1f || brain == null || !brain.isActiveAndEnabled)
                ReleaseCombatBlend();
            else
                brain.SetCameraOverride(combatBlendOverride, 0, combatShot, null,
                    Mathf.SmoothStep(0f, 1f, progress), -1f);
        }

        private void ReleaseCombatBlend()
        {
            if (combatBlendOverride < 0) return;
            brain?.ReleaseCameraOverride(combatBlendOverride);
            combatBlendOverride = -1;
            combatShot = null;
        }

        internal void Shake(float amplitude, float seconds)
        {
            if (amplitude <= 0f || seconds <= 0f) return;
            shakeStart = Time.unscaledTime;
            shakeDuration = seconds;
            shakeAmplitude = amplitude;
        }

        internal void ClearShake()
        {
            transform.position -= shakeOffset;
            shakeOffset = Vector3.zero;
            shakeDuration = 0f;
        }

        internal void NotifyRoomWarp(Transform player, Vector2 delta)
        {
            ReleaseCombatBlend();
            ResetLockDamping();
            ClearShake();
            (brain?.ActiveVirtualCamera as CinemachineVirtualCameraBase)?.OnTargetObjectWarped(player, delta);
            recenter = true;
        }

        internal void FrameTrialEntrance(Vector2 center, Vector2 halfSize)
        {
            trialEntranceBaseSize = BaseViewSize;
            trialCenter = center;
            trialHalfSize = halfSize;
            trialEntranceStart = Time.unscaledTime;
        }

        internal void EndTrialFraming()
        {
            if (!trialEntranceBaseSize.HasValue) return;
            view.orthographicSize = BaseViewSize * combatZoom;
            trialEntranceStart = float.NegativeInfinity;
            trialEntranceBaseSize = null;
        }

        private void LateUpdate()
        {
            bool drivenByBrain = brain != null && brain.isActiveAndEnabled && brain.ActiveVirtualCamera != null;
            var position = drivenByBrain ? transform.position : transform.position - shakeOffset;
            var focus = predictionTarget != null ? predictionTarget : combatFocus;
            var follow = focus != null ? focus : target;
            bool groupFraming = IsLockFramingActive;
            if (follow != null && !groupFraming && combatBlendOverride < 0)
            {
                Vector2 point = follow.position;
                if (predictionTarget == null && combatFocus != null && combatIncludePoint.HasValue)
                    point = (point + combatIncludePoint.Value) * 0.5f;
                Vector2 delta = point - (Vector2)position;
                var zone = focus != null || recenter ? Vector2.zero : deadZone;
                position.x += delta.x - Mathf.Clamp(delta.x, -zone.x, zone.x);
                position.y += delta.y - Mathf.Clamp(delta.y, -zone.y, zone.y);
            }
            recenter = false;
            if (unzoomedSize.HasValue || trialEntranceBaseSize.HasValue) view.orthographicSize = CombatViewSize;
            float entranceAge = Time.unscaledTime - trialEntranceStart;
            if (entranceAge < 1.1f && focus == null && !groupFraming && combatBlendOverride < 0)
            {
                // Preserve extra room at the bottom for the card hand.
                float fit = Mathf.Max(trialHalfSize.y + 1.5f, (trialHalfSize.x + 0.7f) / view.aspect);
                float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 1.1f, entranceAge));
                position.x = Mathf.Lerp(position.x, trialCenter.x, weight);
                position.y = Mathf.Lerp(position.y, trialCenter.y - 0.64f, weight);
                view.orthographicSize = Mathf.Lerp(CombatViewSize, fit, weight);
            }
            else trialEntranceStart = float.NegativeInfinity;

            // Smooth the composed shot before shake, using the last displayed position and lens.
            ApplyLockDamping(ref position, groupFraming, drivenByBrain && focus == null && combatBlendOverride < 0);

            shakeOffset = Vector3.zero;
            float elapsed = Time.unscaledTime - shakeStart;
            if (elapsed < shakeDuration)
            {
                float fade = 1f - elapsed / shakeDuration;
                float phase = elapsed * 130f;
                shakeOffset = new Vector3(Mathf.Cos(phase), Mathf.Sin(phase * 1.37f) * 0.65f)
                    * (shakeAmplitude * fade);
            }
            transform.position = position + shakeOffset;
        }

        private void ApplyLockDamping(ref Vector3 position, bool groupFraming, bool canDamp)
        {
            float deltaTime = Time.unscaledDeltaTime;
            float size = view.orthographicSize;
            if (!canDamp) lockDampingWeight = 0f;
            else if (groupFraming) lockDampingWeight = 1f;
            else lockDampingWeight = Mathf.MoveTowards(lockDampingWeight, 0f, deltaTime / LockReturnSeconds);

            if (lockDampingWeight > 0f)
            {
                float smoothTime = lockFramingSmoothTime * lockDampingWeight;
                // SmoothDamp uses the frame interval when updating velocity and needs a positive interval.
                deltaTime = Mathf.Max(deltaTime, 0.00001f);
                position = Vector3.SmoothDamp(previousFramedPosition, position, ref lockPositionVelocity,
                    smoothTime, Mathf.Infinity, deltaTime);
                size = Mathf.SmoothDamp(previousFramedSize, size, ref lockZoomVelocity,
                    smoothTime, Mathf.Infinity, deltaTime);
                view.orthographicSize = size;
            }
            else ResetLockDamping();

            previousFramedPosition = position;
            previousFramedSize = size;
        }

        private void ResetLockDamping()
        {
            lockDampingWeight = 0f;
            lockPositionVelocity = Vector3.zero;
            lockZoomVelocity = 0f;
        }

        private void OnDisable()
        {
            ResetLockDamping();
            UpdateLockFraming();
            ReleaseCombatBlend();
            SetCombatZoom(1f);
            ClearShake();
            EndTrialFraming();
        }

        private void OnDestroy()
        {
            if (lockFraming != null) Destroy(lockFraming);
            if (lockGroup != null) Destroy(lockGroup.gameObject);
        }

        private sealed class CombatShot : ICinemachineCamera
        {
            internal CombatShot(CameraState state) => State = state;

            public string Name => "Card finisher";
            public string Description => string.Empty;
            public CameraState State { get; }
            public bool IsValid => true;
            public ICinemachineMixer ParentCamera => null;
            public void UpdateCameraState(Vector3 worldUp, float deltaTime) { }
            public void OnCameraActivated(ICinemachineCamera.ActivationEventParams evt) { }
        }
    }
}
