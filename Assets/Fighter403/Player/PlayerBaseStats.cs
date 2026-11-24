using UnityEngine;

namespace Fighter403.Player
{
    [CreateAssetMenu(fileName = "PlayerBaseStats", menuName = "Fighter403/玩家基础属性")]
    public sealed class PlayerBaseStats : ScriptableObject
    {
        internal const int DefaultMaxHealth = 100;
        internal const int DefaultMaxEnergy = 10;
        internal const int DefaultMaxBlock = 30;
        internal const float DefaultEnergyRecoveryPerSecond = 1f;
        internal const float DefaultBlockDecayPerSecond = 0.5f;
        internal const float DefaultMoveSpeed = 4f;

        [SerializeField, Min(1), InspectorName("生命上限")]
        private int maxHealth = DefaultMaxHealth;

        [SerializeField, Min(1), InspectorName("能量上限")]
        private int maxEnergy = DefaultMaxEnergy;

        [SerializeField, Min(0f), InspectorName("能量回复速度")]
        [Tooltip("每个现实秒回复的能量点数。")]
        private float energyRecoveryPerSecond = DefaultEnergyRecoveryPerSecond;

        [SerializeField, Min(0), InspectorName("格挡上限")]
        private int maxBlock = DefaultMaxBlock;

        [SerializeField, Min(0f), InspectorName("格挡衰减速度")]
        [Tooltip("每个游戏秒衰减的格挡点数，受游戏时间倍率影响。")]
        private float blockDecayPerSecond = DefaultBlockDecayPerSecond;

        [SerializeField, Min(0f), InspectorName("移动速度")]
        [Tooltip("每个游戏秒移动的世界单位数。")]
        private float moveSpeed = DefaultMoveSpeed;

        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int MaxEnergy => Mathf.Max(1, maxEnergy);
        public int MaxBlock => Mathf.Max(0, maxBlock);
        public float EnergyRecoveryPerSecond => Mathf.Max(0f, energyRecoveryPerSecond);
        public float BlockDecayPerSecond => Mathf.Max(0f, blockDecayPerSecond);
        public float MoveSpeed => Mathf.Max(0f, moveSpeed);
    }
}
