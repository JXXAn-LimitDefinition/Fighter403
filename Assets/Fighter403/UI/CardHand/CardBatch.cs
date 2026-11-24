using System;
using System.Collections.Generic;
using System.Linq;
using Fighter403.Debuggers;
using Fighter403.Player;
using Fighter403.Audio;

namespace Fighter403.UI
{
    internal enum CardKind
    {
        TriangleRift, PhaseDash, OrbitSpin, MatrixGuard, Starfall, Sandevistan, ThousandSlashes,
        QuickSlash, SlashPreparation, LimitedRedBlackHole, DualThread, InfiniteBlueWhiteHole, BigBang, Slash,
        Counterattack, GuardShift
    }
    public enum CardClass { Attack, Defense, Ability, Unspecified }
    internal enum CardSelectionState { Ready, Selected, InsufficientEnergy, Unavailable, Locked }

    internal sealed class HandCard
    {
        public CardDefinition Definition { get; }
        public CardKind Kind => Definition.Kind;
        public CardClass Class => Definition.Class;
        public bool LockedByRoot { get; set; }
        public HandCard(CardDefinition definition)
        {
            Definition = definition;
        }
    }

    internal sealed class CardBatch
    {
        public const int Size = 4;
        public const int PreviewCount = 3;
        private const int ComboResetCardCount = 6;
        private readonly List<HandCard> draw = new(), discard = new(), hand = new(), queue = new();
        private readonly List<HandCard> consumed = new();
        private readonly Random random;
        private readonly CardDefinition thousandSlashes;
        private readonly CardDefinition bigBang;
        private HandCard[] submitted = Array.Empty<HandCard>();
        private int[] submittedCosts = Array.Empty<int>();
        private int next;
        private int cardsPlayed;
        private int currentSpent;
        private bool recoverCurrentGroup;
        private bool submittedPair;
        private readonly int[] comboRuns = new int[(int)CardClass.Unspecified];
        private int consecutiveAttacks;

        public IReadOnlyList<HandCard> Hand => hand;
        public IReadOnlyList<HandCard> Queue => queue;
        public int DrawCount => draw.Count;
        public int DiscardCount => discard.Count;
        internal bool HasAcquiredThousandSlashes { get; private set; }
        public CombatVitals Vitals { get; }
        public float Energy => Vitals.Energy;
        public int ReservedEnergy => TotalCost(queue, ForecastSubmittedAttacks());
        public bool Busy => next < submitted.Length;
        public int CurrentGroupSize => !Busy ? 0 : submittedPair && next == 0 ? 2 : 1;
        public HandCard CurrentAt(int offset) => submitted[next + offset];
        public bool IsSubmitted(HandCard card) => Array.IndexOf(submitted, card, next + currentSpent) >= 0;

        public HandCard Upcoming(int index) => index >= 0 && index < draw.Count ? draw[draw.Count - 1 - index] : null;

        public void StepEnergy(double realSeconds)
        {
            double multiplier = currentSpent > 0 ? DebuggersManager.CastEnergyRecoveryMultiplier : 1d;
            Vitals.StepEnergy(realSeconds, multiplier);
        }

        public CardBatch(Random random, IReadOnlyList<CardDefinition> definitions, CombatVitals vitals = null,
            IReadOnlyList<CardDefinition> initialDeck = null)
        {
            this.random = random;
            Vitals = vitals ?? new CombatVitals();
            foreach (var definition in definitions)
            {
                if (definition.Kind == CardKind.ThousandSlashes) thousandSlashes ??= definition;
                if (definition.IsTemporary)
                {
                    bigBang ??= definition;
                    continue;
                }
                int copies = definition.Kind == CardKind.ThousandSlashes || definition.ConsumeUntilKill ? 1
                    : definition.Kind is CardKind.TriangleRift or CardKind.PhaseDash ? 3 : 2;
                for (int i = 0; initialDeck == null && i < copies; ++i)
                    if (TryAcquire(definition.Kind)) draw.Add(new HandCard(definition));
            }
            if (initialDeck != null)
                foreach (var definition in initialDeck)
                    if (TryAcquire(definition.Kind)) draw.Add(new HandCard(definition));
            Shuffle(draw);
            FillHand();
        }

        internal bool AddToDeck(CardDefinition definition)
        {
            if (!TryAcquire(definition.Kind)) return false;
            var card = new HandCard(definition);
            if (hand.Count < Size) hand.Add(card);
            else draw.Insert(0, card);
            return true;
        }

        private bool TryAcquire(CardKind kind)
        {
            if (kind == CardKind.BigBang) return false;
            if (kind != CardKind.ThousandSlashes) return true;
            if (HasAcquiredThousandSlashes) return false;
            HasAcquiredThousandSlashes = true;
            return true;
        }

        internal IEnumerable<CardKind> DeckKinds()
        {
            return hand.Concat(draw).Concat(discard).Concat(consumed)
                .Concat(submitted.Skip(next).Take(currentSpent))
                .Where(card => !card.Definition.IsTemporary).Select(card => card.Kind);
        }

        internal bool RemoveFromDeck(CardKind kind)
        {
            if (kind == CardKind.BigBang || Busy || DeckKinds().Count() <= 1) return false;
            foreach (var pile in new[] { draw, discard, consumed, hand })
            {
                int index = pile.FindIndex(card => card.Kind == kind);
                if (index < 0) continue;
                queue.Remove(pile[index]);
                pile.RemoveAt(index);
                RestockPreview();
                FillHand();
                return true;
            }
            return false;
        }

        public int CountInPile(bool discarded, CardKind kind) => (discarded ? discard : draw).FindAll(c => c.Kind == kind).Count;
        public float MultiplierForCurrent(int offset)
        {
            var definition = CurrentAt(offset).Definition;
            int longestRun = 0;
            for (int i = 0; i < comboRuns.Length; i++)
            {
                var attribute = (CardClass)i;
                if (!definition.CountsAsComboClass(attribute)) continue;
                int run = comboRuns[i];
                // Paired cards share their action frame and follow queue order for combo counting.
                for (int previous = 0; previous < offset; previous++)
                    run = (cardsPlayed + previous + 1) % ComboResetCardCount == 0
                        ? 0 : ComboRunAfter(CurrentAt(previous).Definition, attribute, run);
                longestRun = Math.Max(longestRun, run);
            }
            return CombatRules.NextMultiplier(longestRun) * Vitals.DungeonDamageMultiplier;
        }

        private static int ComboRunAfter(CardDefinition definition, CardClass attribute, int run) =>
            definition.CountsAsComboClass(attribute)
                ? run + Math.Min(CombatRules.SameClassCardsForMaxMultiplier - run, definition.ComboCount)
                : 0;
        public bool IsQueued(HandCard card) => queue.Contains(card);

        internal bool CanAffordWithoutQueue(HandCard card) =>
            Vitals.CanSpendEnergy(CostAfterAttacks(card, ForecastSubmittedAttacks()));

        public CardSelectionState GetSelectionState(HandCard card)
        {
            if (!hand.Contains(card) || IsSubmitted(card))
                return CardSelectionState.Unavailable;
            if (card.LockedByRoot) return CardSelectionState.Locked;
            if (IsQueued(card)) return CardSelectionState.Selected;
            return Vitals.CanSpendEnergy(ReservedEnergy + PlannedEnergyCost(card))
                ? CardSelectionState.Ready : CardSelectionState.InsufficientEnergy;
        }

        public bool Select(HandCard card)
        {
            if (GetSelectionState(card) != CardSelectionState.Ready) return false;
            queue.Add(card);
            return true;
        }

        public bool Remove(HandCard card) => queue.Remove(card);
        public void Clear() => queue.Clear();

        internal void ExpireTemporaryCards()
        {
            queue.RemoveAll(card => card.Definition.IsTemporary);
            if (hand.RemoveAll(card => card.Definition.IsTemporary) > 0)
                FillHand();
        }

        public void Reorder(HandCard card, int destination)
        {
            if (!queue.Remove(card)) return;
            queue.Insert(Math.Max(0, Math.Min(destination, queue.Count)), card);
        }

        public bool Submit()
        {
            if (Busy || queue.Count == 0 || queue.Exists(card => card.LockedByRoot)) return false;
            var costs = new int[queue.Count];
            if (!Vitals.SpendEnergy(TotalCost(queue, consecutiveAttacks, costs))) return false;
            submittedPair = Vitals.DualThreadRemaining > 0d && queue.Count >= 2;
            Vitals.ClearDualThread();
            submitted = queue.ToArray();
            submittedCosts = costs;
            next = 0;
            Clear();
            return true;
        }

        public bool SubmitSingle(HandCard card)
        {
            if (Busy || IsQueued(card) || GetSelectionState(card) is CardSelectionState.Unavailable or CardSelectionState.Locked)
                return false;
            int cost = CostAfterAttacks(card, consecutiveAttacks);
            int queuedCost = TotalCost(queue, AttackCountAfter(card, consecutiveAttacks));
            if (!Vitals.CanSpendEnergy(cost + queuedCost) || !Vitals.SpendEnergy(cost)) return false;
            submittedPair = false;
            Vitals.ClearDualThread();
            submitted = new[] { card };
            submittedCosts = new[] { cost };
            next = 0;
            return true;
        }

        public bool TryPlayCurrentGroup(int[] costs, HandCard[] replacements)
        {
            if (!Busy || currentSpent > 0) return false;
            int attacks = consecutiveAttacks;
            int adjustment = 0;
            for (int i = 0; i < CurrentGroupSize; i++)
            {
                var card = CurrentAt(i);
                if (!hand.Contains(card) || card.LockedByRoot) return false;
                costs[i] = CostAfterAttacks(card, attacks);
                attacks = AttackCountAfter(card, attacks);
                adjustment += costs[i] - submittedCosts[next + i];
            }

            if (adjustment > 0 && !Vitals.SpendEnergy(adjustment)) return false;
            if (adjustment < 0) Vitals.RefundEnergy(-adjustment);
            bool createsBigBang = bigBang != null && CurrentGroupSize == 2 &&
                ((CurrentAt(0).Kind == CardKind.InfiniteBlueWhiteHole && CurrentAt(1).Kind == CardKind.LimitedRedBlackHole) ||
                 (CurrentAt(1).Kind == CardKind.InfiniteBlueWhiteHole && CurrentAt(0).Kind == CardKind.LimitedRedBlackHole));
            for (int i = 0; i < CurrentGroupSize; i++)
            {
                var card = CurrentAt(i);
                replacements[i] = PlayCard(card, createsBigBang && card.Kind == CardKind.InfiniteBlueWhiteHole);
            }
            return true;
        }

        private HandCard PlayCard(HandCard card, bool createsBigBang)
        {
            HandCard replacement = null;
            int slot = hand.IndexOf(card);
            hand.RemoveAt(slot);
            // Keep this copy in the submitted group until the action finishes.
            currentSpent++;

            int attacksAfter = AttackCountAfter(card, consecutiveAttacks);
            if (createsBigBang)
                replacement = new HandCard(bigBang);
            else if (thousandSlashes != null && HasAcquiredThousandSlashes &&
                consecutiveAttacks < thousandSlashes.EnergyCost && attacksAfter >= thousandSlashes.EnergyCost)
                replacement = DrawThousandSlashes();

            if (replacement != null)
                GameSfx.Play(Sfx.CardGuaranteed);
            else
            {
                if (draw.Count == 0) RestockPreview();
                if (draw.Count > 0)
                {
                    replacement = DrawNext();
                    GameSfx.Play(Sfx.CardDraw);
                }
            }

            consecutiveAttacks = attacksAfter;
            if (replacement != null) hand.Insert(slot, replacement);
            RestockPreview();
            return replacement;
        }

        public bool RecoverConsumed()
        {
            bool recoveringGroup = !recoverCurrentGroup &&
                submitted.Skip(next).Take(currentSpent).Any(card => card.Definition.ConsumeUntilKill);
            recoverCurrentGroup |= recoveringGroup;
            if (consumed.Count == 0) return recoveringGroup;
            RestoreConsumedToDraw();
            FillHand();
            return true;
        }

        private void RestoreConsumedToDraw()
        {
            // Add recovered cards below the existing draw order, including the visible preview.
            draw.InsertRange(0, consumed);
            consumed.Clear();
        }

        public void CompleteCurrent(bool executed)
        {
            if (!Busy) return;
            int count = CurrentGroupSize;
            bool spent = currentSpent > 0;
            for (int i = 0; i < count; i++)
            {
                if (!spent)
                {
                    Vitals.RefundEnergy(submittedCosts[next + i]);
                    continue;
                }
                var card = CurrentAt(i);
                var definition = card.Definition;
                if (!definition.IsTemporary)
                    (definition.ConsumeUntilKill ? consumed : discard).Add(card);
                if (executed)
                    for (int attribute = 0; attribute < comboRuns.Length; attribute++)
                        comboRuns[attribute] = ComboRunAfter(definition, (CardClass)attribute, comboRuns[attribute]);
                if (++cardsPlayed == ComboResetCardCount)
                {
                    cardsPlayed = 0;
                    Array.Clear(comboRuns, 0, comboRuns.Length);
                }
            }
            next += count;
            currentSpent = 0;
            if (!spent) return;
            if (recoverCurrentGroup)
            {
                RestoreConsumedToDraw();
                recoverCurrentGroup = false;
            }
            FillHand();
            RestockPreview();
        }

        public int PlannedEnergyCost(HandCard card)
        {
            int attacks = consecutiveAttacks;
            for (int i = next + currentSpent; i < submitted.Length; i++)
            {
                if (submitted[i] == card) return CostAfterAttacks(card, attacks);
                attacks = AttackCountAfter(submitted[i], attacks);
            }
            foreach (var queued in queue)
            {
                if (queued == card) break;
                attacks = AttackCountAfter(queued, attacks);
            }
            return CostAfterAttacks(card, attacks);
        }

        private int ForecastSubmittedAttacks()
        {
            int attacks = consecutiveAttacks;
            for (int i = next + currentSpent; i < submitted.Length; i++)
                attacks = AttackCountAfter(submitted[i], attacks);
            return attacks;
        }

        private int TotalCost(IReadOnlyList<HandCard> cards, int attacks, int[] costs = null)
        {
            int total = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                int cost = CostAfterAttacks(cards[i], attacks);
                if (costs != null) costs[i] = cost;
                total += cost;
                attacks = AttackCountAfter(cards[i], attacks);
            }
            return total;
        }

        private static int CostAfterAttacks(HandCard card, int attacks) => card.Kind == CardKind.ThousandSlashes
            ? Math.Max(0, card.Definition.EnergyCost - attacks) : card.Definition.EnergyCost;

        private int AttackCountAfter(HandCard card, int attacks)
        {
            if (thousandSlashes == null || card.Kind == CardKind.ThousandSlashes ||
                !card.Definition.CountsAsComboClass(CardClass.Attack)) return 0;
            int limit = thousandSlashes.EnergyCost;
            return attacks + Math.Min(limit - attacks, card.Definition.ComboCount);
        }

        private static bool IsThousandSlashes(HandCard card) => card.Kind == CardKind.ThousandSlashes;

        private HandCard DrawThousandSlashes() => TakeCard(draw, draw.FindLastIndex(IsThousandSlashes))
            ?? TakeCard(discard, discard.FindLastIndex(IsThousandSlashes));

        private HandCard DrawNext() => TakeCard(draw, draw.Count - 1);

        private static HandCard TakeCard(List<HandCard> pile, int index)
        {
            if (index < 0) return null;
            var card = pile[index];
            pile.RemoveAt(index);
            return card;
        }

        private void FillHand()
        {
            while (hand.Count < Size)
            {
                if (draw.Count == 0) RestockPreview();
                if (draw.Count == 0) return;
                hand.Add(DrawNext());
            }
        }

        private void RestockPreview()
        {
            if (draw.Count >= PreviewCount || discard.Count == 0) return;
            // Keep previously revealed draws at the top when recycling the discard pile.
            Shuffle(discard);
            draw.InsertRange(0, discard);
            discard.Clear();
            GameSfx.Play(Sfx.CardShuffle);
        }

        private void Shuffle(List<HandCard> pile)
        {
            for (int i = pile.Count - 1; i > 0; --i)
            {
                int j = random.Next(i + 1);
                (pile[j], pile[i]) = (pile[i], pile[j]);
            }
        }
    }
}
