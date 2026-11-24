namespace Framework.FrameworkEvent
{
    /// <summary>
    /// 该类用于创建名称的常量表 (建议使用)
    /// </summary>
    public static class EventConst
    {
        public static string MonsterDead => "MonsterDead";
        // 战斗事件
        public const string PLAYER_ATTACK_HIT = "PlayerAttackHit";           // 玩家攻击命中
        public const string PLAYER_DAMAGED = "PlayerDamaged";                // 玩家受到伤害
        public const string PLAYER_DODGE_START = "PlayerDodgeStart";         // 闪避开始
        public const string PLAYER_DODGE_END = "PlayerDodgeEnd";             // 闪避结束
        public const string TOWER_ATTACK_HIT = "TowerAttackHit";            // 塔攻击命中
        // 塔防系统事件
        public const string TOWER_BUILT = "TowerBuilt";           // 塔建造完成
        public const string TOWER_UPGRADED = "TowerUpgraded";      // 塔升级完成
        public const string TOWER_SOLD = "TowerSold";              // 塔出售
        public const string RESOURCE_CHANGED = "ResourceChanged";  // 金币变动
        public const string EnemyDead = "EnemyDead"; // 敌人死亡
        // 核心事件
        public const string CORE_DAMAGED = "CoreDamaged";       // 核心受到伤害（参数 int 剩余HP）
        public const string CORE_DESTROYED = "CoreDestroyed";   // 核心被摧毁
        // 波次事件
        public const string WAVE_STARTED = "WaveStarted";           // 阶段开始（参数 int 阶段索引）
        public const string WAVE_ENDED = "WaveEnded";               // 阶段结束（参数 int 阶段索引）
        public const string ALL_WAVES_COMPLETED = "AllWavesCompleted"; // 全部波次完成（无参数）
        // 音乐事件
        public const string BGM_TRACK_CHANGED = "BgmTrackChanged";  // BGM 曲目切换（参数 int 曲目下标）
    }
}