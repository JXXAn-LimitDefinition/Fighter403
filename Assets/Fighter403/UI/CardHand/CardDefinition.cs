using System.Globalization;
using Fighter403.Player;
using TMPro;
using UnityEngine;

namespace Fighter403.UI
{
    public enum CardHitShape
    {
        None,
        Arc,
        Circle,
        Dash,
        Lock,
        Triangle,
        Landing
    }

    [DisallowMultipleComponent, RequireComponent(typeof(CardHoverVisual))]
    public sealed class CardDefinition : MonoBehaviour
    {
        [SerializeField] private CardKind kind;
        [SerializeField] private TMP_Text title;
        [SerializeField] private CardClass majorAttribute = CardClass.Unspecified;
        [SerializeField] private CardClass minorAttribute = CardClass.Unspecified;
        [SerializeField] private CardClass secondMinorAttribute = CardClass.Unspecified;
        [SerializeField, Min(1)] private int comboCount = 1;
        [SerializeField] private bool countMinorAttributesForCombo;
        [SerializeField, Min(0)] private int energyCost = 1;
        [SerializeField] private bool consumeUntilKill;
        [SerializeField] private CardHitShape hitShape = CardHitShape.Arc;
        [SerializeField] private bool shapeConfigured;
        [SerializeField, Min(0)] private float distance;
        [SerializeField, Min(0)] private float pushDistance = 3f;
        [SerializeField, Range(0, 360)] private float sweepDegrees = 110;
        [SerializeField, Min(0)] private float dashHalfWidth = 0.58f;
        // Legacy serialized values retained for prefab compatibility; Dash no longer teleports.
        [SerializeField, HideInInspector, Min(0)] private float blinkOffset = 0.95f;
        [SerializeField, Min(0)] private int damage;
        [SerializeField, HideInInspector, Min(0)] private int blinkDamage;
        [SerializeField, Min(0)] private int block;
        [SerializeField, Min(0)] private float impactRadius;
        [SerializeField, Min(1)] private int pulses = 1;
        [SerializeField, Min(0)] private int finisherDamage;
        [SerializeField, Min(0)] private int fractureBonus;
        [SerializeField, Min(0)] private float effectSeconds;
        [SerializeField, Min(0)] private int startupFrames;
        [SerializeField, Min(1)] private int activeFrames = 1;
        [SerializeField, Min(0)] private int recoveryFrames;
        [SerializeField] private bool timelineConfigured;
        [SerializeField, HideInInspector, Min(0)] private int hitFrame;
        [SerializeField, HideInInspector, Min(1)] private int endFrame = 1;
        [SerializeField, Min(0)] private float shakeAmplitude;
        [SerializeField, Min(0)] private float shakeSeconds;
        [SerializeField, Min(0)] private float hitStopMs;
        [SerializeField, Range(0, 1)] private float hitStopScale;
        [SerializeField, Min(0)] private int slashStartFrame;
        [SerializeField] private Sprite[] slashFront, slashBack;
        [SerializeField] private Sprite[] blinkEffect;
        [SerializeField] private Sprite[] finisherEffect;
        [SerializeField] private Color guardTint = new(0.65f, 0.93f, 1f);
        [SerializeField] private SandevistanVfxSettings sandevistanVfx = new();

        internal CardKind Kind => kind;
        public string DisplayName => title != null ? title.text : gameObject.name;
        internal int EnergyCost => Mathf.Max(0, energyCost);
        internal bool ConsumeUntilKill => consumeUntilKill;
        internal bool IsTemporary => kind == CardKind.BigBang;
        internal CardClass Class => majorAttribute != CardClass.Unspecified ? majorAttribute
            : kind == CardKind.MatrixGuard ? CardClass.Defense : CardClass.Attack;
        internal CardClass MinorAttribute => minorAttribute;
        internal CardClass SecondMinorAttribute => secondMinorAttribute;
        internal float TriangleHalfWidth => distance * Mathf.Tan(Mathf.Min(HalfAngle, 85f) * Mathf.Deg2Rad);
        internal float ImpactRadius => impactRadius;
        internal int Pulses => kind is CardKind.OrbitSpin or CardKind.ThousandSlashes
            ? Mathf.Clamp(pulses, 1, ActiveFrames) : 1;
        internal int FinisherDamage => finisherDamage;
        internal int FractureBonus => fractureBonus;
        internal float EffectSeconds => effectSeconds;
        public float EditorImpactRadius => impactRadius;
        public float EditorTriangleHalfWidth => TriangleHalfWidth;
        internal int ComboCount => Mathf.Max(1, comboCount);
        internal bool CountsAsComboClass(CardClass attribute) =>
            attribute != CardClass.Unspecified && (kind == CardKind.DualThread || Class == attribute ||
                (kind == CardKind.InfiniteBlueWhiteHole && attribute == CardClass.Defense) ||
                (countMinorAttributesForCombo && (minorAttribute == attribute || secondMinorAttribute == attribute)));
        internal bool IsDirectional => kind is CardKind.TriangleRift or CardKind.PhaseDash
            or CardKind.QuickSlash or CardKind.SlashPreparation or CardKind.Slash;
        internal bool IsTargeted => kind is CardKind.Starfall or CardKind.ThousandSlashes;
        internal float Distance => distance;
        internal float PushDistance => pushDistance;
        internal float HalfAngle => sweepDegrees * 0.5f;
        internal float DashHalfWidth => dashHalfWidth;
        internal int Damage => damage;
        internal int Block => block;
        internal int StartupFrames => timelineConfigured ? Mathf.Max(0, startupFrames) : Mathf.Max(0, hitFrame);
        internal int ActiveFrames => timelineConfigured ? Mathf.Max(1, activeFrames) : 1;
        internal int RecoveryFrames => timelineConfigured
            ? Mathf.Max(0, recoveryFrames)
            : Mathf.Max(0, endFrame - StartupFrames - ActiveFrames);
        internal int ActiveStartFrame => kind == CardKind.PhaseDash ? Mathf.Max(1, StartupFrames) : StartupFrames;
        internal int ActiveEndFrame => ActiveStartFrame + ActiveFrames;
        internal int HitFrame => ActiveStartFrame;
        internal int EndFrame => ActiveEndFrame + RecoveryFrames;
        internal float ShakeAmplitude => shakeAmplitude;
        internal float ShakeSeconds => shakeSeconds;
        internal float HitStopSeconds => hitStopMs * 0.001f;
        internal float HitStopScale => hitStopScale;
        internal int SlashStartFrame => slashStartFrame;
        internal Sprite[] SlashFront => slashFront;
        internal Sprite[] SlashBack => slashBack;
        internal Sprite[] BlinkEffect => blinkEffect;
        internal Sprite[] FinisherEffect => finisherEffect;
        internal Color GuardTint => guardTint;
        internal SandevistanVfxSettings SandevistanVfx => sandevistanVfx;
        internal CardHitShape HitShape => shapeConfigured ? hitShape : DefaultHitShape(kind);

        public int TimelineStartupFrames => StartupFrames;
        public int TimelineActiveFrames => ActiveFrames;
        public int TimelineRecoveryFrames => RecoveryFrames;
        public int TimelineActiveStartFrame => ActiveStartFrame;
        public int TimelineActiveEndFrame => ActiveEndFrame;
        public int TimelineTotalFrames => EndFrame;
        public CardHitShape EditorHitShape => HitShape;
        public float EditorDistance => distance;
        public float EditorSweepDegrees => sweepDegrees;
        public float EditorDashHalfWidth => dashHalfWidth;
        public int EditorSlashStartFrame => slashStartFrame;
        public Sprite[] EditorSlashFront => slashFront;
        public Sprite[] EditorSlashBack => slashBack;

        public void EnsureEditorData()
        {
            if (!timelineConfigured)
            {
                startupFrames = Mathf.Max(0, hitFrame);
                activeFrames = 1;
                recoveryFrames = Mathf.Max(0, endFrame - startupFrames - activeFrames);
                timelineConfigured = true;
            }

            if (!shapeConfigured)
            {
                hitShape = DefaultHitShape(kind);
                shapeConfigured = true;
            }

            startupFrames = Mathf.Max(kind == CardKind.PhaseDash ? 1 : 0, startupFrames);
            activeFrames = Mathf.Max(1, activeFrames);
            recoveryFrames = Mathf.Max(0, recoveryFrames);
            energyCost = Mathf.Max(0, energyCost);
        }

        private void OnValidate() => EnsureEditorData();

        private static CardHitShape DefaultHitShape(CardKind value) => value switch
        {
            CardKind.TriangleRift or CardKind.QuickSlash or CardKind.SlashPreparation or CardKind.Slash => CardHitShape.Arc,
            CardKind.PhaseDash => CardHitShape.Dash,
            CardKind.OrbitSpin or CardKind.LimitedRedBlackHole or CardKind.InfiniteBlueWhiteHole => CardHitShape.Circle,
            CardKind.MatrixGuard or CardKind.Counterattack or CardKind.GuardShift => CardHitShape.None,
            CardKind.Starfall or CardKind.ThousandSlashes => CardHitShape.Lock,
            _ => CardHitShape.None
        };

        internal string Describe()
        {
            string units = distance.ToString("0.###", CultureInfo.InvariantCulture);
            string seconds = effectSeconds.ToString("0.##", CultureInfo.InvariantCulture);
            string description = kind switch
            {
                CardKind.TriangleRift when HitShape == CardHitShape.Triangle =>
                    $"前方 {units}，伤害 {damage}\n{seconds} 秒内下次受击 +{fractureBonus}",
                CardKind.TriangleRift => $"前方 {sweepDegrees:0.#}°，半径 {units}\n伤害 {damage}",
                CardKind.QuickSlash => $"对前方小范围造成 {damage} 点伤害",
                CardKind.Slash => $"斩击前方敌人，造成 {damage} 点伤害",
                CardKind.SlashPreparation => $"对前方小范围造成 {damage} 点伤害\n此牌视为打出 {ComboCount} 张三角攻击牌",
                CardKind.PhaseDash => $"点选朝敌人突进，拖放指定落点\n最远 {units}，沿途造成 {damage} 点伤害",
                CardKind.OrbitSpin when Pulses > 1 => $"半径 {units}，{damage} 伤害 × {Pulses}\n减速 40%，持续 {seconds} 秒",
                CardKind.OrbitSpin => $"半径 {units}\n伤害 {damage}",
                CardKind.Counterattack => $"获得 {block} 点格挡\n{seconds} 秒内将所受伤害的 50% 反弹给伤害来源（含格挡吸收）",
                CardKind.GuardShift => $"获得反击时刻，持续 {seconds} 秒\n伤害额外附加当前格挡值的 50%\n此牌同时计作防御、能力和攻击",
                CardKind.MatrixGuard when effectSeconds > 0 => $"获得 {block} 格挡\n{seconds} 秒内格挡停止衰减",
                CardKind.MatrixGuard => $"获得 {block} 格挡\n格挡随时间衰减",
                CardKind.Starfall when HitShape == CardHitShape.Landing =>
                    $"锁定范围 {units}，落点半径 {impactRadius:0.##}\n{damage} 伤害，眩晕 {seconds} 秒",
                CardKind.Sandevistan =>
                    $"{seconds} 秒内伤害 +{(CombatVitals.SandevistanDamageMultiplier - 1f):P0}\n敌人 {CombatVitals.SandevistanEnemyScale:0.##}×，玩家 1×",
                CardKind.DualThread => $"获得双线程（{seconds} 秒，最多 1 层）\n下次出牌时消耗。若出两张或以上，前两张同时打出并生效\n被动：视为攻击、防御、能力牌各打出一次",
                CardKind.ThousandSlashes =>
                    $"{damage}×{Pulses - 1} ＋ 终斩 {FinisherDamage}\n连攻每计1张减1费\n拥有后首次0费必抽\n本局最多获得1张",
                CardKind.LimitedRedBlackHole =>
                    $"吸引半径 {units} 内的敌人\n{damage} 点伤害，眩晕 {seconds} 秒" +
                    (CountsAsComboClass(CardClass.Attack) ? "\n同时计作攻击和能力" : ""),
                CardKind.InfiniteBlueWhiteHole =>
                    $"推出半径 {units} 内的敌人，获得 {block} 点护盾\n同时计作能力和防御\n在打出双线程后，同时打出此牌与红色黑洞可以获得大爆炸。",
                CardKind.BigBang =>
                    $"斩杀生命低于 50% / 25% / 10% 的普通 / 精英 / 首领敌人\n每次斩杀分别回复 5% / 10% / 20% 最大生命\n若无斩杀，对所有敌人造成 {damage} 点伤害，回满护盾\n仅由双线程红黑洞与蓝白洞同出时生成",
                _ => $"锁定范围 {units}，伤害 {damage}\n无有效目标时保留此牌"
            };
            return ConsumeUntilKill ? description + "\n击杀后回抽牌堆" : description;
        }
    }
}
