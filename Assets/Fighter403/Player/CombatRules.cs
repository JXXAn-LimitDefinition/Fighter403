using System;
using System.Collections.Generic;
using UnityEngine;
using Fighter403.Audio;

namespace Fighter403.Player
{
    internal sealed class CombatVitals
    {
        public const float SandevistanDamageMultiplier = 1.5f;
        public const float SandevistanEnemyScale = 0.05f;
        private readonly float energyRecoveryPerSecond;
        private double blockDecayPause;
        private double sandevistanStartedAt;
        private double sandevistanUntil;
        private double dualThreadUntil;
        private double counterattackUntil, counterattackMomentUntil;
        private double pausedEffectTime = -1d, pausedEffectSeconds;
        private double EffectTime => (pausedEffectTime >= 0d ? pausedEffectTime : Time.unscaledTimeAsDouble) - pausedEffectSeconds;
        private readonly Queue<(CombatTarget Source, int Points)> counterattacks = new();
        public int MaxHealth { get; }
        public int MaxEnergy { get; }
        public int MaxBlock { get; private set; }
        public double BlockDecayPerSecond { get; }
        public double Health { get; private set; }
        internal float DungeonDamageMultiplier { get; set; } = 1f;
        public double Block { get; private set; }
        public float Energy { get; private set; }
        public double SandevistanRemaining => Math.Max(0d, sandevistanUntil - EffectTime);
        public float SandevistanElapsed => (float)(EffectTime - sandevistanStartedAt);
        public bool SandevistanActive => SandevistanRemaining > 0d;
        private double killSlowMotionUntil;
        internal bool KillSlowMotionActive => EffectTime < killSlowMotionUntil;
        public double DualThreadRemaining => Math.Max(0d, dualThreadUntil - EffectTime);
        internal double CounterattackRemaining => Math.Max(0d, counterattackUntil - EffectTime);
        internal double CounterattackMomentRemaining => Math.Max(0d, counterattackMomentUntil - EffectTime);
        internal int CounterattackBonus => CounterattackMomentRemaining > 0d ? (int)Math.Floor(Block * 0.5d + 0.5d) : 0;
        internal event Action<Vector2, bool> Impact;

        internal void PauseEffects() => pausedEffectTime = Time.unscaledTimeAsDouble;

        internal void ResumeEffects()
        {
            if (pausedEffectTime < 0d) return;
            pausedEffectSeconds += Time.unscaledTimeAsDouble - pausedEffectTime;
            pausedEffectTime = -1d;
        }

        internal void ActivateKillSlowMotion(float seconds) => killSlowMotionUntil = EffectTime + seconds;
        internal void ClearKillSlowMotion() => killSlowMotionUntil = 0d;

        public void ActivateDualThread(float realSeconds) => dualThreadUntil = EffectTime + realSeconds;
        public void ClearDualThread() => dualThreadUntil = 0d;

        internal void ActivateCounterattack(float realSeconds) => counterattackUntil = EffectTime + realSeconds;
        internal void ActivateCounterattackMoment(float realSeconds) => counterattackMomentUntil = EffectTime + realSeconds;
        internal bool TryTakeCounterattack(out (CombatTarget Source, int Points) hit) => counterattacks.TryDequeue(out hit);

        internal void ClearCounterattacks()
        {
            counterattackUntil = counterattackMomentUntil = 0d;
            counterattacks.Clear();
        }

        public void ActivateSandevistan(float realSeconds)
        {
            sandevistanStartedAt = EffectTime;
            sandevistanUntil = sandevistanStartedAt + realSeconds;
        }
        public void ClearSandevistan() => sandevistanUntil = 0d;

        public CombatVitals(PlayerBaseStats stats = null)
        {
            bool hasStats = stats != null;
            MaxHealth = hasStats ? stats.MaxHealth : PlayerBaseStats.DefaultMaxHealth;
            MaxEnergy = hasStats ? stats.MaxEnergy : PlayerBaseStats.DefaultMaxEnergy;
            BlockDecayPerSecond = hasStats ? stats.BlockDecayPerSecond : PlayerBaseStats.DefaultBlockDecayPerSecond;
            energyRecoveryPerSecond = hasStats ? stats.EnergyRecoveryPerSecond : PlayerBaseStats.DefaultEnergyRecoveryPerSecond;
            Health = MaxHealth;
            Energy = MaxEnergy;
            SetMaxBlock(hasStats ? stats.MaxBlock : PlayerBaseStats.DefaultMaxBlock);
        }

        public void SetMaxBlock(int maximum)
        {
            MaxBlock = Math.Max(0, maximum);
            Block = Math.Min(Block, MaxBlock);
        }

        public void AddBlock(int amount)
        {
            if (amount > 0 && Health > 0) Block = Math.Min(MaxBlock, Block + amount);
        }

        public void PauseBlockDecay(double gameSeconds) => blockDecayPause = Math.Max(blockDecayPause, gameSeconds);

        public void StepBlock(double gameSeconds)
        {
            if (gameSeconds <= 0) return;
            double pause = Math.Min(gameSeconds, blockDecayPause);
            blockDecayPause -= pause;
            Block = Math.Max(0, Block - BlockDecayPerSecond * (gameSeconds - pause));
        }

        public void StepEnergy(double realSeconds, double recoveryMultiplier = 1d)
        {
            if (realSeconds <= 0 || Energy >= MaxEnergy) return;
            Energy = (float)Math.Min(MaxEnergy, Energy + realSeconds * energyRecoveryPerSecond * recoveryMultiplier);
        }

        public bool CanSpendEnergy(float amount) => amount >= 0f && Energy + 0.0001f >= amount;

        public bool SpendEnergy(float amount)
        {
            if (!CanSpendEnergy(amount)) return false;
            Energy = Math.Max(0f, Energy - amount);
            return true;
        }

        public void RefundEnergy(float amount) => Energy = Math.Min(MaxEnergy, Energy + amount);

        public void TakeDamage(int amount) => TakeDamageFrom(amount, Vector2.zero);

        internal void TakeDamageFrom(int amount, Vector2 direction, Sfx healthImpact = Sfx.PlayerHurt, CombatTarget source = null)
        {
            if (amount <= 0 || Health == 0) return;
            // Resolve after the enemy's attack update, which may still be iterating its projectiles or summons.
            if (source != null && source.Alive && CounterattackRemaining > 0d)
                counterattacks.Enqueue((source, CombatRules.ScaledPoints(amount, 0.5f)));

            double absorbed = Math.Min(amount, Block);
            Block -= absorbed;
            if (absorbed > 0) GameSfx.Play(Block <= 0 ? Sfx.PlayerBlockBreak : Sfx.PlayerBlock);
            if (amount > absorbed)
            {
                Health = Math.Max(0, Health - (amount - absorbed));
                if (Health == 0) GameSfx.Play(Sfx.PlayerDown);
                else if (absorbed == 0d) GameSfx.Play(healthImpact);
            }
            Impact?.Invoke(direction, amount > absorbed);
        }

        internal void ResetHealth()
        {
            Health = MaxHealth;
            Block = blockDecayPause = 0;
            ClearCounterattacks();
        }

        internal void HealPercent(float fraction)
        {
            if (Health <= 0 || Health >= MaxHealth || !(fraction > 0)) return;
            Health = Math.Min(MaxHealth, Health + MaxHealth * fraction);
            GameSfx.Play(Sfx.PlayerHeal);
        }
    }

    internal enum MeleePhase { Chase, Windup, Cooldown }

    internal sealed class MeleeCycle
    {
        public const float Range = 1.25f, WindupSeconds = 0.65f, CooldownSeconds = 1.15f;
        public const int Damage = 12;
        public MeleePhase Phase { get; private set; }
        private float remaining;
        private readonly float windup;
        private readonly float cooldown;

        public MeleeCycle(float windup = WindupSeconds, float cooldown = CooldownSeconds)
        {
            this.windup = windup;
            this.cooldown = cooldown;
        }

        public bool Step(float gameSeconds, bool inRange)
        {
            switch (Phase)
            {
                case MeleePhase.Chase:
                    if (inRange) { Phase = MeleePhase.Windup; remaining = windup; }
                    return false;
                case MeleePhase.Windup:
                    remaining -= gameSeconds;
                    if (remaining > 0.000001f) return false;
                    Phase = MeleePhase.Cooldown;
                    remaining = cooldown;
                    return inRange;
                default:
                    remaining -= gameSeconds;
                    if (remaining <= 0.000001f) Phase = MeleePhase.Chase;
                    return false;
            }
        }

        public void Reset() { Phase = MeleePhase.Chase; remaining = 0; }
    }

    internal static class CombatRules
    {
        public const int SameClassCardsForMaxMultiplier = 3;

        public static bool InRange(float squaredDistance, float range) => squaredDistance <= range * range;
        public static bool InArc(float squaredDistance, float angle, float range, float halfAngle) =>
            InRange(squaredDistance, range) && angle <= halfAngle;
        public static float NextMultiplier(int previousSameClass) => previousSameClass switch
        {
            <= 0 => 1f,
            1 => 1.25f,
            2 => 1.5f,
            _ => 2f,
        };
        public static int ScaledPoints(int points, float multiplier) => (int)Math.Floor(points * multiplier + 0.5f);
    }
}
