using System.Collections.Generic;
using UnityEngine;
using Fighter403.Audio;

namespace Fighter403.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class CombatTarget : MonoBehaviour
    {
        internal static readonly List<CombatTarget> Active = new();
        internal static CombatVitals BattleVitals { get; set; }
        internal static event System.Action EnemyDefeated;
        [SerializeField, Min(1)] private int maxHealth = 100;
        public int Health { get; private set; }
        public int MaxHealth => maxHealth;
        public int LastDamage { get; private set; }
        public bool Alive => isActiveAndEnabled && Health > 0;
        internal CircleCollider2D BodyCollider => hitbox;
        internal virtual bool Anchored => false;
        internal virtual void SetWorldHealthVisible(bool visible) { }
        private SpriteRenderer display;
        private CircleCollider2D hitbox;
        private Color baseColor;
        private float flashUntil;
        private int fractureBonus;
        private double behaviorTime, fractureUntil, slowUntil, stunUntil;
        internal bool Stunned => behaviorTime < stunUntil;
        protected bool Fractured => behaviorTime < fractureUntil;
        internal int PullingEffects { get; set; }
        internal bool BeingPulled => PullingEffects > 0;
        internal float SpeedMultiplier => behaviorTime < slowUntil ? 0.6f : 1f;
        protected static float BehaviorTimeScale => BattleVitals?.SandevistanActive == true
            ? CombatVitals.SandevistanEnemyScale : 1f;

        internal void Fracture(int bonus, float seconds)
        {
            if (Alive && seconds > 0f && !Fractured) GameSfx.Play(Sfx.Fracture);
            fractureBonus = bonus;
            fractureUntil = behaviorTime + seconds;
        }

        internal void Slow(float seconds)
        {
            if (Alive && seconds > 0f && SpeedMultiplier >= 1f) GameSfx.Play(Sfx.Slow);
            slowUntil = System.Math.Max(slowUntil, behaviorTime + seconds);
        }

        internal void Stun(float seconds)
        {
            if (Alive && seconds > 0f && !Stunned) GameSfx.Play(Sfx.Stun);
            stunUntil = System.Math.Max(stunUntil, behaviorTime + seconds);
        }

        protected virtual void Awake()
        {
            display = GetComponent<SpriteRenderer>();
            hitbox = GetComponent<CircleCollider2D>();
            baseColor = display.color;
            ResetTarget();
        }

        protected void OnEnable() => Active.Add(this);
        protected virtual void OnDisable() => Active.Remove(this);
        protected void Update() => behaviorTime += Time.deltaTime * BehaviorTimeScale;


        internal void GetHurtCircle(out Vector2 center, out float radius)
        {
            center = transform.TransformPoint(hitbox.offset);
            Vector3 scale = transform.lossyScale;
            radius = hitbox.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        internal bool Contains(Vector2 worldPoint)
        {
            var local = (Vector2)transform.InverseTransformPoint(new Vector3(worldPoint.x, worldPoint.y, transform.position.z));
            return (local - hitbox.offset).sqrMagnitude <= hitbox.radius * hitbox.radius;
        }

        public bool Hit(int amount) => Hit(amount, 1f);

        internal bool Hit(int amount, float damageMultiplier, bool playBodyImpact = true)
        {
            if (!Alive || amount <= 0) return false;
            if (behaviorTime < fractureUntil) amount += fractureBonus;
            fractureUntil = 0d;
            int damage = MitigateDamage(CombatRules.ScaledPoints(amount, damageMultiplier));
            ResolveDamage(damage, playBodyImpact);
            return true;
        }

        internal bool ExecuteBelowHealth(int percent)
        {
            if (!Alive || Health * 100L >= MaxHealth * (long)percent) return false;
            ResolveDamage(Health, playBodyImpact: false);
            return true;
        }

        private void ResolveDamage(int damage, bool playBodyImpact)
        {
            LastDamage = Mathf.Min(damage, Health);
            Health -= LastDamage;
            bool lethal = Health == 0;
            if (LastDamage > 0)
            {
                flashUntil = Time.unscaledTime + 0.22f;
                if (playBodyImpact) GameSfx.Play(this is TestDummy ? Sfx.DummyHit : Sfx.EnemyHit);
            }
            OnHitResolved(damage);
            if (Health == 0)
            {
                if (this is not FirewallBoss)
                    GameSfx.Play(this switch
                    {
                        TestDummy => Sfx.DummyDown,
                        ProgramEnemy { IsDungeonBoss: true } => Sfx.BossDown,
                        _ => Sfx.EnemyDown,
                    });
                OnDefeated();
                if (lethal && this is not TestDummy) EnemyDefeated?.Invoke();
            }
        }

        internal int Heal(int amount)
        {
            if (!Alive || amount <= 0) return 0;
            int restored = Mathf.Min(amount, MaxHealth - Health);
            Health += restored;
            return restored;
        }

        protected void SetHealthCapacity(int value)
        {
            maxHealth = Mathf.Max(1, value);
            ResetTarget();
        }

        public virtual string DisplayName => "\u6728\u6869";
        protected virtual Color StateColor => baseColor;
        protected virtual int MitigateDamage(int amount) => amount;
        protected virtual void OnHitResolved(int damage) { }
        protected virtual void OnDefeated() { }

        protected void Retire()
        {
            Health = 0;
            OnDefeated();
        }

        public virtual void ResetTarget()
        {
            Health = maxHealth;
            LastDamage = 0;
            flashUntil = 0f;
            behaviorTime = fractureUntil = slowUntil = stunUntil = 0d;
        }

        protected virtual void LateUpdate()
        {
            display.color = Time.unscaledTime < flashUntil ? new Color(1f, 0.41f, 0.35f)
                : !Alive ? new Color(0.34f, 0.34f, 0.36f)
                : Stunned ? new Color(0.89f, 0.61f, 1f)
                : behaviorTime < fractureUntil ? new Color(1f, 0.72f, 0.38f)
                : behaviorTime < slowUntil ? new Color(0.62f, 0.67f, 0.95f) : StateColor;
        }
    }
}
