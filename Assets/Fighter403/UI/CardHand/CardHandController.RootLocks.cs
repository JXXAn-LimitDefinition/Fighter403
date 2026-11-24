using System.Collections.Generic;
using UnityEngine;

namespace Fighter403.UI
{
    public sealed partial class CardHandController
    {
        private readonly List<(HandCard card, double unlockAt)> rootHandLocks = new();
        private Material rootHandLockMaterial;

        internal Material GetRootHandLockMaterial()
        {
            if (rootHandLockMaterial == null)
            {
                var shader = Resources.Load<Shader>("Fighter403RootCardLock");
                if (shader != null) rootHandLockMaterial = new Material(shader);
            }
            return rootHandLockMaterial;
        }

        private void ApplyRootHandLocks(int count, float seconds, System.Random random)
        {
            if (batch == null || count <= 0 || seconds <= 0f) return;
            StepRootHandLocks();

            var candidates = new List<HandCard>(batch.Hand.Count);
            foreach (var card in batch.Hand)
                if (!card.LockedByRoot && !batch.IsSubmitted(card) && batch.CanAffordWithoutQueue(card))
                    candidates.Add(card);

            // Evaluate affordability independently of the staged combo so one replacement always remains.
            int lockCount = Mathf.Min(count, candidates.Count - 1);
            if (lockCount <= 0) return;
            double unlockAt = Time.timeAsDouble + seconds;
            bool dashPathChanged = false;
            for (int i = 0; i < lockCount; i++)
            {
                int choice = random.Next(candidates.Count);
                var locked = candidates[choice];
                candidates.RemoveAt(choice);
                locked.LockedByRoot = true;
                rootHandLocks.Add((locked, unlockAt));
                if (dashDrag == locked) CancelCardDrag();
                if (queueDrag == locked) queueDrag = null;
                if (batch.Remove(locked)) dashPathChanged |= dashPlans.RemoveAll(plan => plan.Card == locked) > 0;
            }

            if (dashPathChanged) RebuildDashPath(0);
            Refresh();
        }

        private void StepRootHandLocks()
        {
            double now = Time.timeAsDouble;
            for (int i = rootHandLocks.Count - 1; i >= 0; i--)
            {
                var entry = rootHandLocks[i];
                if (now < entry.unlockAt) continue;
                entry.card.LockedByRoot = false;
                rootHandLocks.RemoveAt(i);
            }
        }

        private void ClearRootHandLocks()
        {
            if (rootHandLocks.Count == 0) return;
            foreach (var entry in rootHandLocks) entry.card.LockedByRoot = false;
            rootHandLocks.Clear();
            UpdateSelectionStates();
        }
    }
}
