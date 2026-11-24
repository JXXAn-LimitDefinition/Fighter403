using UnityEngine;

namespace Fighter403.Debuggers
{
    public enum DebuggerEffect
    {
        None,
        CastEnergyRecovery
    }

    public sealed class DebuggerDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private DebuggerEffect effect;
        [SerializeField, Min(0f)] private float bonusPercent;

        public string DisplayName => displayName?.Trim();
        public Sprite Icon => icon;
        public DebuggerEffect Effect => effect;
        public float BonusPercent => float.IsFinite(bonusPercent) ? Mathf.Max(0f, bonusPercent) : 0f;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(displayName) && icon != null;
    }
}
