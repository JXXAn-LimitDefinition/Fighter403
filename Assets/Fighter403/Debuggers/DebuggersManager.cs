using System.Collections.Generic;
using UnityEngine;
using Fighter403.Audio;

namespace Fighter403.Debuggers
{
    public static class DebuggersManager
    {
        // The definition asset is the identity. Its name and icon can change independently.
        private static readonly HashSet<DebuggerDefinition> owned = new();

        public static int OwnedCount => owned.Count;

        public static bool Owns(DebuggerDefinition definition) => definition != null && owned.Contains(definition);

        public static bool TryAcquire(DebuggerDefinition definition)
        {
            if (definition == null || !definition.IsConfigured || !owned.Add(definition)) return false;
            GameSfx.Play(Sfx.DebuggerAcquire);
            return true;
        }

        // The prototype treats each Play session as a new run. Future run flow can call this directly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void BeginRun() => owned.Clear();

        internal static DebuggerDefinition[] Snapshot()
        {
            var result = new DebuggerDefinition[owned.Count];
            owned.CopyTo(result);
            return result;
        }

        internal static void Restore(IEnumerable<DebuggerDefinition> definitions)
        {
            owned.Clear();
            owned.UnionWith(definitions);
        }

        internal static double CastEnergyRecoveryMultiplier
        {
            get
            {
                double multiplier = 1d;
                foreach (var definition in owned)
                {
                    if (definition != null && definition.Effect == DebuggerEffect.CastEnergyRecovery)
                        multiplier += definition.BonusPercent * 0.01d;
                }
                return multiplier;
            }
        }
    }
}
